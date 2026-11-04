using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Liquidino.Nucleo;

public sealed record RichiestaChat(
    RuoloModello Modello,
    string Sistema,
    IReadOnlyList<(string Ruolo, string Testo)> Storia,
    string Utente,
    string? ImmagineBase64,
    string? AudioWavBase64,
    string? StrumentoNome,
    string? StrumentoSchema,
    double Temperatura,
    int MaxToken,
    bool SpegniRagionamento,
    string? TipoImmagine = null);

public sealed record RispostaChat(string Testo, string? ArgomentiStrumento);

/// <summary>Parla solo con llama-server su 127.0.0.1. Niente altre reti.</summary>
public sealed class ClienteLlama : IDisposable
{
    readonly HttpClient _http;

    public ClienteLlama(int porta)
    {
        // Solo questo PC: niente proxy di sistema, altrimenti 127.0.0.1 resta in attesa del proxy.
        var prese = new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(2)
        };
        _http = new HttpClient(prese)
        {
            BaseAddress = new Uri($"http://127.0.0.1:{porta}/"),
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public static TimeSpan TempoVita { get; } = TimeSpan.FromSeconds(2);
    public static TimeSpan TempoScarico { get; } = TimeSpan.FromSeconds(4);
    public static TimeSpan TempoCarico { get; } = TimeSpan.FromSeconds(20);
    public static TimeSpan TempoChat { get; } = TimeSpan.FromSeconds(60);
    public static TimeSpan TempoVoce { get; } = TimeSpan.FromSeconds(30);

    public async Task<bool> Vivo(CancellationToken ct)
    {
        try
        {
            using var limite = Limite(ct, TempoVita);
            using var r = await _http.GetAsync("health", limite.Token).ConfigureAwait(false);
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public string? UltimoErrore { get; private set; }

    /// <summary>Chiede al router di caricare il modello e aspetta che lo stato sia «loaded».</summary>
    public async Task<bool> Carica(string id, CancellationToken ct)
    {
        UltimoErrore = null;
        using var limite = Limite(ct, TempoCarico);
        var risp = await Posta("models/load", new { model = id }, limite.Token).ConfigureAwait(false);
        if (!risp.ok)
        {
            if (risp.corpo.Contains("already", StringComparison.OrdinalIgnoreCase)) return true;
            UltimoErrore = "il router ha rifiutato il caricamento di " + id + ": " + Taglia(risp.corpo);
            return false;
        }
        var fine = DateTime.UtcNow + TempoCarico;
        while (DateTime.UtcNow < fine)
        {
            limite.Token.ThrowIfCancellationRequested();
            var stato = await StatoModello(id, limite.Token).ConfigureAwait(false);
            if (stato is "loaded") return true;
            if (stato is "failed" or "error")
            {
                UltimoErrore = "il modello " + id + " è andato in errore durante il caricamento.";
                return false;
            }
            await Task.Delay(400, limite.Token).ConfigureAwait(false);
        }
        var finale = await StatoModello(id, ct).ConfigureAwait(false);
        if (finale is "loaded") return true;
        UltimoErrore = "tempo scaduto: " + id + " non è pronto (stato: " + (finale ?? "sconosciuto") + ").";
        return false;
    }

    public async Task Scarica(string id, CancellationToken ct)
    {
        using var limite = Limite(ct, TempoScarico);
        await Posta("models/unload", new { model = id }, limite.Token).ConfigureAwait(false);
    }

    public async Task SalvaSlot(string id, string file, CancellationToken ct)
    {
        using var limite = Limite(ct, TempoScarico);
        await Posta($"slots/0?action=save&model={Uri.EscapeDataString(id)}", new { filename = file }, limite.Token).ConfigureAwait(false);
    }

    public async Task RipristinaSlot(string id, string file, CancellationToken ct)
    {
        using var limite = Limite(ct, TempoScarico);
        await Posta($"slots/0?action=restore&model={Uri.EscapeDataString(id)}", new { filename = file }, limite.Token).ConfigureAwait(false);
    }

    public async Task<string?> StatoModello(string id, CancellationToken ct)
    {
        try
        {
            using var limite = Limite(ct, TempoVita);
            using var r = await _http.GetAsync("models", limite.Token).ConfigureAwait(false);
            var corpo = await r.Content.ReadAsStringAsync(limite.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(corpo);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
            foreach (var m in data.EnumerateArray())
            {
                var mid = m.TryGetProperty("id", out var i) ? i.GetString() : null;
                if (!string.Equals(mid, id, StringComparison.OrdinalIgnoreCase)) continue;
                if (!m.TryGetProperty("status", out var st)) return "unloaded";
                if (st.ValueKind == JsonValueKind.String) return st.GetString();
                if (st.ValueKind == JsonValueKind.Object)
                {
                    // Il router, se il processo figlio muore, lascia value=unloaded e failed=true.
                    if (st.TryGetProperty("failed", out var fallito) && fallito.ValueKind == JsonValueKind.True)
                        return "failed";
                    if (st.TryGetProperty("value", out var v)) return v.GetString();
                }
            }
            return "unloaded";
        }
        catch { return null; }
    }

    public async Task<string> Decidi(string idModello, string stato, CatalogoAzioni catalogo, CancellationToken ct)
    {
        var freschi = CatalogoAzioni.Predefinito();
        var criteria = new Dictionary<string, string>();
        foreach (var a in catalogo.Azioni)
            criteria[a.Nome] = CatalogoAzioni.Criterio(freschi.Trova(a.Nome) ?? a);
        var corpo = new
        {
            model = idModello,
            state = stato,
            questions = new
            {
                azione = new
                {
                    type = "choice",
                    instructions = CatalogoAzioni.IstruzioniD1,
                    criteria
                }
            }
        };
        using var limite = Limite(ct, TempoChat);
        var risp = await Posta("v1/systemone", corpo, limite.Token).ConfigureAwait(false);
        if (!risp.ok) throw new InvalidOperationException("d1 non ha risposto: " + Taglia(risp.corpo));
        return risp.corpo;
    }

    public async Task<RispostaChat> Chat(string idModello, RichiestaChat richiesta, CancellationToken ct)
    {
        var messaggi = new List<object>
        {
            new { role = "system", content = richiesta.Sistema }
        };
        foreach (var (ruolo, testo) in richiesta.Storia)
            messaggi.Add(new { role = ruolo, content = testo });

        object utente;
        if (richiesta.ImmagineBase64 is { Length: > 0 } img)
        {
            utente = new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "text", text = richiesta.Utente },
                    new { type = "image_url", image_url = new { url = "data:" + (richiesta.TipoImmagine ?? "image/png") + ";base64," + img } }
                }
            };
        }
        else if (richiesta.AudioWavBase64 is { Length: > 0 } wav)
        {
            utente = new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "text", text = string.IsNullOrWhiteSpace(richiesta.Utente) ? "<__media__>" : richiesta.Utente },
                    new { type = "input_audio", input_audio = new { data = wav, format = "wav" } }
                }
            };
        }
        else utente = new { role = "user", content = richiesta.Utente };
        messaggi.Add(utente);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = idModello,
            ["messages"] = messaggi,
            ["temperature"] = richiesta.Temperatura,
            ["max_tokens"] = richiesta.MaxToken
        };
        if (richiesta.SpegniRagionamento)
            payload["chat_template_kwargs"] = new { enable_thinking = false };
        JsonDocument? schema = null;
        try
        {
            if (richiesta.StrumentoNome is not null && richiesta.StrumentoSchema is not null)
            {
                schema = JsonDocument.Parse(richiesta.StrumentoSchema);
                payload["tools"] = new object[]
                {
                    new
                    {
                        type = "function",
                        function = new
                        {
                            name = richiesta.StrumentoNome,
                            description = "Azione permessa di Liquidino.",
                            parameters = schema.RootElement
                        }
                    }
                };
                payload["tool_choice"] = new { type = "function", function = new { name = richiesta.StrumentoNome } };
            }

            using var limite = Limite(ct, TempoChat);
            var risp = await Posta("v1/chat/completions", payload, limite.Token).ConfigureAwait(false);
            if (!risp.ok) throw new InvalidOperationException("La chat non ha risposto: " + Taglia(risp.corpo));
            return LeggiChat(risp.corpo);
        }
        finally { schema?.Dispose(); }
    }

    public static RispostaChat LeggiChat(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var scelta = doc.RootElement.GetProperty("choices")[0];
        var msg = scelta.GetProperty("message");
        var testo = "";
        if (msg.TryGetProperty("content", out var content))
        {
            testo = content.ValueKind == JsonValueKind.String
                ? content.GetString() ?? ""
                : content.ToString();
        }
        string? argomenti = null;
        if (msg.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in calls.EnumerateArray())
            {
                if (!c.TryGetProperty("function", out var fn)) continue;
                if (fn.TryGetProperty("arguments", out var ar))
                    argomenti = ar.ValueKind == JsonValueKind.String ? ar.GetString() : ar.GetRawText();
                break;
            }
        }
        return new RispostaChat(Testo.PulisciRisposta(testo), argomenti);
    }

    public async Task<byte[]> Sintetizza(string idModello, string testo, string voce, CancellationToken ct)
    {
        var corpo = new
        {
            model = idModello,
            input = testo,
            lang = "it",
            voice = voce is "male" or "female" ? voce : "female",
            response_format = "wav"
        };
        using var limite = Limite(ct, TempoVoce);
        using var contenuto = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");
        using var r = await _http.PostAsync("tts", contenuto, limite.Token).ConfigureAwait(false);
        var bytes = await r.Content.ReadAsByteArrayAsync(limite.Token).ConfigureAwait(false);
        if (!r.IsSuccessStatusCode || bytes.Length < 44)
            throw new InvalidOperationException("La voce non è uscita.");
        return bytes;
    }

    async Task<(bool ok, string corpo)> Posta(string percorso, object corpo, CancellationToken ct)
    {
        using var contenuto = new StringContent(JsonSerializer.Serialize(corpo, JsonIo.Opzioni), Encoding.UTF8, "application/json");
        contenuto.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var r = await _http.PostAsync(percorso, contenuto, ct).ConfigureAwait(false);
        var testo = await r.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return (r.IsSuccessStatusCode, testo);
    }

    static CancellationTokenSource Limite(CancellationToken ct, TimeSpan tempo)
    {
        var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(tempo);
        return limite;
    }

    static string Taglia(string s) => s.Length <= 180 ? s : s[..180];

    public void Dispose() => _http.Dispose();
}
