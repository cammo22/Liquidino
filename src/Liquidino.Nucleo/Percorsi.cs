namespace Liquidino.Nucleo;

/// <summary>
/// I dati stanno fuori dalla cartella del programma: Velopack la svuota a ogni aggiornamento.
/// Il mondo è in %LOCALAPPDATA%\Liquidino-dati\mondo. I file di serie (prompt, catalogo) restano nell'app;
/// le copie che puoi modificare stanno nella cartella dati.
/// </summary>
public static class Percorsi
{
    public const string CartellaProvaCammo = @"C:\Users\cammo\LiquidinoProva";
    public const string NomeDati = "Liquidino-dati";
    public const string NomeInstallazioneVecchia = "Liquidino";

    public static string CartellaLocale()
    {
        var locale = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(locale))
            locale = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return locale;
    }

    static string? _radiceForzata;

    /// <summary>Solo per la prova di avvio: tutti i dati in una cartella a parte, lontano da quelli veri.</summary>
    public static void UsaRadice(string? radice) => _radiceForzata = string.IsNullOrWhiteSpace(radice) ? null : Path.GetFullPath(radice);

    public static string RadiceDati() => _radiceForzata ?? Path.Combine(CartellaLocale(), NomeDati);

    /// <summary>Dove la 0.2.0 teneva i dati, cioè dentro la cartella che Velopack cancella.</summary>
    public static string RadiceVecchia() => Path.Combine(CartellaLocale(), NomeInstallazioneVecchia);

    public static string Mondo(string? radice = null) => Path.Combine(radice ?? RadiceDati(), "mondo");
    public static string FileImpostazioni(string? radice = null) => Path.Combine(radice ?? RadiceDati(), "impostazioni.json");
    public static string Prompt(string? radice = null) => Path.Combine(radice ?? RadiceDati(), "prompt", "sistema.md");
    public static string Catalogo(string? radice = null) => Path.Combine(radice ?? RadiceDati(), "catalogo", "azioni.json");
    public static string Slot(string? radice = null) => Path.Combine(radice ?? RadiceDati(), "slot");
    public static string Preset(string? radice = null) => Path.Combine(radice ?? RadiceDati(), "motore", "modelli.ini");
    public static string Orecchio(string? radice = null) => Path.Combine(radice ?? RadiceDati(), "orecchio");
    public static string Registro(string? radice = null) => Path.Combine(Mondo(radice), "registro");
    public static string DiarioTecnico(string? radice = null) => Path.Combine(radice ?? RadiceDati(), "diario-tecnico.log");

    /// <summary>Vero se il percorso, una volta risolto, sta dentro la radice.</summary>
    public static bool StaDentro(string radice, string percorso)
    {
        var basePiena = Path.GetFullPath(radice);
        var pieno = Path.GetFullPath(percorso);
        var confronto = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(pieno, basePiena, confronto)) return true;
        var prefisso = basePiena.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return pieno.StartsWith(prefisso, confronto);
    }
}
