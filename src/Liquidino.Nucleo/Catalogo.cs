using System.Text.Json;

namespace Liquidino.Nucleo;

public sealed record AzioneCatalogo(
    string Nome,
    string Descrizione,
    string Istruzioni,
    bool VuoleParametri,
    string? Esempio,
    string? Schema);

/// <summary>Il menù chiuso delle azioni. d1 sceglie da qui, il programma esegue solo queste.</summary>
public sealed class CatalogoAzioni
{
    public const double Soglia = 0.6;
    public IReadOnlyList<AzioneCatalogo> Azioni { get; }

    public CatalogoAzioni(IReadOnlyList<AzioneCatalogo> azioni) => Azioni = azioni;

    public AzioneCatalogo? Trova(string nome) =>
        Azioni.FirstOrDefault(a => a.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase));

    public static CatalogoAzioni Predefinito() => new(AzioniPredefinite());

    public static CatalogoAzioni Carica(string percorso)
    {
        try
        {
            if (!File.Exists(percorso)) return Predefinito();
            return Leggi(File.ReadAllText(percorso));
        }
        catch
        {
            return Predefinito();
        }
    }

    public static CatalogoAzioni Leggi(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var lista = new List<AzioneCatalogo>();
        var radice = doc.RootElement;
        var arr = radice.ValueKind == JsonValueKind.Array ? radice : radice.GetProperty("azioni");
        foreach (var e in arr.EnumerateArray())
        {
            var nome = e.GetProperty("nome").GetString() ?? "";
            if (nome.Length == 0) continue;
            lista.Add(new AzioneCatalogo(
                nome,
                e.TryGetProperty("descrizione", out var d) ? d.GetString() ?? "" : "",
                e.TryGetProperty("istruzioni", out var i) ? i.GetString() ?? "" : "",
                e.TryGetProperty("vuoleParametri", out var p) && p.ValueKind == JsonValueKind.True,
                e.TryGetProperty("esempio", out var es) ? es.GetString() : null,
                e.TryGetProperty("schema", out var sc) ? sc.GetRawText() : null));
        }
        if (lista.Count == 0) return Predefinito();
        return new CatalogoAzioni(lista);
    }

    public string AJson()
    {
        using var stream = new MemoryStream();
        using var w = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        w.WriteStartObject();
        w.WritePropertyName("azioni");
        w.WriteStartArray();
        foreach (var a in Azioni)
        {
            w.WriteStartObject();
            w.WriteString("nome", a.Nome);
            w.WriteString("descrizione", a.Descrizione);
            w.WriteString("istruzioni", a.Istruzioni);
            w.WriteBoolean("vuoleParametri", a.VuoleParametri);
            if (a.Esempio is not null) w.WriteString("esempio", a.Esempio);
            if (a.Schema is not null)
            {
                w.WritePropertyName("schema");
                using var schema = JsonDocument.Parse(a.Schema);
                schema.RootElement.WriteTo(w);
            }
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
        w.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static void Semina(string percorso)
    {
        if (File.Exists(percorso)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        File.WriteAllText(percorso, Predefinito().AJson());
    }

    public static IReadOnlyList<AzioneCatalogo> AzioniPredefinite() =>
    [
        new("rispondi",
            "È una chiacchiera o una domanda. Non chiede di scrivere, ricordare o dormire.",
            "Rispondi a voce e per iscritto, col carattere scelto. Non dire di aver fatto cose che non hai fatto.",
            false, null, null),
        new("crea_nota",
            "Vuole una nota, un appunto o un'idea da segnare nel mondo di Liquidino.",
            "Compila solo i campi della nota. Il testo è quello che va ricordato, in italiano, senza aggiungere fatti inventati.",
            true,
            "Richiesta: «prendi nota: comprare il pane e il latte.» → titolo «Pane e latte», testo «Comprare il pane e il latte.», cartella note.",
            """{"type":"object","properties":{"titolo":{"type":"string","maxLength":80},"testo":{"type":"string","maxLength":4000},"cartella":{"type":"string","enum":["note","storie","idee","per-cammo"]}},"required":["titolo","testo","cartella"]}"""),
        new("ricorda",
            "Chiede di ricordare un fatto su di lui, in modo esplicito.",
            "Estrai solo il fatto da tenere. Non aggiungere altro.",
            true,
            "Richiesta: «ricordati che il mio colore preferito è il viola.» → fatto «Il colore preferito è il viola.»",
            """{"type":"object","properties":{"fatto":{"type":"string","maxLength":400}},"required":["fatto"]}"""),
        new("dimentica",
            "Chiede di dimenticare un fatto che gli aveva fatto ricordare.",
            "Estrai le parole da cercare nella memoria, senza ripetere il resto della frase.",
            true,
            "Richiesta: «dimentica quello che ti ho detto sul dentista.» → fatto «dentista»",
            """{"type":"object","properties":{"fatto":{"type":"string","maxLength":200}},"required":["fatto"]}"""),
        new("mostra_creazioni",
            "Vuole vedere le cose che Liquidino ha creato, le note o il suo mondo.",
            "Apri il pannello del mondo. Non inventare file.",
            false, null, null),
        new("dormi",
            "Vuole che Liquidino smetta, scarichi i modelli e dorma.",
            "Scarica i modelli e vai a dormire.",
            false, null, null)
    ];
}

public sealed record SceltaAzione(
    string Azione,
    double Sicurezza,
    bool ChiediConferma,
    IReadOnlyDictionary<string, double> Probabilita,
    string Fonte);

public static class LettoreDecisione
{
    /// <summary>
    /// Legge la risposta di /v1/systemone. La sicurezza è la probabilità della scelta,
    /// come nella prova sulla RTX 4060: sotto 0,6 si chiede conferma.
    /// </summary>
    public static SceltaAzione? Leggi(string json, IReadOnlyCollection<string> ammesse)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("answers", out var answers) || answers.ValueKind != JsonValueKind.Object)
                return null;
            JsonElement? scelta = null;
            foreach (var q in answers.EnumerateObject())
            {
                if (q.Value.ValueKind == JsonValueKind.Object && q.Value.TryGetProperty("choice", out _))
                {
                    scelta = q.Value;
                    if (q.Name is "azione" or "intent" or "action") break;
                }
            }
            if (scelta is null) return null;
            var nome = scelta.Value.GetProperty("choice").GetString() ?? "";
            var probabilita = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (scelta.Value.TryGetProperty("probabilities", out var probs) && probs.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in probs.EnumerateObject())
                    probabilita[p.Name] = Numero(p.Value);
            }
            double sicurezza;
            if (probabilita.TryGetValue(nome, out var dellaScelta)) sicurezza = dellaScelta;
            else if (scelta.Value.TryGetProperty("confidence", out var conf)) sicurezza = Numero(conf);
            else sicurezza = 0;

            if (!ammesse.Contains(nome, StringComparer.OrdinalIgnoreCase))
                return new SceltaAzione("rispondi", 0.61, false, probabilita, "d1");

            var conferma = sicurezza < CatalogoAzioni.Soglia;
            return new SceltaAzione(nome, sicurezza, conferma, probabilita, "d1");
        }
        catch
        {
            return null;
        }
    }

    static double Numero(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Number && e.TryGetDouble(out var d)) return d;
        if (e.ValueKind == JsonValueKind.String &&
            double.TryParse(e.GetString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var s)) return s;
        return 0;
    }
}

/// <summary>Se d1 non c'è, una scelta semplice dalle parole. Meglio chiedere che indovinare.</summary>
public static class DecisoreLocale
{
    public static SceltaAzione Scegli(string testo)
    {
        var t = (testo ?? "").ToLowerInvariant();
        if (Contiene(t, "dimentica")) return Fatta("dimentica", 0.8);
        if (Contiene(t, "ricordati", "ricorda che", "ricorda:")) return Fatta("ricorda", 0.8);
        if (Contiene(t, "prendi nota", "segnati", "una nota", "appunta")) return Fatta("crea_nota", 0.75);
        if (Contiene(t, "creazioni", "cosa hai creato", "fammi vedere", "il tuo mondo", "le tue note")) return Fatta("mostra_creazioni", 0.8);
        if (Contiene(t, "vai a dormire", "mettiti a dormire", "puoi dormire")) return Fatta("dormi", 0.85);
        return Fatta("rispondi", 0.7);
    }

    static bool Contiene(string t, params string[] chiavi) => chiavi.Any(t.Contains);

    static SceltaAzione Fatta(string nome, double sicurezza) =>
        new(nome, sicurezza, sicurezza < CatalogoAzioni.Soglia, new Dictionary<string, double> { [nome] = sicurezza }, "locale");
}
