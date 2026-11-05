using System.Buffers.Binary;

namespace Liquidino.Nucleo;

/// <summary>Un WAV letto: campioni float fra -1 e 1, intrecciati per canale.</summary>
public sealed record AudioLetto(float[] Campioni, int Frequenza, int Canali);

/// <summary>Legge e scrive WAV PCM a 16 bit (e float a 32 bit in lettura). Basta per microfono, voce e prove.</summary>
public static class Wav
{
    public static AudioLetto Leggi(byte[] dati)
    {
        if (dati.Length < 12 || dati[0] != 'R' || dati[1] != 'I' || dati[2] != 'F' || dati[3] != 'F' || dati[8] != 'W' || dati[9] != 'A')
            throw new InvalidDataException("non è un file WAV");
        int formato = 0, canali = 0, frequenza = 0, bit = 0;
        var pos = 12;
        while (pos + 8 <= dati.Length)
        {
            var id = System.Text.Encoding.ASCII.GetString(dati, pos, 4);
            var lunghezza = BinaryPrimitives.ReadInt32LittleEndian(dati.AsSpan(pos + 4));
            var corpo = pos + 8;
            if (lunghezza < 0 || corpo > dati.Length) break;
            var fine = (int)Math.Min((long)corpo + lunghezza, dati.Length);
            if (id == "fmt " && fine - corpo >= 16)
            {
                formato = BinaryPrimitives.ReadInt16LittleEndian(dati.AsSpan(corpo));
                canali = BinaryPrimitives.ReadInt16LittleEndian(dati.AsSpan(corpo + 2));
                frequenza = BinaryPrimitives.ReadInt32LittleEndian(dati.AsSpan(corpo + 4));
                bit = BinaryPrimitives.ReadInt16LittleEndian(dati.AsSpan(corpo + 14));
                if (formato == unchecked((short)0xFFFE) && fine - corpo >= 26)
                    formato = BinaryPrimitives.ReadInt16LittleEndian(dati.AsSpan(corpo + 24));
            }
            else if (id == "data")
            {
                if (canali <= 0 || frequenza <= 0) throw new InvalidDataException("WAV senza formato prima dei dati");
                return new AudioLetto(Campioni(dati.AsSpan(corpo, fine - corpo), formato, bit), frequenza, canali);
            }
            pos = corpo + lunghezza + (lunghezza & 1);
        }
        throw new InvalidDataException("WAV senza dati");
    }

    static float[] Campioni(ReadOnlySpan<byte> dati, int formato, int bit)
    {
        if (formato == 1 && bit == 16)
        {
            var n = dati.Length / 2;
            var uscita = new float[n];
            for (var i = 0; i < n; i++) uscita[i] = BinaryPrimitives.ReadInt16LittleEndian(dati[(i * 2)..]) / 32768f;
            return uscita;
        }
        if (formato == 3 && bit == 32)
        {
            var n = dati.Length / 4;
            var uscita = new float[n];
            for (var i = 0; i < n; i++) uscita[i] = BinaryPrimitives.ReadSingleLittleEndian(dati[(i * 4)..]);
            return uscita;
        }
        throw new InvalidDataException("formato WAV non gestito (" + formato + ", " + bit + " bit)");
    }

    /// <summary>Un WAV PCM a 16 bit, mono.</summary>
    public static byte[] Scrivi(ReadOnlySpan<float> campioni, int frequenza)
    {
        var dati = campioni.Length * 2;
        var uscita = new byte[44 + dati];
        void Testo(int pos, string t) => System.Text.Encoding.ASCII.GetBytes(t, uscita.AsSpan(pos));
        Testo(0, "RIFF");
        BinaryPrimitives.WriteInt32LittleEndian(uscita.AsSpan(4), 36 + dati);
        Testo(8, "WAVE");
        Testo(12, "fmt ");
        BinaryPrimitives.WriteInt32LittleEndian(uscita.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(uscita.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(uscita.AsSpan(22), 1);
        BinaryPrimitives.WriteInt32LittleEndian(uscita.AsSpan(24), frequenza);
        BinaryPrimitives.WriteInt32LittleEndian(uscita.AsSpan(28), frequenza * 2);
        BinaryPrimitives.WriteInt16LittleEndian(uscita.AsSpan(32), 2);
        BinaryPrimitives.WriteInt16LittleEndian(uscita.AsSpan(34), 16);
        Testo(36, "data");
        BinaryPrimitives.WriteInt32LittleEndian(uscita.AsSpan(40), dati);
        for (var i = 0; i < campioni.Length; i++)
            BinaryPrimitives.WriteInt16LittleEndian(uscita.AsSpan(44 + i * 2), (short)Math.Round(Math.Clamp(campioni[i], -1f, 1f) * 32767f));
        return uscita;
    }
}

/// <summary>Conversioni dei campioni: quello che arriva da Windows diventa 16 kHz mono, come lo vuole Parakeet.</summary>
public static class Campioni
{
    public const int Frequenza = 16000;

    public static float[] DaPcm16(ReadOnlySpan<byte> dati)
    {
        var n = dati.Length / 2;
        var uscita = new float[n];
        for (var i = 0; i < n; i++) uscita[i] = BinaryPrimitives.ReadInt16LittleEndian(dati[(i * 2)..]) / 32768f;
        return uscita;
    }

    public static float[] DaFloat32(ReadOnlySpan<byte> dati)
    {
        var n = dati.Length / 4;
        var uscita = new float[n];
        for (var i = 0; i < n; i++) uscita[i] = BinaryPrimitives.ReadSingleLittleEndian(dati[(i * 4)..]);
        return uscita;
    }

    /// <summary>Media dei canali, poi a 16 kHz. Se la frequenza è un multiplo di 16 kHz fa la media dei gruppi, se no interpola.</summary>
    public static float[] A16kMono(ReadOnlySpan<float> intrecciati, int frequenza, int canali)
    {
        if (frequenza <= 0 || canali <= 0) throw new ArgumentException("formato audio non valido");
        var fotogrammi = intrecciati.Length / canali;
        var mono = new float[fotogrammi];
        for (var f = 0; f < fotogrammi; f++)
        {
            float somma = 0;
            for (var c = 0; c < canali; c++) somma += intrecciati[f * canali + c];
            mono[f] = somma / canali;
        }
        if (frequenza == Frequenza) return mono;
        if (frequenza % Frequenza == 0)
        {
            var passo = frequenza / Frequenza;
            var uscita = new float[fotogrammi / passo];
            for (var i = 0; i < uscita.Length; i++)
            {
                float somma = 0;
                for (var k = 0; k < passo; k++) somma += mono[i * passo + k];
                uscita[i] = somma / passo;
            }
            return uscita;
        }
        var rapporto = (double)frequenza / Frequenza;
        var lunghezza = (int)(fotogrammi / rapporto);
        var interpolati = new float[lunghezza];
        for (var i = 0; i < lunghezza; i++)
        {
            var x = i * rapporto;
            var a = (int)x;
            var b = Math.Min(a + 1, fotogrammi - 1);
            var t = (float)(x - a);
            interpolati[i] = mono[a] * (1 - t) + mono[b] * t;
        }
        return interpolati;
    }

    /// <summary>Qualunque frequenza e numero di canali in un'altra, interpolando. Mono → stereo copia, stereo → mono fa la media.</summary>
    public static float[] Ricampiona(ReadOnlySpan<float> intrecciati, int frequenza, int canali, int nuovaFrequenza, int nuoviCanali)
    {
        if (frequenza <= 0 || canali <= 0 || nuovaFrequenza <= 0 || nuoviCanali <= 0) throw new ArgumentException("formato audio non valido");
        var fotogrammi = intrecciati.Length / canali;
        if (fotogrammi == 0) return [];
        var mono = canali == 1 ? intrecciati[..fotogrammi].ToArray() : new float[fotogrammi];
        if (canali > 1)
            for (var f = 0; f < fotogrammi; f++)
            {
                float somma = 0;
                for (var c = 0; c < canali; c++) somma += intrecciati[f * canali + c];
                mono[f] = somma / canali;
            }
        // Con gli stessi canali si tengono separati; se no si passa dal mono.
        var stessi = canali == nuoviCanali;
        var rapporto = (double)frequenza / nuovaFrequenza;
        var lunghezza = (int)Math.Floor(fotogrammi / rapporto);
        var uscita = new float[lunghezza * nuoviCanali];
        for (var i = 0; i < lunghezza; i++)
        {
            var x = i * rapporto;
            var a = (int)x;
            var b = Math.Min(a + 1, fotogrammi - 1);
            var t = (float)(x - a);
            for (var c = 0; c < nuoviCanali; c++)
            {
                float va, vb;
                if (stessi) { va = intrecciati[a * canali + c]; vb = intrecciati[b * canali + c]; }
                else { va = mono[a]; vb = mono[b]; }
                uscita[i * nuoviCanali + c] = va * (1 - t) + vb * t;
            }
        }
        return uscita;
    }

    public static double Rms(ReadOnlySpan<float> campioni)
    {
        if (campioni.Length == 0) return 0;
        double somma = 0;
        foreach (var c in campioni) somma += c * c;
        return Math.Sqrt(somma / campioni.Length);
    }
}

/// <summary>Una frase sentita: i campioni a 16 kHz e quando è cominciata, in secondi dall'accensione delle orecchie.</summary>
public sealed record FraseSentita(float[] Campioni, double Inizio)
{
    public double Secondi => Campioni.Length / 16000.0;
}

/// <summary>
/// Trova le frasi in un flusso a 16 kHz mono guardando l'energia, a blocchi da 100 ms. Leggero: niente modelli.
/// Impara da solo il fondo della stanza. Una frase comincia con due blocchi forti (tre volte il fondo)
/// e finisce dopo nove blocchi quieti, o a 15 secondi. Tiene 300 ms prima dell'inizio, per non tagliare la prima sillaba.
/// </summary>
public sealed class RilevatoreVoce
{
    public const int Blocco = Campioni.Frequenza / 10;

    public int BlocchiPerIniziare { get; init; } = 2;
    public int BlocchiPerFinire { get; init; } = 9;
    public double SecondiMassimi { get; init; } = 15;
    public double SecondiMinimi { get; init; } = 0.35;
    public double RapportoSulFondo { get; init; } = 3;
    public double MinimoAssoluto { get; init; } = 0.006;
    const int BlocchiPrima = 3;
    const int BlocchiPerImparare = 3;

    readonly float[] _blocco = new float[Blocco];
    int _pieni;
    readonly Queue<float[]> _prima = new();
    readonly List<float[]> _candidati = [];
    readonly List<float[]> _frase = [];
    int _quieti;
    bool _dentro;
    double _inizio;
    long _blocchi;
    double? _fondo;

    public double Fondo => _fondo ?? 0;
    public bool InFrase => _dentro;

    public void Azzera()
    {
        _pieni = 0;
        _prima.Clear();
        _candidati.Clear();
        _frase.Clear();
        _quieti = 0;
        _dentro = false;
    }

    /// <summary>Aggiunge campioni (anche a pezzi di qualunque lunghezza). Ritorna le frasi finite.</summary>
    public IReadOnlyList<FraseSentita> Aggiungi(ReadOnlySpan<float> campioni)
    {
        List<FraseSentita>? finite = null;
        while (campioni.Length > 0)
        {
            var quanti = Math.Min(Blocco - _pieni, campioni.Length);
            campioni[..quanti].CopyTo(_blocco.AsSpan(_pieni));
            _pieni += quanti;
            campioni = campioni[quanti..];
            if (_pieni < Blocco) break;
            _pieni = 0;
            if (Blocco1((float[])_blocco.Clone()) is { } frase) (finite ??= []).Add(frase);
        }
        return (IReadOnlyList<FraseSentita>?)finite ?? [];
    }

    /// <summary>Chiude la frase in corso, se ce n'è una abbastanza lunga.</summary>
    public FraseSentita? Svuota()
    {
        var frase = _dentro ? Chiudi() : null;
        Azzera();
        return frase;
    }

    FraseSentita? Blocco1(float[] blocco)
    {
        _blocchi++;
        var rms = Campioni.Rms(blocco);
        if (_fondo is null || _blocchi <= BlocchiPerImparare)
        {
            _fondo = _fondo is null ? rms : Math.Min(_fondo.Value, rms);
            Prima(blocco);
            return null;
        }
        var forte = rms > Math.Max(_fondo.Value * RapportoSulFondo, MinimoAssoluto);
        if (!_dentro)
        {
            if (forte)
            {
                _candidati.Add(blocco);
                if (_candidati.Count >= BlocchiPerIniziare)
                {
                    _dentro = true;
                    _quieti = 0;
                    _inizio = (_blocchi - _candidati.Count - _prima.Count) / 10.0;
                    _frase.Clear();
                    _frase.AddRange(_prima);
                    _frase.AddRange(_candidati);
                    _prima.Clear();
                    _candidati.Clear();
                }
                return null;
            }
            foreach (var c in _candidati) Prima(c);
            _candidati.Clear();
            // Il fondo segue piano la stanza, solo quando non parla nessuno.
            _fondo = Math.Max(1e-5, _fondo.Value * 0.95 + rms * 0.05);
            Prima(blocco);
            return null;
        }
        _frase.Add(blocco);
        _quieti = forte ? 0 : _quieti + 1;
        if (_quieti >= BlocchiPerFinire || _frase.Count >= SecondiMassimi * 10)
            return Chiudi();
        return null;
    }

    void Prima(float[] blocco)
    {
        _prima.Enqueue(blocco);
        while (_prima.Count > BlocchiPrima) _prima.Dequeue();
    }

    FraseSentita? Chiudi()
    {
        // Della coda quieta si tengono 200 ms: la frase finisce dove finisce la voce.
        var togli = Math.Max(0, _quieti - 2);
        var tieni = _frase.Count - togli;
        var voce = (tieni - BlocchiPrima - Math.Min(_quieti, 2)) / 10.0;
        FraseSentita? frase = null;
        if (voce >= SecondiMinimi)
        {
            var campioni = new float[tieni * Blocco];
            for (var i = 0; i < tieni; i++) _frase[i].CopyTo(campioni, i * Blocco);
            frase = new FraseSentita(campioni, _inizio);
        }
        _frase.Clear();
        _dentro = false;
        _quieti = 0;
        return frase;
    }
}

/// <summary>La voce di Liquidino sopra il tuo microfono, per la chiamata. Somma e poi ammorbidisce i picchi, senza scatti.</summary>
public static class Miscela
{
    public static void Somma(Span<float> destinazione, ReadOnlySpan<float> voce, float guadagno)
    {
        var n = Math.Min(destinazione.Length, voce.Length);
        for (var i = 0; i < n; i++) destinazione[i] += voce[i] * guadagno;
        for (var i = 0; i < destinazione.Length; i++) destinazione[i] = Morbido(destinazione[i]);
    }

    /// <summary>Lineare fino a 0,8, poi curva dolcemente verso 1: niente distorsione dura quando parlate insieme.</summary>
    public static float Morbido(float x)
    {
        var a = Math.Abs(x);
        if (a <= 0.8f) return x;
        var sopra = (a - 0.8f) / 0.2f;
        var curvo = 0.8f + 0.2f * (float)Math.Tanh(sopra);
        return Math.Sign(x) * curvo;
    }
}
