namespace Liquidino.Nucleo;

public sealed record VoceMondo(string Percorso, string Nome, string Cartella, DateTimeOffset Quando, long Byte);

public sealed record NotaScritta(string Percorso, string Titolo, string Cartella);

/// <summary>Il mondo di Liquidino: note, memoria, cestino. Tutto testo, tutto cancellabile.</summary>
public sealed class Mondo
{
    public static readonly string[] Cartelle = ["note", "storie", "idee", "per-cammo"];
    public const string IntestazioneMemoria = """
        # Cosa so di te

        Liquidino scrive qui solo quello che gli chiedi di ricordare.
        Puoi cancellare le righe, o tutta la pagina: è tua.

        """;

    public string Radice { get; }
    public string RadiceDati { get; }

    public Mondo(string? radiceDati = null)
    {
        RadiceDati = radiceDati ?? Percorsi.RadiceDati();
        Radice = Percorsi.Mondo(RadiceDati);
    }

    public void Prepara()
    {
        foreach (var c in Cartelle)
            Directory.CreateDirectory(Path.Combine(Radice, c));
        Directory.CreateDirectory(Path.Combine(Radice, "cestino"));
        Directory.CreateDirectory(Path.Combine(Radice, "memoria"));
        Directory.CreateDirectory(Path.Combine(Radice, "registro"));
        var mem = PercorsoMemoria();
        if (!File.Exists(mem)) File.WriteAllText(mem, IntestazioneMemoria);
    }

    public string PercorsoMemoria() => Path.Combine(Radice, "memoria", "cosa-so-di-te.md");

    public string LeggiMemoria()
    {
        var p = PercorsoMemoria();
        return File.Exists(p) ? File.ReadAllText(p) : "";
    }

    public NotaScritta CreaNota(string titolo, string testo, string cartella)
    {
        Prepara();
        cartella = Cartelle.Contains(cartella) ? cartella : "note";
        titolo = string.IsNullOrWhiteSpace(titolo) ? "Nota" : titolo.Trim();
        testo = (testo ?? "").Trim();
        if (testo.Length > 4000) testo = testo[..4000];
        var nome = DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + "-" + Testo.NomeFileSicuro(titolo) + ".md";
        var percorso = Path.Combine(Radice, cartella, nome);
        if (!Percorsi.StaDentro(Radice, percorso))
            throw new InvalidOperationException("La nota uscirebbe dal mondo.");
        var corpo = $"# {titolo.Replace("#", "")}\n\n{testo}\n\n_Liquidino, {DateTime.Now:yyyy-MM-dd HH:mm}_\n";
        File.WriteAllText(percorso, corpo);
        return new NotaScritta(percorso, titolo, cartella);
    }

    public void Ricorda(string fatto)
    {
        Prepara();
        fatto = (fatto ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
        if (fatto.Length == 0) return;
        if (fatto.Length > 400) fatto = fatto[..400];
        var riga = $"- {DateTime.Now:yyyy-MM-dd} — {fatto}\n";
        File.AppendAllText(PercorsoMemoria(), riga);
    }

    /// <summary>Toglie le righe che contengono il testo. Non restituisce il contenuto cancellato.</summary>
    public int Dimentica(string cerca)
    {
        cerca = (cerca ?? "").Trim();
        if (cerca.Length < 2) return 0;
        var p = PercorsoMemoria();
        if (!File.Exists(p)) return 0;
        var righe = File.ReadAllLines(p).ToList();
        var tenute = new List<string>();
        var tolte = 0;
        foreach (var r in righe)
        {
            if (r.TrimStart().StartsWith("- ", StringComparison.Ordinal) &&
                r.Contains(cerca, StringComparison.OrdinalIgnoreCase))
            {
                tolte++;
                continue;
            }
            tenute.Add(r);
        }
        if (tolte > 0) File.WriteAllLines(p, tenute);
        return tolte;
    }

    public IReadOnlyList<VoceMondo> Elenca()
    {
        Prepara();
        var voci = new List<VoceMondo>();
        void Aggiungi(string cartella)
        {
            var dir = Path.Combine(Radice, cartella);
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.EnumerateFiles(dir))
            {
                if (!Percorsi.StaDentro(Radice, f)) continue;
                var info = new FileInfo(f);
                voci.Add(new VoceMondo(f, info.Name, cartella, info.LastWriteTime, info.Length));
            }
        }
        foreach (var c in Cartelle) Aggiungi(c);
        Aggiungi("cestino");
        Aggiungi("memoria");
        var reg = Path.Combine(Radice, "registro");
        if (Directory.Exists(reg))
        {
            foreach (var f in Directory.EnumerateFiles(reg))
            {
                var info = new FileInfo(f);
                voci.Add(new VoceMondo(f, info.Name, "registro", info.LastWriteTime, info.Length));
            }
        }
        return voci.OrderByDescending(v => v.Quando).ToList();
    }

    public string? NelCestino(string percorso)
    {
        if (string.IsNullOrWhiteSpace(percorso) || !File.Exists(percorso)) return null;
        if (!Percorsi.StaDentro(Radice, percorso)) return null;
        var cestino = Path.Combine(Radice, "cestino");
        if (Percorsi.StaDentro(cestino, percorso)) return percorso;
        Directory.CreateDirectory(cestino);
        var dest = Path.Combine(cestino, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Path.GetFileName(percorso));
        if (!Percorsi.StaDentro(Radice, dest)) return null;
        File.Move(percorso, dest);
        return dest;
    }

    public long ByteTotali()
    {
        if (!Directory.Exists(Radice)) return 0;
        long n = 0;
        foreach (var f in Directory.EnumerateFiles(Radice, "*", SearchOption.AllDirectories))
        {
            try { n += new FileInfo(f).Length; } catch { /* un file sparito nel frattempo */ }
        }
        return n;
    }
}

public static class Estrattore
{
    public static (string Titolo, string Testo, string Cartella) NotaDaTesto(string messaggio)
    {
        var t = (messaggio ?? "").Trim();
        string[] inizi = ["prendi nota:", "prendi nota", "segnati questa idea:", "segnati:", "segnati", "nota:", "appunta:"];
        foreach (var k in inizi)
        {
            if (t.StartsWith(k, StringComparison.OrdinalIgnoreCase))
            {
                t = t[k.Length..].Trim().TrimStart(':', '-', ' ');
                break;
            }
        }
        if (t.Length == 0) t = (messaggio ?? "").Trim();
        var titolo = t.Length <= 48 ? t : t[..48].Trim();
        if (titolo.Length == 0) titolo = "Nota";
        var cartella = "note";
        var basso = t.ToLowerInvariant();
        if (basso.Contains("storia") || basso.Contains("favola")) cartella = "storie";
        else if (basso.Contains("idea")) cartella = "idee";
        return (titolo, t, cartella);
    }

    public static string FattoDaTesto(string messaggio, bool dimentica)
    {
        var t = (messaggio ?? "").Trim();
        string[] inizi = dimentica
            ? ["dimentica quello che", "dimentica ciò che", "dimentica che", "dimentica:"]
            : ["ricordati che", "ricorda che", "ricordati:", "ricorda:"];
        foreach (var k in inizi)
        {
            if (t.StartsWith(k, StringComparison.OrdinalIgnoreCase))
            {
                t = t[k.Length..].Trim().TrimStart(':', '-', ' ');
                break;
            }
        }
        if (dimentica)
        {
            foreach (var k in new[] { "ti ho detto sul ", "ti ho detto su ", "sul ", "su " })
            {
                var i = t.ToLowerInvariant().IndexOf(k, StringComparison.Ordinal);
                if (i >= 0) { t = t[(i + k.Length)..].Trim(); break; }
            }
        }
        return t.Trim().TrimEnd('.');
    }

    public static bool ProvaParametri(string? json, out Dictionary<string, string> campi)
    {
        campi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return false;
        var s = json.Trim();
        var a = s.IndexOf('{');
        var b = s.LastIndexOf('}');
        if (a < 0 || b <= a) return false;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(s[a..(b + 1)]);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return false;
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                campi[p.Name] = p.Value.ValueKind == System.Text.Json.JsonValueKind.String
                    ? p.Value.GetString() ?? ""
                    : p.Value.ToString();
            }
            return campi.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
