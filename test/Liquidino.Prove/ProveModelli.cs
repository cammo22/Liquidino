using System.Net;
using System.Security.Cryptography;
using System.Text;
using Liquidino.Nucleo;
using Xunit;

namespace Liquidino.Prove;

/// <summary>Risponde come Hugging Face, ma da un dizionario in memoria. Conta le richieste.</summary>
sealed class HuggingFaceFinto : HttpMessageHandler
{
    public Dictionary<string, byte[]> Risposte { get; } = new();
    public List<string> Chieste { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        Chieste.Add(url);
        return Task.FromResult(Risposte.TryGetValue(url, out var corpo)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(corpo) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

public class ProveModelli
{
    static string Sha(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    const string Elenco = """
        [
          {"type":"file","path":"README.md","size":900},
          {"type":"file","path":"LFM3-Omni-2B-F16.gguf","size":10,"lfs":{"oid":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","size":4000000000}},
          {"type":"file","path":"LFM3-Omni-2B-Q8_0.gguf","size":10,"lfs":{"oid":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","size":2200000000}},
          {"type":"file","path":"LFM3-Omni-2B-Q4_K_M.gguf","size":10,"lfs":{"oid":"CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC","size":1300000000}},
          {"type":"file","path":"mmproj-LFM3-Omni-2B-F16.gguf","size":10,"lfs":{"oid":"dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd","size":800000000}},
          {"type":"file","path":"mmproj-LFM3-Omni-2B-Q8_0.gguf","size":10,"lfs":{"oid":"eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee","size":450000000}},
          {"type":"file","path":"big/LFM3-Omni-2B-BF16-00001-of-00002.gguf","size":10,"lfs":{"oid":"ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff","size":5}},
          {"type":"directory","path":"big"}
        ]
        """;

    [Fact]
    public void Il_catalogo_ha_i_sei_ruoli_e_i_nomi_li_capisce_il_rilevatore()
    {
        Assert.Equal(Enum.GetValues<RuoloModello>().Order(), CatalogoModelli.Tutti.Select(s => s.Ruolo).Order());
        foreach (var scheda in CatalogoModelli.Tutti)
        {
            Assert.StartsWith("LiquidAI/", scheda.Repo);
            Assert.EndsWith("-GGUF", scheda.Repo);
            var scelta = new SceltaDownload(scheda, "abc1234", new FileRepo(scheda.Nome + "-Q4_K_M.gguf", 1, new string('a', 64)), new FileRepo("mmproj-x-Q8_0.gguf", 1, new string('b', 64)));
            Assert.Equal(scheda.Ruolo, RilevatoreModelli.RuoloDi(scelta.NomePesi));
            Assert.Null(RilevatoreModelli.RuoloDi(scelta.NomeProiettore!));
            Assert.Equal(RilevatoreModelli.Chiave(scelta.NomePesi), RilevatoreModelli.Chiave(scelta.NomeProiettore!));
        }
    }

    [Fact]
    public void Sceglie_Q4_K_M_e_il_proiettore_Q8_e_salta_le_parti_divise()
    {
        var file = CatalogoModelli.LeggiElenco(Elenco);
        Assert.Equal(7, file.Count);
        Assert.Null(file.Single(f => f.Percorso == "README.md").Sha256);
        var scelta = CatalogoModelli.Scegli(CatalogoModelli.Di(RuoloModello.Omni)!, "0123456789abcdef", file)!;
        Assert.Equal("LFM3-Omni-2B-Q4_K_M.gguf", scelta.Pesi.Percorso);
        Assert.Equal(new string('c', 64), scelta.Pesi.Sha256);
        Assert.Equal(1300000000, scelta.Pesi.Byte);
        Assert.Equal("mmproj-LFM3-Omni-2B-Q8_0.gguf", scelta.Proiettore!.Percorso);
        Assert.Equal("LFM3-Omni-2B-Q4_K_M.gguf", scelta.NomePesi);
        Assert.Equal("mmproj-LFM3-Omni-2B-Q8_0.gguf", scelta.NomeProiettore);
        Assert.Equal(1750000000, scelta.Totale);
    }

    [Fact]
    public void Senza_un_gguf_con_impronta_non_sceglie_niente()
    {
        var file = new[] { new FileRepo("README.md", 10, null), new FileRepo("x-Q4_K_M.gguf", 10, null) };
        Assert.Null(CatalogoModelli.Scegli(CatalogoModelli.Tutti[0], "0123456", file));
        Assert.Equal("Q8_0", CatalogoModelli.Quant("a/b/LFM3-1.7B-Q8_0.gguf"));
        Assert.Equal("Q4_0", CatalogoModelli.Quant("LFM3-1.7B-QAD-Q4_0.gguf"));
        Assert.Equal("gguf", CatalogoModelli.Quant("strano.gguf"));
        Assert.Equal("abcdef0123", CatalogoModelli.LeggiRevisione("""{"id":"x","sha":"abcdef0123"}"""));
        Assert.Null(CatalogoModelli.LeggiRevisione("""{"id":"x","sha":"../../etc"}"""));
        Assert.False(CatalogoModelli.PercorsoSicuro("../fuori.gguf"));
        Assert.False(CatalogoModelli.PercorsoSicuro("C:/fuori.gguf"));
        Assert.True(CatalogoModelli.PercorsoSicuro("sotto/LFM3-1.7B-Q4_K_M.gguf"));
    }

    [Fact]
    public async Task Scarica_controlla_l_impronta_e_installa_coi_nomi_giusti()
    {
        var pesi = Encoding.ASCII.GetBytes(new string('p', 5000));
        var proiettore = Encoding.ASCII.GetBytes("proiettore");
        var finto = new HuggingFaceFinto();
        var repo = "LiquidAI/d1-2B-GGUF";
        finto.Risposte[CatalogoModelli.Sito + "api/models/" + repo] = Encoding.UTF8.GetBytes("""{"sha":"1234567890abcdef"}""");
        finto.Risposte[CatalogoModelli.Sito + "api/models/" + repo + "/tree/1234567890abcdef?recursive=true"] = Encoding.UTF8.GetBytes(
            "[{\"type\":\"file\",\"path\":\"d1-2B-Q4_K_M.gguf\",\"size\":1,\"lfs\":{\"oid\":\"" + Sha(pesi) + "\",\"size\":" + pesi.Length + "}}," +
            "{\"type\":\"file\",\"path\":\"mmproj-d1-2B-F16.gguf\",\"size\":1,\"lfs\":{\"oid\":\"" + Sha(proiettore) + "\",\"size\":" + proiettore.Length + "}}]");
        finto.Risposte[CatalogoModelli.IndirizzoFile(repo, "1234567890abcdef", "d1-2B-Q4_K_M.gguf")] = pesi;
        finto.Risposte[CatalogoModelli.IndirizzoFile(repo, "1234567890abcdef", "mmproj-d1-2B-F16.gguf")] = proiettore;

        var modelli = Path.Combine(Aiuti.Temporanea(), "modelli");
        using var scaricatore = new ScaricatoreModelli(finto);
        var scelta = await scaricatore.Prepara(CatalogoModelli.Di(RuoloModello.D1)!, CancellationToken.None);
        Assert.NotNull(scelta);
        var visti = new List<Avanzamento>();
        var salvati = await scaricatore.Scarica(scelta!, modelli, new Progresso(visti.Add), CancellationToken.None);
        Assert.Equal(2, salvati.Count);
        Assert.All(salvati, s => Assert.True(Percorsi.StaDentro(modelli, s)));
        Assert.Equal(pesi, File.ReadAllBytes(Path.Combine(modelli, "d1-2B", "d1-2B-Q4_K_M.gguf")));
        Assert.Empty(Directory.GetFiles(modelli, "*.part", SearchOption.AllDirectories));
        Assert.Contains(visti, v => v.Fatti == pesi.Length);

        var trovati = RilevatoreModelli.LeggiGguf(modelli);
        var d1 = Assert.Single(trovati);
        Assert.Equal(RuoloModello.D1, d1.Ruolo);
        Assert.EndsWith("mmproj-d1-2B-F16.gguf", d1.Mmproj);
        Assert.Equal(2, CatalogoModelli.FileDa(d1, modelli).Count);
        Assert.Empty(CatalogoModelli.FileDa(d1, Aiuti.Temporanea()));
    }

    [Fact]
    public async Task Un_file_con_l_impronta_sbagliata_non_resta_fra_i_modelli()
    {
        var finto = new HuggingFaceFinto();
        var scheda = CatalogoModelli.Di(RuoloModello.Mani)!;
        var buono = new FileRepo("LFM3-1.7B-Q4_K_M.gguf", 4, new string('0', 64));
        finto.Risposte[CatalogoModelli.IndirizzoFile(scheda.Repo, "abcdef1", buono.Percorso)] = [1, 2, 3, 4];
        var modelli = Path.Combine(Aiuti.Temporanea(), "modelli");
        using var scaricatore = new ScaricatoreModelli(finto);
        var errore = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scaricatore.Scarica(new SceltaDownload(scheda, "abcdef1", buono, null), modelli, null, CancellationToken.None));
        Assert.Contains("impronta", errore.Message);
        Assert.Empty(Directory.GetFiles(modelli, "*", SearchOption.AllDirectories));
    }

    sealed class Progresso(Action<Avanzamento> fai) : IProgress<Avanzamento>
    {
        public void Report(Avanzamento value) => fai(value);
    }
}

/// <summary>
/// Gira solo in CI con LIQUIDINO_HF=1: controlla che ogni voce del catalogo esista davvero su Hugging Face
/// e che ci sia un GGUF da scegliere. Non scarica i pesi.
/// </summary>
public class ProvaCatalogoHf
{
    [Fact]
    public async Task Ogni_modello_del_catalogo_ha_un_gguf_su_hugging_face()
    {
        if (Environment.GetEnvironmentVariable("LIQUIDINO_HF") != "1") return;
        using var scaricatore = new ScaricatoreModelli();
        var righe = new List<string>();
        foreach (var scheda in CatalogoModelli.Tutti)
        {
            var scelta = await scaricatore.Prepara(scheda, CancellationToken.None);
            righe.Add(scheda.Repo + " -> " + (scelta is null ? "NIENTE" : scelta.Pesi.Percorso + " (" + CatalogoModelli.Peso(scelta.Pesi.Byte) + ")"
                + (scelta.Proiettore is null ? "" : " + " + scelta.Proiettore.Percorso) + " @ " + scelta.Revisione[..7]));
            Assert.True(scelta is not null, string.Join("\n", righe));
            Assert.Equal(scheda.Ruolo, RilevatoreModelli.RuoloDi(scelta!.NomePesi));
        }
        var esito = Environment.GetEnvironmentVariable("LIQUIDINO_HF_ESITO");
        if (!string.IsNullOrWhiteSpace(esito)) File.WriteAllLines(esito, righe);
    }
}
