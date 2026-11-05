using System.Text;

namespace Liquidino.Nucleo;

/// <summary>Una prova di riempimento: quanti token il motore ha letto davvero, e se lo slot è finito su disco.</summary>
public sealed record Riempimento(int? Token, bool? SlotSalvato, string? Errore);

public sealed record EsitoProvaContesto(bool Riuscita, int Contesto, int Token, bool? SlotSalvato, IReadOnlyList<string> Passi)
{
    public string Frase()
    {
        if (!Riuscita)
            return "Prova di carico non riuscita: nessun gradino ha retto, ho lasciato il contesto di prima. " + string.Join(" ", Passi);
        var slot = SlotSalvato switch
        {
            true => " Lo slot si salva su disco.",
            false => " Lo slot non si è salvato su disco: il resto funziona lo stesso.",
            _ => ""
        };
        return "Prova di carico riuscita: le mani reggono " + Contesto / 1024 + "K di contesto (ho riempito " + Token + " token)." + slot;
    }
}

/// <summary>
/// La prova di carico che riempie davvero la cache. Parte dal contesto più grande e scende di un gradino
/// (la metà) finché uno regge. «Regge» vuol dire che il motore ha letto almeno l'80% del contesto senza errori.
/// </summary>
public static class ProvaContesto
{
    public const int Minimo = 8192;
    public const double Quota = 0.9;
    public const double Soglia = 0.8;
    public const int Campione = 2000;

    public static IReadOnlyList<int> Gradini(int partenza)
    {
        var lista = new List<int>();
        var c = Math.Max(Minimo, partenza);
        while (c >= Minimo)
        {
            lista.Add(c);
            if (c == Minimo) break;
            c = Math.Max(Minimo, c / 2);
        }
        return lista;
    }

    public static int Obiettivo(int contesto) => (int)(contesto * Quota);

    /// <summary>
    /// Quanti caratteri usare per misurare il valore di un token. Un carattere vale meno di un token,
    /// quindi un campione lungo quanto i token voluti resta sempre sotto il contesto da riempire.
    /// </summary>
    public static int CampionePer(int voluti) => Math.Clamp(voluti, 100, Campione);

    public static bool Regge(int contesto, int? letti) => letti is int n && n >= contesto * Soglia;

    /// <summary>
    /// Quanti caratteri servono per arrivare a «voluti» token, sapendo che una frase quasi vuota ne costa «base»
    /// e il campione di «caratteriCampione» ne costa «conCampione».
    /// </summary>
    public static int CaratteriPer(int voluti, int @base, int conCampione, int caratteriCampione)
    {
        var perToken = conCampione > @base ? (double)caratteriCampione / (conCampione - @base) : 3.0;
        return Math.Max(1, (int)((voluti - @base) * perToken));
    }

    /// <summary>Testo italiano che si ripete con un numero diverso, lungo esattamente «caratteri».</summary>
    public static string Riempitivo(int caratteri)
    {
        var b = new StringBuilder(Math.Max(0, caratteri) + 120);
        for (var i = 1; b.Length < caratteri; i++)
            b.Append("Riga ").Append(i).Append(": Liquidino conta le stelle sul desktop e tiene a mente ogni numero. ");
        return b.ToString(0, Math.Max(0, caratteri));
    }

    /// <summary>Prova i gradini in ordine. «prova» accende il motore con quel contesto e lo riempie.</summary>
    public static async Task<EsitoProvaContesto> Esegui(int partenza, Func<int, CancellationToken, Task<Riempimento>> prova, Action<string>? passo, CancellationToken ct)
    {
        var passi = new List<string>();
        foreach (var contesto in Gradini(partenza))
        {
            ct.ThrowIfCancellationRequested();
            passo?.Invoke("Provo " + contesto / 1024 + "K…");
            Riempimento r;
            try { r = await prova(contesto, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception e) { r = new Riempimento(null, null, e.Message); }
            if (Regge(contesto, r.Token))
            {
                passi.Add(contesto / 1024 + "K regge.");
                return new EsitoProvaContesto(true, contesto, r.Token!.Value, r.SlotSalvato, passi);
            }
            var perche = r.Token is int n ? "letti solo " + n + " token" : r.Errore ?? "nessuna risposta";
            passi.Add(contesto / 1024 + "K no (" + perche + ").");
            passo?.Invoke(contesto / 1024 + "K non regge: " + perche + ".");
        }
        return new EsitoProvaContesto(false, 0, 0, null, passi);
    }
}

/// <summary>
/// La regola di Windows Firewall che toglie internet a llama-server. Serve l'amministratore una volta (UAC).
/// Il traffico su 127.0.0.1 non passa dal firewall, quindi Liquidino continua a parlargli.
/// </summary>
public static class RegolaFirewall
{
    public const string Nome = "Liquidino - llama-server senza internet";

    public static string ScriptCrea(string eseguibile) =>
        "$ErrorActionPreference = 'Stop'; " +
        "Remove-NetFirewallRule -DisplayName '" + Nome + "' -ErrorAction SilentlyContinue; " +
        "New-NetFirewallRule -DisplayName '" + Nome + "' -Direction Outbound -Action Block -Profile Any -Program '" + Apici(eseguibile) + "' | Out-Null";

    /// <summary>Esce con 0 se la regola c'è per questo eseguibile, 3 se non c'è, 4 se è per un altro.</summary>
    public static string ScriptControlla(string eseguibile) =>
        "$r = Get-NetFirewallRule -DisplayName '" + Nome + "' -ErrorAction SilentlyContinue; " +
        "if (-not $r) { exit 3 }; " +
        "$p = ($r | Get-NetFirewallApplicationFilter).Program; " +
        "if ($p -contains '" + Apici(eseguibile) + "') { exit 0 } else { exit 4 }";

    /// <summary>Argomenti per powershell.exe. Lo script viaggia codificato: niente problemi con spazi e virgolette.</summary>
    public static string Argomenti(string script) =>
        "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand " +
        Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

    /// <summary>Dentro gli apici singoli di PowerShell, l'apice si raddoppia.</summary>
    public static string Apici(string testo) => (testo ?? "").Replace("'", "''");
}
