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
        _http = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{porta}/"),
            Timeout = TimeSpan.FromMinutes(4)
        };
    }

    public async Task<bool> Vivo(CancellationToken ct)
    {
        try
        {
            using var r = await _http.GetAsync("health", ct);
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> Carica(string id, CancellationToken ct)
    {
        var risp = await Posta("models/load", new { model = id }, ct);
        if (!risp.ok && risp.corpo.Contains("already", StringComparison.OrdinalIgnoreCase)) return true;
        var fine = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < fine)
        {
            ct.ThrowIfCancellationRequested();
            var stato = await StatoModello(id, ct);
            if (stato is "loaded") return true;
            if (stato is "failed" or "error") return false;
            await Task.Delay(400, ct);
        }
        return await StatoModello(id, ct) is "loaded";
    }

    public async Task Scarica(string id, CancellationToken ct)
    {
        await Posta("models/unload", new { model = id }, ct);
    }

    public async Task SalvaSlot(string id, string file, CancellationToken ct)
    {
        await Posta($"slots/0?action=save&model={Uri.EscapeDataString(id)}", new { filename = file }, ct);
    }

    public async Task RipristinaSlot(string id, string file, CancellationToken ct)
    {
        await Posta($"slots/0?action=restore&model={Uri.EscapeDataString(id)}", new { filename = file }, ct);
    }

    public async Task<string?> StatoModello(string id, CancellationToken ct)
    {
        try
        {
            using var r = await _http.GetAsync("models", ct);
            var corpo = await r.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(corpo);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
            foreach (var m in data.EnumerateArray())
            {
                var mid = m.TryGetProperty("id", out var i) ? i.GetString() : null;
                if (!string.Equals(mid, id, StringComparison.OrdinalIgnoreCase)) continue;
                if (!m.TryGetProperty("status", out var st)) return "unloaded";
                if (st.ValueKind == JsonValueKind.String) return st.GetString();
                if (st.ValueKind == JsonValueKind.Object && st.TryGetProperty("value", out var v)) return v.GetString();
            }
            return "unloaded";
        }
        catch { return null; }
    }

    public async Task<string> Decidi(string idModello, string stato, CatalogoAzioni catalogo, CancellationToken ct)
    {
        var criteria = new Dictionary<string, string>();
        foreach (var a in catalogo.Azioni) criteria[a.Nome] = a.Descrizione;
        var corpo = new
        {
            model = idModello,
            state = stato,
            questions = new
            {
                azione = new
                {
                    type = "choice",
                    instructions = "Quale azione deve fare Liquidino? Scegline una sola fra queste.",
                    criteria
                }
            }
        };
        var risp = await Posta("v1/systemone", corpo, ct);
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

            var risp = await Posta("v1/chat/completions", payload, ct);
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
        using var contenuto = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");
        using var r = await _http.PostAsync("tts", contenuto, ct);
        var bytes = await r.Content.ReadAsByteArrayAsync(ct);
        if (!r.IsSuccessStatusCode || bytes.Length < 44)
            throw new InvalidOperationException("La voce non è uscita.");
        return bytes;
    }

    async Task<(bool ok, string corpo)> Posta(string percorso, object corpo, CancellationToken ct)
    {
        using var contenuto = new StringContent(JsonSerializer.Serialize(corpo, JsonIo.Opzioni), Encoding.UTF8, "application/json");
        contenuto.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var r = await _http.PostAsync(percorso, contenuto, ct);
        var testo = await r.Content.ReadAsStringAsync(ct);
        return (r.IsSuccessStatusCode, testo);
    }

    static string Taglia(string s) => s.Length <= 180 ? s : s[..180];

    public void Dispose() => _http.Dispose();
}
