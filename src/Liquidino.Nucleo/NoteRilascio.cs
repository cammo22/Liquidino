namespace Liquidino.Nucleo;

/// <summary>Le note di una versione, prese dal CHANGELOG. Tre voci, sempre: novità, correzioni, da provare.</summary>
public static class NoteRilascio
{
    public const string Novita = "### Novità";
    public const string Correzioni = "### Correzioni";
    public const string DaProvare = "### Da provare";

    public static string Estrai(string changelog, string versione)
    {
        var righe = (changelog ?? "").Replace("\r\n", "\n").Split('\n');
        var dentro = false;
        var prese = new List<string>();
        foreach (var riga in righe)
        {
            if (riga.StartsWith("## "))
            {
                if (dentro) break;
                dentro = riga.Contains(versione, StringComparison.Ordinal);
                continue;
            }
            if (dentro) prese.Add(riga);
        }
        var testo = string.Join("\n", prese).Trim();
        if (testo.Length == 0)
            testo = Novita + "\n\n- Liquidino " + versione + ".\n\n" + Correzioni + "\n\n- Nessuna.\n\n" + DaProvare + "\n\n- Aprilo e guarda che parta.\n";
        return testo.Trim() + "\n";
    }

    public static bool HaLeTreVoci(string note) =>
        note.Contains(Novita, StringComparison.Ordinal)
        && note.Contains(Correzioni, StringComparison.Ordinal)
        && note.Contains(DaProvare, StringComparison.Ordinal);
}
