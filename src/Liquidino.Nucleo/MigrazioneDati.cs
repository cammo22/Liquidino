namespace Liquidino.Nucleo;

/// <summary>
/// Copia i dati dalla cartella del programma a Liquidino-dati, una volta.
/// Non sovrascrive ciò che è già al sicuro. Non copia l'eseguibile.
/// Va chiamata all'avvio e prima che Velopack svuoti la cartella (hook OnBeforeUpdate).
/// </summary>
public static class MigrazioneDati
{
    public static readonly string[] Voci =
    [
        "impostazioni.json",
        "diario-tecnico.log",
        "mondo",
        "slot",
        "catalogo",
        "prompt",
        "orecchio",
        "motore"
    ];

    public static void Esegui()
    {
        var destinazione = Percorsi.RadiceDati();
        Porta(Percorsi.RadiceVecchia(), destinazione);
        var app = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Stessa(app, Percorsi.RadiceVecchia()) && !Stessa(app, destinazione))
            Porta(app, destinazione);
    }

    /// <summary>Copia le voci dell'utente. I file già presenti nella destinazione restano.</summary>
    public static void Porta(string origine, string destinazione)
    {
        if (string.IsNullOrWhiteSpace(origine) || string.IsNullOrWhiteSpace(destinazione)) return;
        if (!Directory.Exists(origine)) return;
        if (Stessa(origine, destinazione) || Percorsi.StaDentro(destinazione, origine) || Percorsi.StaDentro(origine, destinazione))
            return;
        foreach (var voce in Voci)
        {
            var da = Path.Combine(origine, voce);
            var a = Path.Combine(destinazione, voce);
            if (File.Exists(da)) CopiaFile(da, a);
            else if (Directory.Exists(da)) CopiaCartella(da, a);
        }
    }

    static void CopiaCartella(string da, string a)
    {
        foreach (var file in Directory.EnumerateFiles(da, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(da, file);
            CopiaFile(file, Path.Combine(a, rel));
        }
    }

    static void CopiaFile(string da, string a)
    {
        if (File.Exists(a)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(a)!);
        File.Copy(da, a);
    }

    static bool Stessa(string a, string b)
    {
        var ca = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), ca);
    }
}
