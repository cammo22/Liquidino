namespace Liquidino.Nucleo;

/// <summary>Frasi e pulizia del testo. Quando non ce la fa, Liquidino lo dice chiaro.</summary>
public static class Testo
{
    public static string FalloTu(string perche)
    {
        perche = (perche ?? "").Trim().TrimEnd('.');
        if (perche.Length == 0) perche = "non ci sono riuscito e non so dirti di più";
        return "Questo fallo tu: " + char.ToLowerInvariant(perche[0]) + perche[1..] + ".";
    }

    /// <summary>Toglie i ragionamenti fra &lt;think&gt; che non vanno letti ad alta voce.</summary>
    public static string PulisciRisposta(string? testo)
    {
        if (string.IsNullOrEmpty(testo)) return "";
        var s = testo;
        while (true)
        {
            var a = s.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
            if (a < 0) break;
            var b = s.IndexOf("</think>", a, StringComparison.OrdinalIgnoreCase);
            if (b < 0) { s = s[..a]; break; }
            s = s.Remove(a, b + "</think>".Length - a);
        }
        return s.Trim();
    }

    public static string FormatoVirgola(double valore)
    {
        var s = valore.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        return s.Replace('.', ',');
    }

    public static string NomeFileSicuro(string titolo)
    {
        var invalidi = Path.GetInvalidFileNameChars();
        var pulito = new string((titolo ?? "").Select(c => invalidi.Contains(c) ? '-' : c).ToArray()).Trim().Trim('.');
        if (pulito.Length == 0) pulito = "nota";
        if (pulito.Length > 48) pulito = pulito[..48].Trim();
        return pulito;
    }

    public static bool ESi(string testo)
    {
        var t = Normalizza(testo);
        return t is "sì" or "si" or "ok" or "va bene" or "sì grazie" or "si grazie" or "dai" or "fallo" or "certo" or "yes";
    }

    public static bool ENo(string testo)
    {
        var t = Normalizza(testo);
        return t is "no" or "niente" or "lascia stare" or "lascia perdere" or "annulla" or "no grazie";
    }

    /// <summary>Chiede di usare mouse, tastiera o altri programmi: nel prototipo non si fa.</summary>
    public static bool ChiedeIlPc(string testo)
    {
        var s = (testo ?? "").ToLowerInvariant();
        string[] chiavi =
        [
            "clicca", "muovi il mouse", "sposta il mouse", "apri il programma", "usa il pc",
            "usa il computer", "premi il tasto", "scrivi al posto mio", "controlla il desktop",
            "apri esplora", "lancia "
        ];
        return chiavi.Any(s.Contains);
    }

    static string Normalizza(string testo)
    {
        var t = (testo ?? "").Trim().TrimEnd('.', '!', '?').ToLowerInvariant();
        return t.Replace("'", "'").Trim();
    }
}
