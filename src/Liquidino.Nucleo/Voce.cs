namespace Liquidino.Nucleo;

public enum MotoreVoce
{
    Nessuno,
    Omni,
    Windows
}

/// <summary>
/// Chi legge le risposte. Prima Omni (/tts) se c'è il motore. Se manca, o se la sua voce non esce,
/// le voci italiane già installate in Windows. Niente si scarica: se non ce n'è nessuna, resta il testo.
/// </summary>
public static class SceltaVoce
{
    public static MotoreVoce Scegli(bool motorePresente, bool omniPresente, bool windowsAccesa, bool windowsDisponibile)
    {
        if (motorePresente && omniPresente) return MotoreVoce.Omni;
        return windowsAccesa && windowsDisponibile ? MotoreVoce.Windows : MotoreVoce.Nessuno;
    }

    /// <summary>Dopo un errore di Omni si prova Windows, se c'è e se l'hai lasciata accesa.</summary>
    public static MotoreVoce DopoErrore(bool windowsAccesa, bool windowsDisponibile) =>
        windowsAccesa && windowsDisponibile ? MotoreVoce.Windows : MotoreVoce.Nessuno;

    /// <summary>female o male, come per /tts. Se la voce di quel genere non c'è, va bene l'altra.</summary>
    public static T? Preferita<T>(IReadOnlyList<T> voci, Func<T, bool> eFemminile, string voce) where T : class
    {
        if (voci.Count == 0) return null;
        var vuoleDonna = voce != "male";
        return voci.FirstOrDefault(v => eFemminile(v) == vuoleDonna) ?? voci[0];
    }
}
