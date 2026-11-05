using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Liquidino.Nucleo;

/// <summary>Filtri e ricerca del pannello del mondo.</summary>
public static class FiltroMondo
{
    public const string Tutte = "tutte";
    public const long MassimoPerCercareDentro = 64 * 1024;

    /// <summary>Le cartelle che il pannello sa mostrare, nell'ordine del menu.</summary>
    public static readonly string[] Scelte = [Tutte, "note", "storie", "idee", "per-cammo", "diario", "memoria", "cestino", "registro"];

    /// <summary>
    /// Tiene le voci della cartella scelta che contengono la parola nel nome o, per i file di testo piccoli, dentro.
    /// Il registro si cerca solo per nome: dentro ci sono i conti, non le creazioni.
    /// </summary>
    public static IReadOnlyList<VoceMondo> Applica(IReadOnlyList<VoceMondo> voci, string? cartella, string? cerca)
    {
        var scelta = string.IsNullOrWhiteSpace(cartella) ? Tutte : cartella.Trim();
        var parola = InterpretePc.Normalizza(cerca);
        return voci
            .Where(v => scelta == Tutte || string.Equals(v.Cartella, scelta, StringComparison.OrdinalIgnoreCase))
            .Where(v => parola.Length == 0 || Trovata(v, parola))
            .ToList();
    }

    static bool Trovata(VoceMondo v, string parola)
    {
        if (InterpretePc.Normalizza(v.Nome).Contains(parola, StringComparison.Ordinal)) return true;
        if (v.Cartella == "registro" || v.Byte > MassimoPerCercareDentro || !Mondo.ETesto(v.Percorso)) return false;
        try { return InterpretePc.Normalizza(File.ReadAllText(v.Percorso)).Contains(parola, StringComparison.Ordinal); }
        catch { return false; }
    }
}

/// <summary>Il tuo voto sulle creazioni: 👍 o 👎. Sta in mondo/voti.json, si può cancellare.</summary>
public sealed class VotiMondo
{
    readonly Mondo _mondo;
    readonly object _lucchetto = new();

    public VotiMondo(Mondo mondo) => _mondo = mondo;

    public string Percorso => Path.Combine(_mondo.Radice, "voti.json");

    public static bool SiVota(VoceMondo voce) => Mondo.Cartelle.Contains(voce.Cartella) || voce.Cartella == "diario";

    public int? Voto(string percorsoFile)
    {
        var chiave = Chiave(percorsoFile);
        if (chiave is null) return null;
        lock (_lucchetto) return Leggi().TryGetValue(chiave, out var v) ? v : null;
    }

    /// <summary>+1 piace, -1 non piace, 0 toglie il voto. Fuori dal mondo non si vota.</summary>
    public bool Vota(string percorsoFile, int voto)
    {
        var chiave = Chiave(percorsoFile);
        if (chiave is null || !File.Exists(percorsoFile)) return false;
        lock (_lucchetto)
        {
            var tutti = Leggi();
            if (voto == 0) tutti.Remove(chiave);
            else tutti[chiave] = Math.Sign(voto);
            Scrivi(tutti);
        }
        return true;
    }

    /// <summary>Le creazioni che ti sono piaciute, le più recenti prima. Solo quelle che esistono ancora.</summary>
    public IReadOnlyList<string> Piaciute(int quante)
    {
        Dictionary<string, int> tutti;
        lock (_lucchetto) tutti = Leggi();
        return tutti
            .Where(t => t.Value > 0)
            .Select(t => Path.Combine(_mondo.Radice, t.Key.Replace('/', Path.DirectorySeparatorChar)))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(quante)
            .ToList();
    }

    string? Chiave(string percorsoFile)
    {
        if (string.IsNullOrWhiteSpace(percorsoFile) || !Percorsi.StaDentro(_mondo.Radice, percorsoFile)) return null;
        var rel = Path.GetRelativePath(_mondo.Radice, percorsoFile).Replace('\\', '/');
        return rel.StartsWith("..", StringComparison.Ordinal) ? null : rel;
    }

    Dictionary<string, int> Leggi()
    {
        try
        {
            if (!File.Exists(Percorso)) return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var letto = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(Percorso));
            return letto is null
                ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(letto, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    void Scrivi(Dictionary<string, int> tutti)
    {
        Directory.CreateDirectory(_mondo.Radice);
        var temporaneo = Percorso + ".tmp";
        File.WriteAllText(temporaneo, JsonSerializer.Serialize(tutti, JsonIo.Opzioni));
        File.Move(temporaneo, Percorso, overwrite: true);
    }
}

/// <summary>
/// Il diario della giornata, in mondo/diario. Solo i conti e i titoli dei file creati quel giorno:
/// la chat e la memoria non ci finiscono, come nel registro.
/// </summary>
public static class Diario
{
    public sealed record Conti(int Turni, int Note, int Pensieri, int Ricordi, int Dimenticati, int Gesti, int Errori, int Rifatti);

    /// <summary>Di notte, prima della fine della notte, il giorno da raccontare è ancora quello di ieri.</summary>
    public static DateOnly GiornoDaRaccontare(DateTimeOffset ora, int oraFineNotte)
    {
        var oggi = DateOnly.FromDateTime(ora.DateTime);
        return ora.Hour < Math.Clamp(oraFineNotte, 0, 23) ? oggi.AddDays(-1) : oggi;
    }

    public static string PercorsoDi(Mondo mondo, DateOnly giorno) =>
        Path.Combine(mondo.Radice, "diario", giorno.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".md");

    public static Conti Conta(string radiceDati, DateOnly giorno)
    {
        var file = Path.Combine(Percorsi.Registro(radiceDati), giorno.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl");
        int turni = 0, note = 0, pensieri = 0, ricordi = 0, dimenticati = 0, gesti = 0, errori = 0, rifatti = 0;
        if (File.Exists(file))
        {
            foreach (var riga in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(riga)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(riga);
                    var r = doc.RootElement;
                    var tipo = Testo(r, "tipo");
                    var azione = Testo(r, "azione");
                    var esito = Testo(r, "esito");
                    var ok = esito == "ok";
                    switch (tipo)
                    {
                        case "turno": turni++; break;
                        case "azione" when azione == "crea_nota" && ok: note++; break;
                        case "ciclo" when ok && azione == "crea_nota": pensieri++; break;
                        case "ciclo" when esito == "rifatto": rifatti++; break;
                        case "memoria" when azione == "ricorda" && ok: ricordi++; break;
                        case "memoria" when azione == "dimentica" && ok: dimenticati++; break;
                        case "pc" when ok: gesti++; break;
                        case "errore": errori++; break;
                    }
                }
                catch { /* una riga rovinata non ferma il diario */ }
            }
        }
        return new Conti(turni, note, pensieri, ricordi, dimenticati, gesti, errori, rifatti);
    }

    /// <summary>Scrive (o riscrive) il diario del giorno. Ritorna il percorso.</summary>
    public static string Scrivi(Mondo mondo, DateOnly giorno, DateTimeOffset adesso)
    {
        mondo.Prepara();
        var conti = Conta(mondo.RadiceDati, giorno);
        var it = CultureInfo.GetCultureInfo("it-IT");
        var prefisso = giorno.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "-";
        var creati = mondo.Elenca()
            .Where(v => Mondo.Cartelle.Contains(v.Cartella) && v.Nome.StartsWith(prefisso, StringComparison.Ordinal))
            .OrderBy(v => v.Nome, StringComparer.Ordinal)
            .Select(v => "- " + v.Cartella + ": " + Mondo.TitoloDi(v.Nome))
            .ToList();

        var righe = new List<string>
        {
            "# Diario di " + giorno.ToString("dddd d MMMM yyyy", it),
            "",
            "- Abbiamo parlato " + Volte(conti.Turni) + ".",
            "- Note scritte: " + conti.Note + ". Pensieri: " + conti.Pensieri + ". Rifatti: " + conti.Rifatti + ".",
            "- Ricordi aggiunti: " + conti.Ricordi + ", tolti: " + conti.Dimenticati + ".",
            "- Gesti sul PC: " + conti.Gesti + ". Errori: " + conti.Errori + ".",
            "",
            "## Cosa ho creato"
        };
        righe.AddRange(creati.Count == 0 ? ["- Niente, oggi."] : creati);
        righe.Add("");
        righe.Add("_Liquidino, " + adesso.ToString("dd/MM HH:mm", it) + ". Qui ci sono i conti della giornata e i titoli di quello che ho creato: le nostre chiacchiere no._");
        var percorso = PercorsoDi(mondo, giorno);
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        File.WriteAllText(percorso, string.Join("\n", righe) + "\n");
        return percorso;
    }

    static string Volte(int n) => n switch
    {
        0 => "zero volte",
        1 => "una volta",
        _ => n + " volte"
    };

    static string? Testo(JsonElement r, string nome) =>
        r.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

public static partial class NomiMondo
{
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}-\d{6}-(.+)\.md$")]
    internal static partial Regex NotaConData();
}
