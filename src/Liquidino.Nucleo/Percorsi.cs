namespace Liquidino.Nucleo;

/// <summary>
/// Cartelle di Liquidino. Il suo mondo sta in %LOCALAPPDATA%\Liquidino\mondo
/// e da lì non esce, se non glielo chiedi tu a mano.
/// </summary>
public static class Percorsi
{
    public const string CartellaProvaCammo = @"C:\Users\cammo\LiquidinoProva";

    public static string RadiceDati()
    {
        var locale = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(locale))
            locale = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return Path.Combine(locale, "Liquidino");
    }

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
