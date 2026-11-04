namespace Liquidino.Nucleo;

/// <summary>
/// Il testo di «Copia dettagli errore»: versione, sistema, motore, ultimo errore e la coda del diario tecnico.
/// Non contiene la chat, la memoria né le note. Il registro non viene copiato.
/// </summary>
public static class DettagliErrore
{
    public const int RigheDiario = 30;
    public const int MassimoDiario = 3000;

    public static string Componi(string versione, string sistema, string motore, Registro registro, string? radiceDati = null)
    {
        var righe = new List<string>
        {
            "Liquidino " + versione,
            "Sistema: " + sistema,
            "Motore: " + motore,
            "Errori contati: " + registro.Errori,
            "Ultimo errore: " + (string.IsNullOrWhiteSpace(registro.UltimoErrore) ? "nessuno" : registro.UltimoErrore),
            "",
            "Diario tecnico (ultime righe):"
        };
        righe.Add(CodaDiario(Percorsi.DiarioTecnico(radiceDati)));
        return string.Join(Environment.NewLine, righe).TrimEnd() + Environment.NewLine;
    }

    public static string CodaDiario(string percorso)
    {
        try
        {
            if (!File.Exists(percorso)) return "(vuoto)";
            var tutte = File.ReadAllLines(percorso);
            var coda = string.Join(Environment.NewLine, tutte.Skip(Math.Max(0, tutte.Length - RigheDiario))).Trim();
            if (coda.Length > MassimoDiario) coda = coda[^MassimoDiario..];
            return coda.Length == 0 ? "(vuoto)" : coda;
        }
        catch (Exception e)
        {
            return "(non si legge: " + e.Message + ")";
        }
    }
}
