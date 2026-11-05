using System.Runtime.InteropServices;
using Liquidino.Nucleo;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace Liquidino;

/// <summary>I dispositivi audio di Windows per nome: microfoni (ingressi) e uscite.</summary>
public static class Dispositivi
{
    public static IReadOnlyList<string> Ingressi() => Nomi(DataFlow.Capture);
    public static IReadOnlyList<string> Uscite() => Nomi(DataFlow.Render);

    static IReadOnlyList<string> Nomi(DataFlow verso)
    {
        try
        {
            using var elenco = new MMDeviceEnumerator();
            return elenco.EnumerateAudioEndPoints(verso, DeviceState.Active).Select(d => d.FriendlyName).ToList();
        }
        catch { return []; }
    }

    public static string? MicrofonoPredefinito()
    {
        try
        {
            using var elenco = new MMDeviceEnumerator();
            if (!elenco.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)) return null;
            return elenco.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications).FriendlyName;
        }
        catch { return null; }
    }

    public static MMDevice? Trova(DataFlow verso, string nome)
    {
        try
        {
            var elenco = new MMDeviceEnumerator();
            return elenco.EnumerateAudioEndPoints(verso, DeviceState.Active).FirstOrDefault(d => d.FriendlyName == nome);
        }
        catch { return null; }
    }

    /// <summary>Il microfono vero da usare: lo scelto, o quello di Windows, mai l'uscita di VB-CABLE.</summary>
    public static MMDevice? Microfono(string? scelto)
    {
        var nome = CavoVirtuale.Microfono(Ingressi(), MicrofonoPredefinito(), scelto);
        return nome is null ? null : Trova(DataFlow.Capture, nome);
    }

    /// <summary>«CABLE Input» di VB-CABLE, se è installato.</summary>
    public static MMDevice? Cavo()
    {
        var nome = CavoVirtuale.Ingresso(Uscite());
        return nome is null ? null : Trova(DataFlow.Render, nome);
    }

    /// <summary>KSDATAFORMAT_SUBTYPE_IEEE_FLOAT: i campioni sono float a 32 bit.</summary>
    static readonly Guid FloatIeee = new("00000003-0000-0010-8000-00aa00389b71");

    /// <summary>I campioni di un buffer WASAPI come float intrecciati, qualunque sia il formato condiviso.</summary>
    public static float[] AFloat(byte[] buffer, int byteLetti, WaveFormat formato)
    {
        var dati = buffer.AsSpan(0, byteLetti);
        var floatSiCuro = formato.Encoding == WaveFormatEncoding.IeeeFloat
            || (formato is WaveFormatExtensible est && est.SubFormat == FloatIeee);
        if (formato.BitsPerSample == 32 && floatSiCuro) return Campioni.DaFloat32(dati);
        if (formato.BitsPerSample == 16) return Campioni.DaPcm16(dati);
        if (formato.BitsPerSample == 24)
        {
            var n = dati.Length / 3;
            var uscita = new float[n];
            for (var i = 0; i < n; i++)
                uscita[i] = ((dati[i * 3] << 8) | (dati[i * 3 + 1] << 16) | (dati[i * 3 + 2] << 24)) / 2147483648f;
            return uscita;
        }
        if (formato.BitsPerSample == 32)
        {
            var n = dati.Length / 4;
            var uscita = new float[n];
            for (var i = 0; i < n; i++) uscita[i] = BitConverter.ToInt32(dati.Slice(i * 4, 4)) / 2147483648f;
            return uscita;
        }
        throw new NotSupportedException("formato del microfono non gestito: " + formato);
    }
}

/// <summary>
/// La cattura dell'audio di un programma che Windows offre da sé (dalla 10 versione 2004), la stessa che usa OBS:
/// «includi» sente solo quel programma e i suoi figli (Discord in chiamata), «escludi» sente tutto il PC tranne loro
/// (tutto tranne la voce di Liquidino). Niente driver. Esce a 16 kHz mono, convertito da Windows.
/// </summary>
public sealed class CatturaProcesso : IDisposable
{
    const string PercorsoLoopback = "VAD\\Process_Loopback";
    static readonly Guid IidAudioClient = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");

    [DllImport("Mmdevapi.dll", ExactSpelling = true, PreserveSig = false)]
    static extern void ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        IntPtr activationParams,
        IActivateCompletamento completionHandler,
        out IActivateOperazione activationOperation);

    [ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IActivateOperazione
    {
        void GetActivateResult(out int activateResult, [MarshalAs(UnmanagedType.IUnknown)] out object activatedInterface);
    }

    [ComImport, Guid("41D949AB-9862-444A-80F6-C261334DA5EB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IActivateCompletamento
    {
        void ActivateCompleted(IActivateOperazione activateOperation);
    }

    /// <summary>Windows chiama il completamento da un suo filo: l'oggetto deve dichiararsi agile.</summary>
    [ComImport, Guid("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAgileObject { }

    [ComVisible(true)]
    sealed class Completamento : IActivateCompletamento, IAgileObject
    {
        public readonly TaskCompletionSource<IAudioClient> Fatto = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ActivateCompleted(IActivateOperazione operazione)
        {
            try
            {
                operazione.GetActivateResult(out var esito, out var interfaccia);
                if (esito < 0) Fatto.TrySetException(Marshal.GetExceptionForHR(esito) ?? new COMException("attivazione audio", esito));
                else Fatto.TrySetResult((IAudioClient)interfaccia);
            }
            catch (Exception e) { Fatto.TrySetException(e); }
        }
    }

    readonly AudioClient _client;
    readonly AutoResetEvent _pronto = new(false);
    readonly Thread _filo;
    volatile bool _ferma;
    public event Action<float[]>? Campioni16k;
    public event Action<Exception>? Errore;

    CatturaProcesso(AudioClient client)
    {
        _client = client;
        _filo = new Thread(Ciclo) { IsBackground = true, Name = "Liquidino orecchie (programma)" };
    }

    /// <summary>Apre la cattura. Fallisce se Windows è troppo vecchio o non c'è un'uscita audio.</summary>
    public static async Task<CatturaProcesso> Apri(int pid, bool includi, CancellationToken ct)
    {
        // AUDIOCLIENT_ACTIVATION_PARAMS: tipo 1 (process loopback), il processo, modo 0 = includi l'albero, 1 = escludilo.
        var parametri = Marshal.AllocHGlobal(12);
        // PROPVARIANT con VT_BLOB: tipo all'inizio, poi la lunghezza e il puntatore ai parametri.
        var variante = Marshal.AllocHGlobal(24);
        try
        {
            Marshal.WriteInt32(parametri, 0, 1);
            Marshal.WriteInt32(parametri, 4, pid);
            Marshal.WriteInt32(parametri, 8, includi ? 0 : 1);
            for (var i = 0; i < 24; i++) Marshal.WriteByte(variante, i, 0);
            Marshal.WriteInt16(variante, 0, 65);
            Marshal.WriteInt32(variante, 8, 12);
            Marshal.WriteIntPtr(variante, 8 + IntPtr.Size, parametri);
            var completamento = new Completamento();
            ActivateAudioInterfaceAsync(PercorsoLoopback, IidAudioClient, variante, completamento, out var operazione);
            IAudioClient interfaccia;
            using (ct.Register(() => completamento.Fatto.TrySetCanceled()))
                interfaccia = await completamento.Fatto.Task.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            GC.KeepAlive(operazione);
            var client = new AudioClient(interfaccia);
            var flag = AudioClientStreamFlags.Loopback | AudioClientStreamFlags.EventCallback
                       | AudioClientStreamFlags.AutoConvertPcm | AudioClientStreamFlags.SrcDefaultQuality;
            client.Initialize(AudioClientShareMode.Shared, flag, 200 * 10000, 0, new WaveFormat(Nucleo.Campioni.Frequenza, 16, 1), Guid.Empty);
            var cattura = new CatturaProcesso(client);
            client.SetEventHandle(cattura._pronto.SafeWaitHandle.DangerousGetHandle());
            return cattura;
        }
        finally
        {
            Marshal.FreeHGlobal(variante);
            Marshal.FreeHGlobal(parametri);
        }
    }

    public void Avvia()
    {
        _client.Start();
        _filo.Start();
    }

    void Ciclo()
    {
        try
        {
            var cattura = _client.AudioCaptureClient;
            while (!_ferma)
            {
                _pronto.WaitOne(100);
                int pacchetto;
                while (!_ferma && (pacchetto = cattura.GetNextPacketSize()) > 0)
                {
                    var dati = cattura.GetBuffer(out var fotogrammi, out var flag);
                    var campioni = new float[fotogrammi];
                    if ((flag & AudioClientBufferFlags.Silent) == 0 && fotogrammi > 0)
                    {
                        var grezzi = new short[fotogrammi];
                        Marshal.Copy(dati, grezzi, 0, fotogrammi);
                        for (var i = 0; i < fotogrammi; i++) campioni[i] = grezzi[i] / 32768f;
                    }
                    cattura.ReleaseBuffer(fotogrammi);
                    if (fotogrammi > 0) Campioni16k?.Invoke(campioni);
                }
            }
        }
        catch (Exception e)
        {
            if (!_ferma) Errore?.Invoke(e);
        }
    }

    public void Dispose()
    {
        _ferma = true;
        _pronto.Set();
        try { if (_filo.IsAlive) _filo.Join(1000); } catch { /* il filo è già finito */ }
        try { _client.Stop(); } catch { /* già fermo */ }
        try { _client.Dispose(); } catch { /* già chiuso */ }
        _pronto.Dispose();
    }
}

/// <summary>Il microfono vero, nel suo formato. Dà campioni a 16 kHz per le orecchie e quelli originali per il cavo.</summary>
public sealed class CatturaMicrofono : IDisposable
{
    readonly WasapiCapture _cattura;
    public string Nome { get; }
    public WaveFormat Formato => _cattura.WaveFormat;
    public event Action<float[]>? Campioni16k;
    public event Action<float[], int, int>? CampioniOriginali;
    public event Action<Exception>? Errore;

    public CatturaMicrofono(MMDevice dispositivo)
    {
        Nome = dispositivo.FriendlyName;
        _cattura = new WasapiCapture(dispositivo, true, 20);
        _cattura.DataAvailable += (_, a) =>
        {
            if (a.BytesRecorded <= 0) return;
            try
            {
                var f = _cattura.WaveFormat;
                var intrecciati = Dispositivi.AFloat(a.Buffer, a.BytesRecorded, f);
                CampioniOriginali?.Invoke(intrecciati, f.SampleRate, f.Channels);
                Campioni16k?.Invoke(Nucleo.Campioni.A16kMono(intrecciati, f.SampleRate, f.Channels));
            }
            catch (Exception e) { Errore?.Invoke(e); }
        };
        _cattura.RecordingStopped += (_, a) => { if (a.Exception is not null) Errore?.Invoke(a.Exception); };
    }

    public void Avvia() => _cattura.StartRecording();

    /// <summary>Registra qualche secondo a 16 kHz mono, per la taratura della parola.</summary>
    public static async Task<float[]> Registra(NAudio.CoreAudioApi.MMDevice dispositivo, TimeSpan durata, CancellationToken ct)
    {
        var voluti = (int)(durata.TotalSeconds * Nucleo.Campioni.Frequenza);
        var raccolti = new List<float>(voluti + 4000);
        var fatto = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var mic = new CatturaMicrofono(dispositivo);
        mic.Campioni16k += c =>
        {
            lock (raccolti)
            {
                raccolti.AddRange(c);
                if (raccolti.Count >= voluti) fatto.TrySetResult();
            }
        };
        mic.Errore += e => fatto.TrySetException(e);
        mic.Avvia();
        using (ct.Register(() => fatto.TrySetCanceled()))
            await fatto.Task.WaitAsync(durata + TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        lock (raccolti) return raccolti.Take(voluti).ToArray();
    }

    public void Dispose()
    {
        try { _cattura.StopRecording(); } catch { /* già fermo */ }
        try { _cattura.Dispose(); } catch { /* già chiuso */ }
    }
}

/// <summary>
/// La voce di Liquidino nel tuo microfono, per la chiamata: il tuo microfono più la sua voce, suonati su «CABLE Input».
/// Nel programma della chiamata scegli «CABLE Output» come microfono. La sua voce continua a uscire anche dalle tue casse.
/// </summary>
public sealed class VoceNelMicrofono : IDisposable
{
    readonly int _frequenza;
    readonly int _canali;
    readonly WasapiOut _uscita;
    readonly BufferedWaveProvider _mic;
    readonly BufferedWaveProvider _voce;
    public string Cavo { get; }
    public float Guadagno { get; set; } = 1f;

    public VoceNelMicrofono(MMDevice cavo)
    {
        Cavo = cavo.FriendlyName;
        // Il formato del cavo così com'è in Windows: niente ricampionamento in più dentro WASAPI.
        var misto = cavo.AudioClient.MixFormat;
        _frequenza = misto.SampleRate > 0 ? misto.SampleRate : 48000;
        _canali = misto.Channels is > 0 and <= 8 ? misto.Channels : 2;
        var formato = WaveFormat.CreateIeeeFloatWaveFormat(_frequenza, _canali);
        _mic = new BufferedWaveProvider(formato) { DiscardOnBufferOverflow = true, BufferDuration = TimeSpan.FromSeconds(1), ReadFully = true };
        _voce = new BufferedWaveProvider(formato) { DiscardOnBufferOverflow = true, BufferDuration = TimeSpan.FromSeconds(60), ReadFully = true };
        _uscita = new WasapiOut(cavo, AudioClientShareMode.Shared, true, 40);
        _uscita.Init(new Somma(this, formato));
        _uscita.Play();
    }

    /// <summary>Il tuo microfono, così com'è: si ricampiona al formato del cavo.</summary>
    public void DalMicrofono(float[] intrecciati, int frequenza, int canali)
    {
        // Se gli orologi del microfono e del cavo non vanno uguali, il ritardo crescerebbe: oltre 150 ms si salta un pezzetto.
        if (_mic.BufferedDuration > TimeSpan.FromMilliseconds(150)) return;
        var pronti = Nucleo.Campioni.Ricampiona(intrecciati, frequenza, canali, _frequenza, _canali);
        Aggiungi(_mic, pronti);
    }

    /// <summary>La frase di Liquidino (un WAV) da mettere nel microfono.</summary>
    public void Suona(byte[] wav)
    {
        var letto = Wav.Leggi(wav);
        Aggiungi(_voce, Nucleo.Campioni.Ricampiona(letto.Campioni, letto.Frequenza, letto.Canali, _frequenza, _canali));
    }

    public void Zitto() => _voce.ClearBuffer();

    static void Aggiungi(BufferedWaveProvider dove, float[] campioni)
    {
        if (campioni.Length == 0) return;
        var byteDati = new byte[campioni.Length * 4];
        Buffer.BlockCopy(campioni, 0, byteDati, 0, byteDati.Length);
        dove.AddSamples(byteDati, 0, byteDati.Length);
    }

    sealed class Somma(VoceNelMicrofono padre, WaveFormat formato) : IWaveProvider
    {
        byte[] _voce = [];
        public WaveFormat WaveFormat => formato;

        public int Read(byte[] buffer, int offset, int count)
        {
            padre._mic.Read(buffer, offset, count);
            if (_voce.Length < count) _voce = new byte[count];
            padre._voce.Read(_voce, 0, count);
            var mic = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(offset, count));
            var voce = MemoryMarshal.Cast<byte, float>(_voce.AsSpan(0, count));
            Miscela.Somma(mic, voce, padre.Guadagno);
            return count;
        }
    }

    public void Dispose()
    {
        try { _uscita.Stop(); } catch { /* già fermo */ }
        try { _uscita.Dispose(); } catch { /* già chiuso */ }
    }
}
