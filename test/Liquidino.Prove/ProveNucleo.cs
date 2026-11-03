using Liquidino.Nucleo;
using Xunit;

namespace Liquidino.Prove;

public class ProveNucleo
{
    [Fact]
    public void Soglia_sotto_0_6_chiede_conferma_e_sopra_esegue()
    {
        var basse = """
            {"answers":{"azione":{"choice":"crea_nota","probabilities":{"crea_nota":0.39,"ricorda":0.61},"confidence":0.2}}}
            """;
        var alte = """
            {"answers":{"azione":{"choice":"ricorda","probabilities":{"ricorda":0.62,"rispondi":0.38},"confidence":0.4}}}
            """;
        var ammesse = CatalogoAzioni.Predefinito().Azioni.Select(a => a.Nome).ToList();
        var bassa = LettoreDecisione.Leggi(basse, ammesse)!;
        var alta = LettoreDecisione.Leggi(alte, ammesse)!;
        Assert.Equal("crea_nota", bassa.Azione);
        Assert.Equal(0.39, bassa.Sicurezza, 3);
        Assert.True(bassa.ChiediConferma);
        Assert.True(bassa.Sicurezza < CatalogoAzioni.Soglia);
        Assert.False(alta.ChiediConferma);
        Assert.True(alta.Sicurezza >= CatalogoAzioni.Soglia);
    }

    [Fact]
    public void Sicurezza_usa_la_probabilita_della_scelta_non_solo_la_confidence()
    {
        var json = """
            {"answers":{"azione":{"choice":"dormi","probabilities":{"dormi":0.91},"confidence":0.1}}}
            """;
        var s = LettoreDecisione.Leggi(json, ["dormi", "rispondi"])!;
        Assert.Equal(0.91, s.Sicurezza, 3);
        Assert.False(s.ChiediConferma);
    }

    [Fact]
    public void Il_catalogo_ha_le_sei_azioni()
    {
        var nomi = CatalogoAzioni.Predefinito().Azioni.Select(a => a.Nome).ToArray();
        Assert.Equal(["rispondi", "crea_nota", "ricorda", "dimentica", "mostra_creazioni", "dormi"], nomi);
        var riletto = CatalogoAzioni.Leggi(CatalogoAzioni.Predefinito().AJson());
        Assert.Equal(nomi, riletto.Azioni.Select(a => a.Nome).ToArray());
        Assert.Contains("note", riletto.Trova("crea_nota")!.Schema);
    }

    [Fact]
    public void Riconosce_i_sei_modelli_e_il_mmproj()
    {
        var dir = Temporanea();
        void Finto(string nome) => File.WriteAllBytes(Path.Combine(dir, nome), [1, 2, 3]);
        Finto("LFM3-1.7B-Q4_K_M.gguf");
        Finto("LFM3-VL-2B-Q4_K_M.gguf");
        Finto("LFM3-Audio-2B-Q4_K_M.gguf");
        Finto("LFM3-Omni-2B-Q4_K_M.gguf");
        Finto("mmproj-LFM3-Omni-2B-Q8_0.gguf");
        Finto("d1-2B-Q4_K_M.gguf");
        Finto("mmproj-d1-2B-Q8_0.gguf");
        Finto("d1-Omni-2B-Q8_0.gguf");
        Finto("mmproj-d1-Omni-2B-Q8_0.gguf");
        var schede = RilevatoreModelli.LeggiGguf(dir);
        Assert.Equal(6, schede.Count);
        Assert.Equal(Path.Combine(dir, "mmproj-LFM3-Omni-2B-Q8_0.gguf"), schede.Single(s => s.Ruolo == RuoloModello.Omni).Mmproj);
        Assert.Equal(Path.Combine(dir, "mmproj-d1-2B-Q8_0.gguf"), schede.Single(s => s.Ruolo == RuoloModello.D1).Mmproj);
        Assert.Null(schede.Single(s => s.Ruolo == RuoloModello.Mani).Mmproj);
        Assert.Equal("liquidino-mani", schede.Single(s => s.Ruolo == RuoloModello.Mani).Id);
    }

    [Fact]
    public void Trova_llama_server_prima_di_llama_exe()
    {
        var dir = Temporanea();
        var sotto = Path.Combine(dir, "llama");
        Directory.CreateDirectory(sotto);
        File.WriteAllText(Path.Combine(sotto, "llama.exe"), "");
        File.WriteAllText(Path.Combine(sotto, "llama-server.exe"), "");
        var trovato = RilevatoreModelli.TrovaEseguibile(dir);
        Assert.Equal(Path.Combine(sotto, "llama-server.exe"), trovato);
        var argomenti = RilevatoreModelli.Argomenti(trovato!, 8094, @"C:\tmp\modelli.ini");
        Assert.Contains("--host 127.0.0.1", argomenti);
        Assert.Contains("--models-max 2", argomenti);
        Assert.Contains("--no-models-autoload", argomenti);
        Assert.DoesNotContain("serve ", argomenti);
        var tramite = RilevatoreModelli.Argomenti(Path.Combine(sotto, "llama.exe"), 8094, "p.ini");
        Assert.StartsWith("serve ", tramite);
    }

    [Fact]
    public void Il_preset_ha_cache_q4_flash_attention_e_128k()
    {
        var dir = Temporanea();
        var imp = new ImpostazioniUtente();
        imp.ApplicaContesto(SceltaContesto.Scegli(new InfoMacchina(InfoMacchina.Gb(63), 8192L * 1024 * 1024)));
        var schede = new[]
        {
            new SchedaModello(RuoloModello.Mani, "liquidino-mani", Path.Combine(dir, "mani.gguf"), null, "mani.gguf"),
            new SchedaModello(RuoloModello.Omni, "liquidino-omni", Path.Combine(dir, "omni.gguf"), Path.Combine(dir, "mm.gguf"), "omni.gguf"),
            new SchedaModello(RuoloModello.D1, "liquidino-d1", Path.Combine(dir, "d1.gguf"), null, "d1.gguf")
        };
        var ini = PresetMotore.Scrivi(Path.Combine(dir, "modelli.ini"), Path.Combine(dir, "slot"), schede, imp);
        Assert.Contains("cache-type-k = q4_0", ini);
        Assert.Contains("cache-type-v = q4_0", ini);
        Assert.Contains("flash-attn = on", ini);
        Assert.Contains("parallel = 1", ini);
        Assert.Contains("ctx-size = 131072", ini);
        Assert.Contains("reasoning = off", ini);
        Assert.Contains("batch-size = 8192", ini);
        Assert.Contains("[liquidino-omni]", ini);
        Assert.DoesNotContain("0.0.0.0", ini);
    }

    [Fact]
    public void Mai_piu_di_due_modelli_insieme()
    {
        var accesi = new[] { RuoloModello.D1, RuoloModello.Omni };
        var piano = PoliticaCarico.Calcola(accesi, [RuoloModello.Mani]);
        Assert.Equal(2, piano.Spegni.Count);
        Assert.Equal([RuoloModello.Mani], piano.Accendi);
        Assert.Throws<InvalidOperationException>(() =>
            PoliticaCarico.Calcola([], [RuoloModello.D1, RuoloModello.Omni, RuoloModello.Mani]));
        var resta = PoliticaCarico.Calcola(accesi, [RuoloModello.D1, RuoloModello.Omni]);
        Assert.Empty(resta.Spegni);
        Assert.Empty(resta.Accendi);
    }

    [Fact]
    public void Contesto_128k_sul_pc_di_prova_e_corto_se_non_si_vede_la_scheda()
    {
        var grande = SceltaContesto.Scegli(new InfoMacchina(InfoMacchina.Gb(63), 8192L * 1024 * 1024));
        Assert.Equal(131072, grande.Mani);
        Assert.Equal(32768, grande.Omni);
        Assert.Equal(16384, grande.D1);
        var cieco = SceltaContesto.Scegli(new InfoMacchina(InfoMacchina.Gb(4), null));
        Assert.Equal(8192, cieco.Mani);
        Assert.Contains("scheda video", cieco.Frase);
    }

    [Fact]
    public void La_nota_resta_nel_mondo_e_il_percorso_esterno_no()
    {
        var radice = Temporanea();
        var mondo = new Mondo(radice);
        var nota = mondo.CreaNota("Pane e latte", "Comprare il pane.", "note");
        Assert.True(Percorsi.StaDentro(mondo.Radice, nota.Percorso));
        Assert.Contains("Pane", File.ReadAllText(nota.Percorso));
        var fuori = Path.Combine(Path.GetTempPath(), "fuori-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(fuori, "segreto");
        Assert.Null(mondo.NelCestino(fuori));
        Assert.True(File.Exists(fuori));
        File.Delete(fuori);
        Assert.False(Percorsi.StaDentro(mondo.Radice, Path.Combine(mondo.Radice, "..", "segreto.txt")));
        var cestino = mondo.NelCestino(nota.Percorso);
        Assert.NotNull(cestino);
        Assert.Contains("cestino", cestino!.Replace('\\', '/'));
    }

    [Fact]
    public void Ricorda_e_dimentica_senza_mettere_il_testo_nel_registro()
    {
        var radice = Temporanea();
        var mondo = new Mondo(radice);
        var registro = new Registro(radice);
        mondo.Ricorda("il colore preferito è il viola");
        Assert.Contains("viola", mondo.LeggiMemoria());
        var tolte = mondo.Dimentica("viola");
        Assert.Equal(1, tolte);
        Assert.DoesNotContain("viola", mondo.LeggiMemoria());
        registro.Scrivi("memoria", "cancellato un ricordo", "ok", "dimentica");
        var log = File.ReadAllText(Directory.GetFiles(Percorsi.Registro(radice)).Single(f => f.EndsWith(".jsonl")));
        Assert.DoesNotContain("viola", log);
        Assert.Contains("cancellato un ricordo", log);
    }

    [Fact]
    public async Task Sotto_soglia_chiede_conferma_poi_scrive_la_nota()
    {
        var radice = Temporanea();
        var imp = Impostazioni(radice);
        var mondo = new Mondo(radice);
        mondo.Prepara();
        var registro = new Registro(radice);
        var finto = new RegistaFinto { MotorePresente = true };
        finto.Presenti.Add(RuoloModello.D1);
        finto.SuDecidi = _ => new SceltaAzione("crea_nota", 0.45, true, new Dictionary<string, double>(), "d1");
        var dialogo = new Dialogo(imp, mondo, registro, finto, CatalogoAzioni.Predefinito());
        var prima = await dialogo.Messaggio("forse una nota", CancellationToken.None);
        Assert.True(prima.ChiestaConferma);
        Assert.Contains("0,45", prima.Testo);
        Assert.DoesNotContain(mondo.Elenca(), v => v.Cartella == "note");
        var dopo = await dialogo.Messaggio("sì", CancellationToken.None);
        Assert.False(dopo.ChiestaConferma);
        Assert.NotNull(dopo.FileCreato);
        Assert.True(File.Exists(dopo.FileCreato));
    }

    [Fact]
    public async Task Senza_motore_risponde_e_dice_di_farlo_tu()
    {
        var radice = Temporanea();
        var dialogo = new Dialogo(Impostazioni(radice), new Mondo(radice), new Registro(radice), new RegistaFinto(), CatalogoAzioni.Predefinito());
        var esito = await dialogo.Messaggio("ciao, come stai?", CancellationToken.None);
        Assert.Contains("Questo fallo tu", esito.Testo);
        Assert.False(esito.VuoiVoce);
    }

    [Fact]
    public async Task Il_permesso_del_pc_non_fa_partire_nessuna_azione()
    {
        var radice = Temporanea();
        var imp = Impostazioni(radice);
        imp.PermessoPc = true;
        var dialogo = new Dialogo(imp, new Mondo(radice), new Registro(radice), new RegistaFinto(), CatalogoAzioni.Predefinito());
        var esito = await dialogo.Messaggio("clicca sul pulsante di salva", CancellationToken.None);
        Assert.True(esito.FalloTu);
        Assert.Contains("mani", esito.Testo);
    }

    [Fact]
    public async Task Di_notte_il_ciclo_non_scrive_e_di_giorno_si()
    {
        var radice = Temporanea();
        var imp = Impostazioni(radice);
        imp.CicloAutonomo = true;
        imp.PrimoAvvio = DateTimeOffset.Now;
        var mondo = new Mondo(radice);
        mondo.Prepara();
        var ciclo = new CicloAutonomo(imp, mondo, new Registro(radice), new RegistaFinto());
        var notte = await ciclo.Gira(new DateTimeOffset(2026, 11, 3, 23, 30, 0, TimeSpan.Zero), false, CancellationToken.None);
        Assert.False(notte.Fatto);
        var giorno = await ciclo.Gira(new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero), false, CancellationToken.None);
        Assert.True(giorno.Fatto);
        Assert.NotNull(giorno.File);
        Assert.Contains("amicizia", File.ReadAllText(giorno.File!).ToLowerInvariant());
        var occupato = await ciclo.Gira(new DateTimeOffset(2026, 11, 3, 11, 0, 0, TimeSpan.Zero), true, CancellationToken.None);
        Assert.False(occupato.Fatto);
    }

    [Fact]
    public void Pulisce_i_ragionamenti_e_l_orecchio_non_e_pronto()
    {
        Assert.Equal("Ciao.", Testo.PulisciRisposta("<think>piano</think>\nCiao."));
        var stato = OrecchioLeggero.Stato(Temporanea());
        Assert.False(stato.Pronto);
        Assert.Contains("Ehi Liquidino", stato.Messaggio);
        Assert.Equal("0,60", Testo.FormatoVirgola(0.6));
    }

    [Fact]
    public void Dopo_dodici_errori_propone_l_evoluzione_una_volta_al_giorno()
    {
        var registro = new Registro(Temporanea());
        var avvisi = 0;
        for (var i = 0; i < 13; i++)
            if (registro.SegnaErrore("prova " + i)) avvisi++;
        Assert.Equal(1, avvisi);
        Assert.Equal(13, registro.Errori);
    }

    [Fact]
    public void Il_catalogo_e_il_prompt_nel_repository_si_leggono()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Liquidino.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var cat = CatalogoAzioni.Carica(Path.Combine(dir!.FullName, "risorse", "catalogo", "azioni.json"));
        Assert.Equal(6, cat.Azioni.Count);
        Assert.Contains("{{carattere}}", File.ReadAllText(Path.Combine(dir.FullName, "risorse", "prompt", "sistema.md")));
    }

    [Fact]
    public async Task Permesso_on_off_e_due_stop_non_restano_appesi()
    {
        var comandi = new ComandiLocali();
        comandi.AlternaPermesso();
        Assert.True(comandi.Permesso);
        comandi.AlternaPermesso();
        Assert.False(comandi.Permesso);

        var entrato = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rilascio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var primo = comandi.StopAsync(async ct =>
        {
            entrato.TrySetResult();
            await rilascio.Task.WaitAsync(ct);
        }, TimeSpan.FromSeconds(4));

        await entrato.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var secondo = comandi.StopAsync(_ => Task.CompletedTask, TimeSpan.FromSeconds(4));
        var chiVince = await Task.WhenAny(secondo, Task.Delay(1500));
        Assert.Same(secondo, chiVince);

        rilascio.TrySetResult();
        await primo.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Il_preset_non_contiene_hf_repo_ne_indirizzi()
    {
        var sporco = """
            version = 1
            [*]
            flash-attn = on
            hf-repo = handy-computer/nemotron-3.5-asr-streaming-0.6b-gguf

            [handy-computer/nemotron-3.5-asr-streaming-0.6b-gguf]
            hf-repo = handy-computer/nemotron-3.5-asr-streaming-0.6b-gguf

            [liquidino-mani]
            model = C:/modelli/LFM3-1.7B-Q4_K_M.gguf
            """;
        var pulito = PresetMotore.SenzaRete(sporco);
        Assert.Contains("[liquidino-mani]", pulito);
        Assert.Contains("flash-attn = on", pulito);
        Assert.DoesNotContain("hf-repo", pulito);
        Assert.DoesNotContain("nemotron", pulito);
        Assert.DoesNotContain("http", pulito);
        var argomenti = RilevatoreModelli.Argomenti("llama-server.exe", 8094, "modelli.ini");
        Assert.Contains("--offline", argomenti);
    }

    [Fact]
    public void Il_decisore_locale_capisce_le_parole_semplici()
    {
        Assert.Equal("crea_nota", DecisoreLocale.Scegli("Liquidino, prendi nota: pane e latte").Azione);
        Assert.Equal("dimentica", DecisoreLocale.Scegli("dimentica il dentista").Azione);
        Assert.Equal("mostra_creazioni", DecisoreLocale.Scegli("fammi vedere cosa hai creato").Azione);
        Assert.Equal("rispondi", DecisoreLocale.Scegli("che tempo fa").Azione);
    }

    static string Temporanea()
    {
        var dir = Path.Combine(Path.GetTempPath(), "liquidino-prove-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    static ImpostazioniUtente Impostazioni(string radice)
    {
        var imp = new ImpostazioniUtente { PercorsoFile = Path.Combine(radice, "impostazioni.json") };
        return imp;
    }

    sealed class RegistaFinto : IRegista
    {
        public bool MotorePresente { get; set; }
        public HashSet<RuoloModello> Presenti { get; } = [];
        public List<RuoloModello> AccesiLista { get; } = [];
        public IReadOnlyCollection<RuoloModello> Accesi => AccesiLista;
        public Func<string, SceltaAzione?>? SuDecidi { get; set; }

        public bool Presente(RuoloModello ruolo) => Presenti.Contains(ruolo);
        public string Descrizione() => "finto";

        public Task<bool> Assicura(IReadOnlyList<RuoloModello> ruoli, CancellationToken ct)
        {
            if (ruoli.Distinct().Count() > 2) throw new InvalidOperationException("Mai più di due modelli insieme.");
            AccesiLista.Clear();
            AccesiLista.AddRange(ruoli);
            return Task.FromResult(MotorePresente && ruoli.All(Presente));
        }

        public Task ScaricaTutto(CancellationToken ct)
        {
            AccesiLista.Clear();
            return Task.CompletedTask;
        }

        public Task Ferma() => ScaricaTutto(CancellationToken.None);

        public Task<SceltaAzione?> Decidi(string stato, CatalogoAzioni catalogo, CancellationToken ct) =>
            Task.FromResult(SuDecidi?.Invoke(stato));

        public Task<RispostaChat?> Completa(RichiestaChat richiesta, CancellationToken ct) =>
            Task.FromResult<RispostaChat?>(new RispostaChat("{}", null));

        public Task<string?> Trascrivi(byte[] wav, CancellationToken ct) => Task.FromResult<string?>("ciao");
        public Task<byte[]?> Sintetizza(string testo, string voce, CancellationToken ct) => Task.FromResult<byte[]?>(null);
    }
}
