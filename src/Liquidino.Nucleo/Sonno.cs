using System.Text.Json;

namespace Liquidino.Nucleo;

/// <summary>Una dormita: quando è cominciata, quanto vuole dormire e i sogni fatti.</summary>
public sealed class Dormita
{
    public DateTimeOffset Inizio { get; init; }
    public TimeSpan Durata { get; init; }
    public DateTimeOffset Sveglia => Inizio + Durata;
    public List<string> Sogni { get; } = [];
    public string? File { get; set; }

    public TimeSpan Manca(DateTimeOffset ora) => ora >= Sveglia ? TimeSpan.Zero : Sveglia - ora;
    public bool Finita(DateTimeOffset ora) => ora >= Sveglia;
}

/// <summary>
/// Il sonno lo decidi tu: niente orari. Quando lo metti a dormire sceglie lui quanto dormire, in base a quanto è stata piena
/// la giornata, e al massimo due ore. Mentre dorme sogna spesso: ripensa a quello che avete fatto e lo scrive in mondo/sogni.
/// Una dormita arrivata fino alla sveglia conta come completa.
/// </summary>
public static class Sonno
{
    public static readonly TimeSpan Massimo = TimeSpan.FromHours(2);
    public static readonly TimeSpan Minimo = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Dieci minuti, più tre per ogni cosa fatta oggi (frasi, note, pensieri, gesti), entro il massimo che hai scelto (mai oltre due ore).
    /// Un po' di caso, più o meno il 10%, così non è sempre uguale.
    /// </summary>
    public static TimeSpan Durata(Diario.Conti oggi, int minutiMassimi, int seme)
    {
        var massimo = TimeSpan.FromMinutes(Math.Clamp(minutiMassimi, (int)Minimo.TotalMinutes, (int)Massimo.TotalMinutes));
        var fatte = oggi.Turni + oggi.Note + oggi.Pensieri + oggi.Gesti + oggi.Ricordi;
        var minuti = Minimo.TotalMinutes + 3.0 * fatte;
        minuti *= 0.9 + new Random(seme).NextDouble() * 0.2;
        var durata = TimeSpan.FromMinutes(Math.Round(minuti));
        if (durata < Minimo) durata = Minimo;
        return durata > massimo ? massimo : durata;
    }

    public static string Leggibile(TimeSpan t) =>
        t.TotalMinutes < 1 ? "meno di un minuto"
        : t.TotalMinutes < 60 ? (int)Math.Round(t.TotalMinutes) + " minuti"
        : (int)t.TotalHours + (t.Hours == 1 && t.Minutes == 0 ? " ora" : " ore") + (t.Minutes > 0 ? " e " + t.Minutes + " minuti" : "");

    public static string FraseInizio(Dormita d) =>
        "Vado a dormire. Dormo " + Leggibile(d.Durata) + ": mi sveglio alle " + d.Sveglia.ToString("HH:mm") + ". Intanto sogno, e ogni tanto ti dico cosa.";

    public static string FraseFine(Dormita d, DateTimeOffset ora, bool completa, int dormiteComplete)
    {
        var dormito = ora - d.Inizio;
        var sogni = d.Sogni.Count == 0 ? "senza sogni" : d.Sogni.Count == 1 ? "con un sogno" : "con " + d.Sogni.Count + " sogni";
        return completa
            ? "Mi sono svegliato da solo: ho dormito " + Leggibile(dormito) + ", " + sogni + ". Dormite complete: " + dormiteComplete + "."
            : "Mi hai svegliato dopo " + Leggibile(dormito) + ", " + sogni + ". Questa non conta come dormita completa.";
    }

    /// <summary>Le scelte per «sogna ogni»: da 10 secondi a 10 minuti.</summary>
    public static readonly int[] SecondiSogni = [10, 20, 30, 60, 120, 300, 600];

    /// <summary>Le scelte per «un pensiero ogni», da sveglio: da 10 secondi a 2 ore.</summary>
    public static readonly int[] SecondiPensieri = [10, 30, 60, 120, 300, 600, 900, 1800, 3600, 7200];

    public static string Intervallo(int secondi) =>
        secondi < 60 ? secondi + " s" : secondi < 3600 ? secondi / 60 + " min" : secondi / 3600 + (secondi == 3600 ? " ora" : " ore");

    /// <summary>Il file dei sogni di questa dormita, in mondo/sogni.</summary>
    public static string ScriviSogno(Mondo mondo, Dormita d, string sogno, DateTimeOffset ora)
    {
        var cartella = Path.Combine(mondo.Radice, "sogni");
        Directory.CreateDirectory(cartella);
        d.File ??= Path.Combine(cartella, d.Inizio.ToString("yyyy-MM-dd-HHmm") + ".md");
        if (!File.Exists(d.File))
            File.WriteAllText(d.File, "# Sogni del " + d.Inizio.ToString("dd/MM/yyyy HH:mm") + "\n\n");
        File.AppendAllText(d.File, "- " + ora.ToString("HH:mm:ss") + " " + sogno.Replace('\n', ' ').Trim() + "\n");
        d.Sogni.Add(sogno);
        return d.File;
    }

    /// <summary>Un sogno di riserva, quando non c'è un modello. Breve e vero: non finge di aver pensato.</summary>
    public static string SognoDiRiserva(int n) => (n % 4) switch
    {
        0 => "Zzz… sogno una cassetta che gira piano.",
        1 => "Zzz… nel sogno metto in ordine le note di oggi.",
        2 => "Zzz… sogno di sentirti chiamare il mio nome.",
        _ => "Zzz… sogno luci accese e spente, una alla volta."
    };
}

/// <summary>Un 👍 o un 👎 su qualcosa che ha scritto o fatto. Lui non risponde: lo tiene per allenarsi.</summary>
public sealed record Voto(DateTimeOffset Quando, string Fonte, string Testo, bool Piace);

/// <summary>
/// La palestra: i tuoi 👍 e 👎, in palestra/voti.jsonl. Servono per l'allenamento di notte (KTO su LFM3-1.7B),
/// che arriverà in una versione dopo. Lui non risponde ai voti e non li commenta.
/// </summary>
public static class Palestra
{
    public static string File(string? radiceDati = null) => Path.Combine(radiceDati ?? Percorsi.RadiceDati(), "palestra", "voti.jsonl");

    static readonly object Uno = new();

    public static void Segna(Voto voto, string? radiceDati = null)
    {
        var file = File(radiceDati);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var testo = voto.Testo.Length > 2000 ? voto.Testo[..2000] : voto.Testo;
        var riga = JsonSerializer.Serialize(new { quando = voto.Quando, fonte = voto.Fonte, testo, piace = voto.Piace });
        lock (Uno) System.IO.File.AppendAllText(file, riga + "\n");
    }

    public static (int Piace, int NonPiace) Conta(string? radiceDati = null)
    {
        var file = File(radiceDati);
        if (!System.IO.File.Exists(file)) return (0, 0);
        int si = 0, no = 0;
        foreach (var riga in System.IO.File.ReadLines(file))
        {
            try
            {
                using var doc = JsonDocument.Parse(riga);
                if (doc.RootElement.GetProperty("piace").GetBoolean()) si++; else no++;
            }
            catch { /* riga rovinata: si salta */ }
        }
        return (si, no);
    }
}
