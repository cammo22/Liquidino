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

    public static string OraLocale(DateTimeOffset ora)
    {
        var it = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
        return "Sono le " + ora.ToString("HH:mm", it) + " di " + ora.ToString("dddd d MMMM yyyy", it) + ".";
    }

    /// <summary>Sì anche dentro una frase breve: «certo, vai», «ok fallo», «… Sì».</summary>
    public static bool ESi(string testo) => Verdetto(testo) > 0;

    /// <summary>No anche dentro una frase breve: «no grazie», «lascia stare».</summary>
    public static bool ENo(string testo) => Verdetto(testo) < 0;

    static int Verdetto(string testo)
    {
        var parole = Parole(testo);
        if (parole.Count == 0 || parole.Count > 12) return 0;
        var ultimo = 0;
        for (var i = 0; i < parole.Count; i++)
        {
            if (i + 1 < parole.Count && Frase(parole, i, "lascia", "stare", "perdere")) { ultimo = -1; i++; continue; }
            if (i + 1 < parole.Count && Frase(parole, i, "va", "bene")) { ultimo = 1; i++; continue; }
            if (i + 1 < parole.Count && Frase(parole, i, "no", "grazie")) { ultimo = -1; i++; continue; }
            if (i + 1 < parole.Count && (Frase(parole, i, "sì", "grazie") || Frase(parole, i, "si", "grazie"))) { ultimo = 1; i++; continue; }
            if (parole[i] is "no" or "niente" or "annulla") ultimo = -1;
            else if (parole[i] is "sì" or "si" or "certo" or "ok" or "vai" or "fallo" or "dai" or "yes") ultimo = 1;
        }
        return ultimo;
    }

    static bool Frase(List<string> parole, int i, string prima, string seconda, string? secondaAlt = null) =>
        parole[i] == prima && (parole[i + 1] == seconda || (secondaAlt is not null && parole[i + 1] == secondaAlt));

    static List<string> Parole(string testo)
    {
        var lista = new List<string>();
        var cur = new System.Text.StringBuilder();
        foreach (var c in (testo ?? "").ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) cur.Append(c);
            else if (cur.Length > 0) { lista.Add(cur.ToString()); cur.Clear(); }
        }
        if (cur.Length > 0) lista.Add(cur.ToString());
        return lista;
    }

    /// <summary>Chiede qualcosa sul PC (un gesto delle mani, fattibile o no). Le regole stanno in InterpretePc.</summary>
    public static bool ChiedeIlPc(string testo) => InterpretePc.Riguarda(testo);
}
