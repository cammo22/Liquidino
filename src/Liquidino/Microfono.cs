using NAudio.Wave;

namespace Liquidino;

/// <summary>Registra dal microfono un WAV a 16 kHz, solo in memoria di lavoro. Non si tiene l'audio.</summary>
public sealed class Microfono : IDisposable
{
    WaveInEvent? _onda;
    WaveFileWriter? _file;
    string? _percorso;

    public bool StaRegistrando { get; private set; }

    public void Inizia()
    {
        if (StaRegistrando) return;
        _percorso = Path.Combine(Path.GetTempPath(), "liquidino-orecchio-" + Guid.NewGuid().ToString("N") + ".wav");
        _onda = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 40
        };
        _file = new WaveFileWriter(_percorso, _onda.WaveFormat);
        _onda.DataAvailable += (_, a) =>
        {
            try { _file?.Write(a.Buffer, 0, a.BytesRecorded); }
            catch { /* la registrazione sta per chiudersi */ }
        };
        _onda.StartRecording();
        StaRegistrando = true;
    }

    public byte[]? Ferma()
    {
        if (!StaRegistrando && _file is null) return null;
        StaRegistrando = false;
        try { _onda?.StopRecording(); } catch { /* già fermo */ }
        try { _file?.Dispose(); } catch { /* file già chiuso */ }
        try { _onda?.Dispose(); } catch { /* dispositivo già chiuso */ }
        _file = null;
        _onda = null;
        if (_percorso is null || !File.Exists(_percorso)) return null;
        var bytes = File.ReadAllBytes(_percorso);
        try { File.Delete(_percorso); } catch { /* il file temporaneo si può lasciare, è solo voce appena detta */ }
        _percorso = null;
        return bytes;
    }

    public void Dispose() => Ferma();
}

/// <summary>Legge il WAV della voce e lo butta appena finisce.</summary>
public sealed class LettoreVoce : IDisposable
{
    WaveOutEvent? _uscita;
    WaveFileReader? _lettura;
    string? _file;
    int _chiusura;
    public event Action? Finito;

    public void Suona(string wav)
    {
        Ferma();
        _file = wav;
        _lettura = new WaveFileReader(wav);
        _uscita = new WaveOutEvent();
        _uscita.Init(_lettura);
        _uscita.PlaybackStopped += (_, _) => Chiudi();
        _uscita.Play();
    }

    public void Ferma() => Chiudi();

    void Chiudi()
    {
        if (Interlocked.Exchange(ref _chiusura, 1) == 1) return;
        var uscita = _uscita;
        var lettura = _lettura;
        var file = _file;
        _uscita = null;
        _lettura = null;
        _file = null;
        try { uscita?.Stop(); } catch { /* già fermo */ }
        try { uscita?.Dispose(); } catch { /* già chiuso */ }
        try { lettura?.Dispose(); } catch { /* già chiuso */ }
        if (file is not null) { try { File.Delete(file); } catch { /* si cancella al prossimo avvio */ } }
        _chiusura = 0;
        if (uscita is not null) Finito?.Invoke();
    }

    public void Dispose() => Ferma();
}
