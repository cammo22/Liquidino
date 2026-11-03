using System.Text.Json;

namespace Liquidino.Nucleo;

/// <summary>Registro di tutto, una riga JSON per azione. La cancellazione non contiene il testo cancellato.</summary>
public sealed class Registro
{
    readonly string _radiceDati;
    readonly object _lucchetto = new();
    public int Errori { get; private set; }
    public string? UltimoErrore { get; private set; }
    DateOnly? _ultimoAvviso;

    public Registro(string? radiceDati = null)
    {
        _radiceDati = radiceDati ?? Percorsi.RadiceDati();
        LeggiErrori();
    }

    public void Scrivi(string tipo, string dettaglio, string esito = "ok", string? azione = null, double? sicurezza = null)
    {
        var riga = JsonSerializer.Serialize(new
        {
            ora = DateTimeOffset.Now.ToString("o"),
            tipo,
            azione,
            esito,
            dettaglio,
            sicurezza
        }, JsonIo.Riga);
        lock (_lucchetto)
        {
            var dir = Percorsi.Registro(_radiceDati);
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, DateTime.Now.ToString("yyyy-MM-dd") + ".jsonl"), riga + "\n");
        }
    }

    /// <summary>Aumenta il conteggio. Dopo 12 errori, una volta al giorno, si può dire della possibile evoluzione.</summary>
    public bool SegnaErrore(string motivo)
    {
        motivo = (motivo ?? "errore").Replace('\n', ' ').Trim();
        if (motivo.Length > 240) motivo = motivo[..240];
        bool avvisa;
        lock (_lucchetto)
        {
            Errori++;
            UltimoErrore = motivo;
            var oggi = DateOnly.FromDateTime(DateTime.Now);
            avvisa = Errori >= 12 && _ultimoAvviso != oggi;
            if (avvisa) _ultimoAvviso = oggi;
            SalvaErrori();
        }
        Scrivi("errore", motivo, "errore");
        return avvisa;
    }

    public const string FraseEvoluzione =
        "Sto sbagliando spesso. Più avanti posso proporti di evolvermi, con un piccolo addestramento, e terrei da parte la versione di prima. Da solo non lo faccio.";

    void LeggiErrori()
    {
        var p = PercorsoErrori();
        if (!File.Exists(p)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(p));
            var r = doc.RootElement;
            if (r.TryGetProperty("totale", out var t) && t.TryGetInt32(out var n)) Errori = n;
            if (r.TryGetProperty("ultimo", out var u)) UltimoErrore = u.GetString();
            if (r.TryGetProperty("ultimoAvviso", out var a) && DateOnly.TryParse(a.GetString(), out var d)) _ultimoAvviso = d;
        }
        catch { /* il conteggio riparte, il file si riscrive */ }
    }

    void SalvaErrori()
    {
        var p = PercorsoErrori();
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        var json = JsonSerializer.Serialize(new
        {
            totale = Errori,
            ultimo = UltimoErrore,
            ultimoAvviso = _ultimoAvviso?.ToString("yyyy-MM-dd")
        }, JsonIo.Opzioni);
        File.WriteAllText(p, json);
    }

    string PercorsoErrori() => Path.Combine(Percorsi.Registro(_radiceDati), "errori.json");
}
