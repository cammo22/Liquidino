using Liquidino.Nucleo;
using Xunit;

namespace Liquidino.Prove;

public class ProveContesto
{
    [Fact]
    public void I_gradini_scendono_di_meta_fino_a_8k()
    {
        Assert.Equal([131072, 65536, 32768, 16384, 8192], ProvaContesto.Gradini(131072));
        Assert.Equal([8192], ProvaContesto.Gradini(4096));
        Assert.Equal([20000, 10000, 8192], ProvaContesto.Gradini(20000));
    }

    [Fact]
    public async Task Tiene_il_primo_gradino_che_regge()
    {
        var provati = new List<int>();
        var esito = await ProvaContesto.Esegui(131072, (c, _) =>
        {
            provati.Add(c);
            return Task.FromResult(c switch
            {
                131072 => new Riempimento(null, null, "memoria finita"),
                65536 => new Riempimento(30000, null, null),
                _ => new Riempimento((int)(c * 0.9), true, null)
            });
        }, null, CancellationToken.None);
        Assert.True(esito.Riuscita);
        Assert.Equal(32768, esito.Contesto);
        Assert.Equal([131072, 65536, 32768], provati);
        Assert.Equal(true, esito.SlotSalvato);
        Assert.Contains("32K", esito.Frase());
        Assert.Contains("128K no (memoria finita)", string.Join(" ", esito.Passi));
        Assert.Contains("64K no (letti solo 30000 token)", string.Join(" ", esito.Passi));
    }

    [Fact]
    public async Task Se_nessuno_regge_lo_dice_e_lo_stop_si_rispetta()
    {
        var no = await ProvaContesto.Esegui(16384, (_, _) => throw new InvalidOperationException("motore spento"), null, CancellationToken.None);
        Assert.False(no.Riuscita);
        Assert.Contains("motore spento", no.Frase());

        using var stop = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProvaContesto.Esegui(131072, (_, t) =>
        {
            stop.Cancel();
            t.ThrowIfCancellationRequested();
            return Task.FromResult(new Riempimento(null, null, null));
        }, null, stop.Token));
    }

    [Fact]
    public void La_calibrazione_e_il_riempitivo()
    {
        // 20 token di contorno, il campione da 2000 caratteri ne vale 520: 4 caratteri per token.
        Assert.Equal(4000, ProvaContesto.CaratteriPer(1020, 20, 520, 2000));
        Assert.Equal(2000, ProvaContesto.Riempitivo(2000).Length);
        Assert.StartsWith("Riga 1: ", ProvaContesto.Riempitivo(50));
        Assert.Contains("Riga 30", ProvaContesto.Riempitivo(3000));
        Assert.True(ProvaContesto.Regge(1000, 800));
        Assert.False(ProvaContesto.Regge(1000, 799));
        Assert.False(ProvaContesto.Regge(1000, null));
        Assert.Equal(117964, ProvaContesto.Obiettivo(131072));
        // Il campione resta più corto (in caratteri) dei token voluti, quindi non sfora mai il contesto.
        Assert.Equal(100, ProvaContesto.CampionePer(100));
        Assert.Equal(100, ProvaContesto.CampionePer(10));
        Assert.Equal(2000, ProvaContesto.CampionePer(117964));
        Assert.Equal(2000, ProvaContesto.CampionePer(7372));
    }

    [Fact]
    public void Il_cliente_legge_i_token_letti_dal_motore()
    {
        var json = """
            {"choices":[{"message":{"role":"assistant","content":"ok"}}],"usage":{"prompt_tokens":1234,"completion_tokens":1,"total_tokens":1235}}
            """;
        var r = ClienteLlama.LeggiChat(json);
        Assert.Equal("ok", r.Testo);
        Assert.Equal(1234, r.TokenPrompt);
        Assert.Null(ClienteLlama.LeggiChat("""{"choices":[{"message":{"content":"ok"}}]}""").TokenPrompt);
    }

    [Fact]
    public void La_regola_del_firewall_blocca_solo_l_uscita_di_llama_server()
    {
        var exe = @"C:\Users\cammo\Liquidino Prova\l'llama\llama-server.exe";
        var crea = RegolaFirewall.ScriptCrea(exe);
        Assert.Contains("-Direction Outbound", crea);
        Assert.Contains("-Action Block", crea);
        Assert.Contains(@"-Program 'C:\Users\cammo\Liquidino Prova\l''llama\llama-server.exe'", crea);
        Assert.Contains("Remove-NetFirewallRule -DisplayName '" + RegolaFirewall.Nome + "'", crea);
        Assert.DoesNotContain("Inbound", crea);

        var argomenti = RegolaFirewall.Argomenti(crea);
        var codificato = argomenti[(argomenti.LastIndexOf(' ') + 1)..];
        Assert.Equal(crea, System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(codificato)));
        Assert.Contains("-NoProfile", argomenti);
        Assert.Contains("exit 3", RegolaFirewall.ScriptControlla(exe));
    }
}
