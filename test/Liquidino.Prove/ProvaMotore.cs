using Liquidino.Nucleo;
using Xunit;

namespace Liquidino.Prove;

/// <summary>
/// Gira solo in CI, con LIQUIDINO_E2E=1 e un llama-server CPU più un GGUF piccolo
/// già scaricati dallo script. Sul PC di tutti i giorni il test non fa niente.
/// </summary>
public class ProvaMotore
{
    [Fact]
    public async Task Il_regista_carica_risponde_e_scarica()
    {
        if (Environment.GetEnvironmentVariable("LIQUIDINO_E2E") != "1")
            return;

        var radice = Environment.GetEnvironmentVariable("LIQUIDINO_E2E_ROOT");
        Assert.False(string.IsNullOrWhiteSpace(radice), "Manca LIQUIDINO_E2E_ROOT");
        Assert.True(File.Exists(Path.Combine(radice!, "modelli", "prova-1.7.gguf")), "Manca il GGUF di prova");

        var dati = Path.Combine(radice, "dati");
        Directory.CreateDirectory(dati);
        var imp = new ImpostazioniUtente
        {
            PercorsoFile = Path.Combine(dati, "impostazioni.json"),
            CartellaMotore = radice,
            Porta = 18241,
            ContestoMani = 2048,
            PrimoAvvioCompletato = true,
            NomeUtente = "prova"
        };
        var registro = new Registro(dati);
        var regista = new Regista(imp, registro, dati);
        using var tempo = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            Assert.True(regista.MotorePresente, regista.Riassunto());
            Assert.True(regista.Presente(RuoloModello.Mani), regista.Riassunto());
            var acceso = await regista.Assicura(new[] { RuoloModello.Mani }, tempo.Token);
            Assert.True(acceso, regista.UltimoErrore ?? "caricamento fallito senza dettaglio");
            Assert.Contains(RuoloModello.Mani, regista.Accesi);

            var risp = await regista.Completa(new RichiestaChat(
                RuoloModello.Mani,
                "Rispondi solo con la parola ciao.",
                Array.Empty<(string, string)>(),
                "Di' ciao.",
                null, null, null, null,
                0, 16, true), tempo.Token);
            Assert.False(string.IsNullOrWhiteSpace(risp?.Testo), "la chat è tornata vuota");

            await regista.ScaricaTutto(tempo.Token);
            Assert.DoesNotContain(RuoloModello.Mani, regista.Accesi);

            var giornale = Directory.GetFiles(Percorsi.Registro(dati), "*.jsonl").Single();
            var log = File.ReadAllText(giornale);
            Assert.Contains("chiedo il caricamento", log);
            Assert.Contains("caricato", log);
            Assert.Contains("scaricato", log);
        }
        finally
        {
            regista.UccidiSubito();
        }
    }
}
