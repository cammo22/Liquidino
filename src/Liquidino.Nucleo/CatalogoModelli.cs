using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace Liquidino.Nucleo;

/// <summary>Un modello che Liquidino conosce: a cosa serve e da quale repository di Hugging Face arriva.</summary>
public sealed record SchedaCatalogo(RuoloModello Ruolo, string Nome, string APerCosa, string Repo, bool NelGiro)
{
    /// <summary>La cartella del modello dentro quella dei modelli: un modello, una cartella.</summary>
    public string Cartella(string cartellaModelli) => Path.Combine(cartellaModelli, Nome);
}

/// <summary>
/// Un file di un repository, come lo descrive Hugging Face: percorso, peso e impronta.
/// I file grandi (LFS) hanno lo SHA-256; quelli piccoli solo l'impronta git (SHA-1 del blob).
/// </summary>
public sealed record FileRepo(string Percorso, long Byte, string? Sha256, string? ShaGit = null)
{
    public bool HaImpronta => Sha256 is not null || ShaGit is not null;
}

/// <summary>Un modello fatto di file fissi (per esempio Parakeet: encoder, decoder, joiner e tokens), tutti nella sua cartella.</summary>
public sealed record SchedaFissa(string Nome, string APerCosa, string Repo, string NomeCartella, IReadOnlyList<string> File)
{
    public string Cartella(string cartellaModelli) => Path.Combine(cartellaModelli, NomeCartella);
    public bool Presente(string cartellaModelli) => File.All(f => new FileInfo(Path.Combine(Cartella(cartellaModelli), f)) is { Exists: true, Length: > 0 });
}

/// <summary>I file di un modello fisso alla revisione scelta, con il loro peso e la loro impronta.</summary>
public sealed record SceltaFissa(SchedaFissa Scheda, string Revisione, IReadOnlyList<FileRepo> File)
{
    public long Totale => File.Sum(f => f.Byte);
}

/// <summary>Cosa si scarica per un modello: i pesi, il proiettore per vedere (se c'è) e la revisione esatta.</summary>
public sealed record SceltaDownload(SchedaCatalogo Scheda, string Revisione, FileRepo Pesi, FileRepo? Proiettore)
{
    public long Totale => Pesi.Byte + (Proiettore?.Byte ?? 0);

    /// <summary>I nomi con cui i file si salvano: li capisce il rilevatore e il proiettore si accoppia ai pesi.</summary>
    public string NomePesi => Scheda.Nome + "-" + CatalogoModelli.Quant(Pesi.Percorso) + ".gguf";
    public string? NomeProiettore => Proiettore is null ? null : "mmproj-" + Scheda.Nome + "-" + CatalogoModelli.Quant(Proiettore.Percorso) + ".gguf";
}

public sealed record Avanzamento(string File, long Fatti, long Totale);

/// <summary>
/// Il catalogo dei modelli LFM3 e la scelta dei file da scaricare. Nessun download parte da solo: solo dal tasto Scarica,
/// con la revisione fissata al momento del clic e l'impronta SHA-256 che Hugging Face dichiara per ogni file.
/// </summary>
public static class CatalogoModelli
{
    public static readonly IReadOnlyList<SchedaCatalogo> Tutti =
    [
        new(RuoloModello.Mani, "LFM3-1.7B",
            "Le mani. Scrive le note, compila le azioni e riscrive i pensieri. È quello che regge il contesto lungo, fino a 128K. Se Omni non c'è, chiacchiera lui.",
            "LiquidAI/LFM3-1.7B-GGUF", true),
        new(RuoloModello.Omni, "LFM3-Omni-2B",
            "La voce. Chiacchiera, trascrive quello che dici, legge le risposte ad alta voce e guarda immagini e schermo.",
            "LiquidAI/LFM3-Omni-2B-GGUF", true),
        new(RuoloModello.D1, "d1-2B",
            "La scelta. Decide quale azione fare e quanto ne è sicuro: risponde con probabilità, non con frasi. Sotto 0,6 ti chiede conferma.",
            "LiquidAI/d1-2B-GGUF", true),
        new(RuoloModello.Vl, "LFM3-VL-2B",
            "Gli occhi. Guarda immagini e schermo quando Omni non c'è.",
            "LiquidAI/LFM3-VL-2B-GGUF", true),
        new(RuoloModello.Audio, "LFM3-Audio-2B",
            "L'audio. Il regista lo riconosce e lo sa caricare, ma non è ancora nel giro di tutti i giorni.",
            "LiquidAI/LFM3-Audio-2B-GGUF", false),
        new(RuoloModello.D1Omni, "d1-Omni-2B",
            "La scelta sulla voce: decide direttamente da quello che senti. Il regista lo riconosce, ma non è ancora nel giro di tutti i giorni.",
            "LiquidAI/d1-Omni-2B-GGUF", false)
    ];

    /// <summary>Il quant preferito per i pesi: leggero sulla 4060 senza perdere troppo. Poi gli altri, in ordine.</summary>
    public static readonly string[] QuantPesi = ["Q4_K_M", "Q5_K_M", "Q6_K", "Q8_0", "Q4_0"];

    /// <summary>Il proiettore per le immagini e l'audio si tiene più preciso: pesa poco.</summary>
    public static readonly string[] QuantProiettore = ["Q8_0", "F16", "BF16", "F32"];

    public const string Sito = "https://huggingface.co/";

    public static SchedaCatalogo? Di(RuoloModello ruolo) => Tutti.FirstOrDefault(s => s.Ruolo == ruolo);

    /// <summary>La parte del nome che dice il quant, come Q4_K_M, o "gguf" se non si capisce.</summary>
    public static string Quant(string percorso)
    {
        var nome = Path.GetFileNameWithoutExtension(percorso ?? "").ToUpperInvariant();
        foreach (var q in QuantPesi.Concat(QuantProiettore).Concat(["Q5_K_S", "Q4_K_S", "Q3_K_M", "IQ4_XS", "NVFP4"]))
            if (nome.EndsWith("-" + q, StringComparison.Ordinal) || nome.EndsWith("." + q, StringComparison.Ordinal) || nome.EndsWith("_" + q, StringComparison.Ordinal))
                return q;
        return "gguf";
    }

    /// <summary>
    /// Sceglie dall'elenco del repository i pesi (il primo quant della lista che c'è) e, se c'è, il proiettore.
    /// File divisi in più parti e file senza impronta non si prendono: non si potrebbero controllare.
    /// </summary>
    public static SceltaDownload? Scegli(SchedaCatalogo scheda, string revisione, IReadOnlyList<FileRepo> file)
    {
        var gguf = file
            .Where(f => f.Percorso.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Percorso.Contains("-of-", StringComparison.OrdinalIgnoreCase))
            .Where(f => !string.IsNullOrWhiteSpace(f.Sha256) && f.Byte > 0)
            .ToList();
        static bool EProiettore(FileRepo f) => Path.GetFileName(f.Percorso).Contains("mmproj", StringComparison.OrdinalIgnoreCase);
        var pesi = PrimoPer(gguf.Where(f => !EProiettore(f)).ToList(), QuantPesi);
        if (pesi is null) return null;
        var proiettore = PrimoPer(gguf.Where(EProiettore).ToList(), QuantProiettore)
            ?? gguf.Where(EProiettore).OrderBy(f => f.Byte).FirstOrDefault();
        return new SceltaDownload(scheda, revisione, pesi, proiettore);
    }

    static FileRepo? PrimoPer(IReadOnlyList<FileRepo> file, string[] ordine)
    {
        foreach (var q in ordine)
        {
            var trovato = file.Where(f => Quant(f.Percorso) == q).OrderBy(f => f.Percorso.Count(c => c == '/')).FirstOrDefault();
            if (trovato is not null) return trovato;
        }
        return null;
    }

    /// <summary>L'elenco dei file da /api/models/{repo}/tree/{revisione}?recursive=true.</summary>
    public static IReadOnlyList<FileRepo> LeggiElenco(string json)
    {
        var lista = new List<FileRepo>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return lista;
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            if (!e.TryGetProperty("type", out var t) || t.GetString() != "file") continue;
            var percorso = e.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
            long size = e.TryGetProperty("size", out var s) && s.TryGetInt64(out var n) ? n : 0;
            string? sha = null;
            string? git = null;
            if (e.TryGetProperty("lfs", out var lfs) && lfs.ValueKind == JsonValueKind.Object)
            {
                if (lfs.TryGetProperty("oid", out var oid)) sha = oid.GetString();
                if (lfs.TryGetProperty("size", out var ls) && ls.TryGetInt64(out var lsize)) size = lsize;
            }
            else if (e.TryGetProperty("oid", out var blob)) git = blob.GetString();
            if (sha is not null && !EImpronta(sha)) sha = null;
            if (git is not null && !(git.Length == 40 && git.All(Uri.IsHexDigit))) git = null;
            if (percorso.Length > 0) lista.Add(new FileRepo(percorso, size, sha?.ToLowerInvariant(), git?.ToLowerInvariant()));
        }
        return lista;
    }

    /// <summary>La revisione (commit) da /api/models/{repo}.</summary>
    public static string? LeggiRevisione(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("sha", out var sha) && sha.GetString() is { } s && s.Length >= 7 && s.All(Uri.IsHexDigit) ? s : null;
    }

    static bool EImpronta(string sha) => sha.Length == 64 && sha.All(Uri.IsHexDigit);

    /// <summary>Un percorso di repository sicuro da mettere in un indirizzo: niente risalite, niente indirizzi assoluti.</summary>
    public static bool PercorsoSicuro(string percorso) =>
        percorso.Length > 0 && !percorso.StartsWith('/') && !percorso.Contains("..", StringComparison.Ordinal)
        && !percorso.Contains('\\') && !percorso.Contains(':');

    public static string IndirizzoFile(string repo, string revisione, string percorso) =>
        Sito + repo + "/resolve/" + revisione + "/" + string.Join('/', percorso.Split('/').Select(Uri.EscapeDataString));

    /// <summary>I file fissi alla revisione: null se ne manca uno o se uno non ha l'impronta.</summary>
    public static SceltaFissa? ScegliFissi(SchedaFissa scheda, string revisione, IReadOnlyList<FileRepo> file)
    {
        var scelti = new List<FileRepo>();
        foreach (var nome in scheda.File)
        {
            var f = file.FirstOrDefault(x => x.Percorso == nome);
            if (f is null || !f.HaImpronta || f.Byte <= 0) return null;
            scelti.Add(f);
        }
        return new SceltaFissa(scheda, revisione, scelti);
    }

    /// <summary>I file di un modello installato: pesi e proiettore. Si cancellano solo se stanno dentro la cartella dei modelli.</summary>
    public static IReadOnlyList<string> FileDa(SchedaModello installato, string cartellaModelli)
    {
        var file = new List<string> { installato.Percorso };
        if (!string.IsNullOrWhiteSpace(installato.Mmproj)) file.Add(installato.Mmproj);
        return file.Where(f => Percorsi.StaDentro(cartellaModelli, f)).ToList();
    }

    public static string Peso(long byte_)
    {
        if (byte_ <= 0) return "?";
        var gb = byte_ / 1024d / 1024d / 1024d;
        return gb >= 1 ? gb.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("it-IT")) + " GB"
            : (byte_ / 1024d / 1024d).ToString("0", System.Globalization.CultureInfo.GetCultureInfo("it-IT")) + " MB";
    }
}

/// <summary>
/// Scarica un modello da Hugging Face nella sua cartella. Prima un file .part, poi il controllo SHA-256, poi il nome vero:
/// un file a metà o diverso da quello dichiarato non resta mai fra i modelli.
/// </summary>
public sealed class ScaricatoreModelli : IDisposable
{
    readonly HttpClient _http;

    public ScaricatoreModelli(HttpMessageHandler? gestore = null)
    {
        _http = new HttpClient(gestore ?? new SocketsHttpHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 10 })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Liquidino", "1"));
    }

    /// <summary>Legge revisione ed elenco dei file e sceglie cosa scaricare. Null se nel repository non c'è un GGUF adatto.</summary>
    public async Task<SceltaDownload?> Prepara(SchedaCatalogo scheda, CancellationToken ct)
    {
        using var tempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tempo.CancelAfter(TimeSpan.FromSeconds(30));
        var info = await _http.GetStringAsync(CatalogoModelli.Sito + "api/models/" + scheda.Repo, tempo.Token).ConfigureAwait(false);
        var revisione = CatalogoModelli.LeggiRevisione(info) ?? throw new InvalidOperationException("Hugging Face non dice la revisione di " + scheda.Repo + ".");
        var elenco = await _http.GetStringAsync(CatalogoModelli.Sito + "api/models/" + scheda.Repo + "/tree/" + revisione + "?recursive=true", tempo.Token).ConfigureAwait(false);
        return CatalogoModelli.Scegli(scheda, revisione, CatalogoModelli.LeggiElenco(elenco));
    }

    /// <summary>Come Prepara, per un modello fatto di file fissi. Null se un file manca o non ha impronta.</summary>
    public async Task<SceltaFissa?> Prepara(SchedaFissa scheda, CancellationToken ct)
    {
        using var tempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tempo.CancelAfter(TimeSpan.FromSeconds(30));
        var info = await _http.GetStringAsync(CatalogoModelli.Sito + "api/models/" + scheda.Repo, tempo.Token).ConfigureAwait(false);
        var revisione = CatalogoModelli.LeggiRevisione(info) ?? throw new InvalidOperationException("Hugging Face non dice la revisione di " + scheda.Repo + ".");
        var elenco = await _http.GetStringAsync(CatalogoModelli.Sito + "api/models/" + scheda.Repo + "/tree/" + revisione + "?recursive=true", tempo.Token).ConfigureAwait(false);
        return CatalogoModelli.ScegliFissi(scheda, revisione, CatalogoModelli.LeggiElenco(elenco));
    }

    /// <summary>Scarica i file fissi nella cartella del modello, ognuno controllato con la sua impronta.</summary>
    public async Task<IReadOnlyList<string>> Scarica(SceltaFissa scelta, string cartellaModelli, IProgress<Avanzamento>? avanzamento, CancellationToken ct)
    {
        var cartella = scelta.Scheda.Cartella(cartellaModelli);
        if (!Percorsi.StaDentro(cartellaModelli, cartella)) throw new InvalidOperationException("La cartella del modello uscirebbe da quella dei modelli.");
        Directory.CreateDirectory(cartella);
        ControllaSpazio(cartella, scelta.Totale);
        var salvati = new List<string>();
        foreach (var f in scelta.File)
        {
            var destinazione = Path.Combine(cartella, Path.GetFileName(f.Percorso));
            if (!Percorsi.StaDentro(cartella, destinazione)) throw new InvalidOperationException("Il file " + f.Percorso + " uscirebbe dalla cartella del modello.");
            salvati.Add(await UnFile(scelta.Scheda.Repo, scelta.Revisione, f, destinazione, avanzamento, ct).ConfigureAwait(false));
        }
        return salvati;
    }

    static void ControllaSpazio(string cartella, long totale)
    {
        var libero = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(cartella))!).AvailableFreeSpace;
        if (libero < totale + 512L * 1024 * 1024)
            throw new InvalidOperationException("sul disco servono " + CatalogoModelli.Peso(totale) + " liberi più un margine, e ce ne sono " + CatalogoModelli.Peso(libero) + ".");
    }

    /// <summary>Scarica pesi e proiettore nella cartella del modello. Ritorna i percorsi salvati.</summary>
    public async Task<IReadOnlyList<string>> Scarica(SceltaDownload scelta, string cartellaModelli, IProgress<Avanzamento>? avanzamento, CancellationToken ct)
    {
        var cartella = scelta.Scheda.Cartella(cartellaModelli);
        if (!Percorsi.StaDentro(cartellaModelli, cartella)) throw new InvalidOperationException("La cartella del modello uscirebbe da quella dei modelli.");
        Directory.CreateDirectory(cartella);
        ControllaSpazio(cartella, scelta.Totale);
        var salvati = new List<string>();
        salvati.Add(await UnFile(scelta.Scheda.Repo, scelta.Revisione, scelta.Pesi, Path.Combine(cartella, scelta.NomePesi), avanzamento, ct).ConfigureAwait(false));
        if (scelta.Proiettore is not null && scelta.NomeProiettore is not null)
            salvati.Add(await UnFile(scelta.Scheda.Repo, scelta.Revisione, scelta.Proiettore, Path.Combine(cartella, scelta.NomeProiettore), avanzamento, ct).ConfigureAwait(false));
        return salvati;
    }

    async Task<string> UnFile(string repo, string revisione, FileRepo file, string destinazione, IProgress<Avanzamento>? avanzamento, CancellationToken ct)
    {
        if (!CatalogoModelli.PercorsoSicuro(file.Percorso)) throw new InvalidOperationException("Il percorso " + file.Percorso + " non è sicuro.");
        if (!file.HaImpronta) throw new InvalidOperationException("Hugging Face non dice l'impronta di " + file.Percorso + ": non lo installo.");
        var parziale = destinazione + ".part";
        var nome = Path.GetFileName(destinazione);
        try
        {
            using var risposta = await _http.GetAsync(CatalogoModelli.IndirizzoFile(repo, revisione, file.Percorso), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            risposta.EnsureSuccessStatusCode();
            var totale = risposta.Content.Headers.ContentLength ?? file.Byte;
            // Per i file LFS si controlla lo SHA-256; per quelli piccoli l'impronta git: SHA-1 di «blob <byte>\0» più il contenuto.
            using var sha = IncrementalHash.CreateHash(file.Sha256 is not null ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA1);
            if (file.Sha256 is null) sha.AppendData(System.Text.Encoding.ASCII.GetBytes("blob " + file.Byte + "\0"));
            await using (var sorgente = await risposta.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var uscita = new FileStream(parziale, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true))
            {
                var buffer = new byte[1 << 20];
                long fatti = 0;
                var ultimo = DateTime.MinValue;
                int letti;
                while ((letti = await sorgente.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await uscita.WriteAsync(buffer.AsMemory(0, letti), ct).ConfigureAwait(false);
                    sha.AppendData(buffer, 0, letti);
                    fatti += letti;
                    if (DateTime.UtcNow - ultimo > TimeSpan.FromMilliseconds(250))
                    {
                        ultimo = DateTime.UtcNow;
                        avanzamento?.Report(new Avanzamento(nome, fatti, totale));
                    }
                }
                avanzamento?.Report(new Avanzamento(nome, fatti, totale));
            }
            var impronta = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
            if (impronta != (file.Sha256 ?? file.ShaGit))
                throw new InvalidOperationException("l'impronta di " + nome + " non corrisponde a quella di Hugging Face: il file non è stato installato.");
            File.Move(parziale, destinazione, overwrite: true);
            return destinazione;
        }
        finally
        {
            try { if (File.Exists(parziale)) File.Delete(parziale); } catch { /* si cancella al prossimo tentativo */ }
        }
    }

    public void Dispose() => _http.Dispose();
}
