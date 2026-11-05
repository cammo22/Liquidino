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
        string Dettaglio(string? motivo)
        {
            var log = Path.Combine(dati, "motore", "server.log");
            var coda = File.Exists(log) ? LeggiCondiviso(log) : "(niente server.log)";
            if (coda.Length > 2500) coda = coda[^2500..];
            var ini = Path.Combine(dati, "motore", "modelli.ini");
            var preset = File.Exists(ini) ? LeggiCondiviso(ini) : "(niente modelli.ini)";
            return (motivo ?? "senza dettaglio") + "\n" + regista.Riassunto() + "\n" + preset + "\n" + coda;
        }
        try
        {
            // Il dettaglio si legge solo se la prova fallisce: server.log intanto si sta scrivendo.
            void Verifica(bool vero, Func<string?> motivo)
            {
                if (!vero) Assert.Fail(Dettaglio(motivo()));
            }
            Verifica(regista.MotorePresente, regista.Riassunto);
            Verifica(regista.Presente(RuoloModello.Mani), regista.Riassunto);
            var acceso = await regista.Assicura(new[] { RuoloModello.Mani }, tempo.Token);
            Verifica(acceso, () => regista.UltimoErrore ?? "caricamento fallito senza dettaglio");
            Assert.Contains(RuoloModello.Mani, regista.Accesi);

            var risp = await regista.Completa(new RichiestaChat(
                RuoloModello.Mani,
                "Continue the story in one sentence.",
                Array.Empty<(string, string)>(),
                "Once upon a time",
                null, null, null, null,
                0.7, 32, true), tempo.Token);
            Verifica(!string.IsNullOrWhiteSpace(risp?.Testo), () => "la chat è tornata vuota");

            // Prova di carico vera, in piccolo. Il GGUF della CI è addestrato su 128 token e llama-server
            // taglia lo slot a 128 anche se il preset dice 2048: ne chiediamo 100, cioè quasi tutto lo slot.
            const int voluti = 100;
            var riempito = await regista.ProvaRiempimento(voluti, tempo.Token);
            File.WriteAllText(Path.Combine(radice, "esito-riempimento.txt"),
                "token voluti: " + voluti + ", letti: " + (riempito.Token?.ToString() ?? "?") + ", slot su disco: " + (riempito.SlotSalvato?.ToString() ?? "?")
                + ", errore: " + (riempito.Errore ?? "nessuno") + ", risposta per lo slot: " + (riempito.NotaSlot ?? "nessuna"));
            Verifica(ProvaContesto.Regge(voluti, riempito.Token) && riempito.Token <= 128,
                () => "prova di carico: " + (riempito.Token?.ToString() ?? riempito.Errore));

            await regista.ScaricaTutto(tempo.Token);
            Assert.DoesNotContain(RuoloModello.Mani, regista.Accesi);

            var giornale = Directory.GetFiles(Percorsi.Registro(dati), "*.jsonl").Single();
            var log = LeggiCondiviso(giornale);
            Assert.Contains("chiedo il caricamento", log);
            Assert.Contains("caricato", log);
            Assert.Contains("scaricato", log);
        }
        finally
        {
            regista.UccidiSubito();
        }
    }

    /// <summary>Legge un file che intanto si sta ancora scrivendo (il log di llama-server, copiato riga per riga).</summary>
    static string LeggiCondiviso(string percorso)
    {
        using var flusso = new FileStream(percorso, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var lettore = new StreamReader(flusso);
        return lettore.ReadToEnd();
    }

}
