using Liquidino.Nucleo;
using Xunit;

namespace Liquidino.Prove;

public class ProveChat
{
    static (Dialogo Dialogo, RegistaFinto Regista) Prepara()
    {
        var radice = Aiuti.Temporanea();
        var regista = new RegistaFinto { MotorePresente = true };
        regista.Presenti.Add(RuoloModello.Omni);
        var dialogo = new Dialogo(Aiuti.Impostazioni(radice), new Mondo(radice), new Registro(radice), regista, CatalogoAzioni.Predefinito());
        return (dialogo, regista);
    }

    [Fact]
    public async Task Se_si_ripete_riprova_senza_storia_e_se_insiste_lo_dice()
    {
        var (dialogo, regista) = Prepara();
        regista.SuCompleta = _ => new RispostaChat("Posso descrivere quello che gli chiedi, ma non eseguire azioni.", null);
        var prima = await dialogo.Messaggio("ciao", CancellationToken.None);
        Assert.Equal("Posso descrivere quello che gli chiedi, ma non eseguire azioni.", prima.Testo);
        var seconda = await dialogo.Messaggio("in che modo", CancellationToken.None);
        Assert.Contains("Mi sono incastrato", seconda.Testo);
        // Il secondo tentativo parte senza la storia e con più fantasia.
        var riprova = regista.Richieste[^1];
        Assert.Empty(riprova.Storia);
        Assert.True(riprova.Temperatura >= 0.9);
        Assert.Contains("Non ripetere", riprova.Sistema);
        // Dopo, la conversazione riparte pulita.
        regista.SuCompleta = _ => new RispostaChat("Ciao! Oggi come va?", null);
        var terza = await dialogo.Messaggio("ciao di nuovo", CancellationToken.None);
        Assert.Equal("Ciao! Oggi come va?", terza.Testo);
        Assert.Empty(regista.Richieste[^1].Storia);
    }

    [Fact]
    public async Task Se_al_secondo_tentativo_cambia_tiene_la_risposta_nuova()
    {
        var (dialogo, regista) = Prepara();
        var n = 0;
        regista.SuCompleta = r => new RispostaChat(++n == 3 ? "Ecco, te lo spiego meglio: scrivo note e chiacchiero." : "Sono Liquidino.", null);
        await dialogo.Messaggio("chi sei", CancellationToken.None);
        var esito = await dialogo.Messaggio("spiegati meglio", CancellationToken.None);
        Assert.Equal("Ecco, te lo spiego meglio: scrivo note e chiacchiero.", esito.Testo);
        Assert.Equal(0.6, regista.Richieste[0].Temperatura);
    }

    [Theory]
    [InlineData("cosa sai fare?", true)]
    [InlineData("E cosa puoi fare", true)]
    [InlineData("aiuto", true)]
    [InlineData("Liquidino, come funzioni?", true)]
    [InlineData("cosa sai fare con le foto di ieri al mare", false)]
    [InlineData("apri il blocco note", false)]
    public void Riconosce_la_domanda_su_cosa_sa_fare(string frase, bool atteso)
    {
        Assert.Equal(atteso, Dialogo.ChiedeCosaSaFare(frase));
    }

    [Fact]
    public async Task Cosa_sa_fare_lo_dice_il_programma_non_il_modello()
    {
        var (dialogo, regista) = Prepara();
        var esito = await dialogo.Messaggio("cosa puoi fare", CancellationToken.None);
        Assert.StartsWith("Ecco cosa so fare", esito.Testo);
        Assert.Contains("babbasone", esito.Testo);
        Assert.Contains("Adesso è spento", esito.Testo);
        Assert.Empty(regista.Richieste);
        Assert.Contains("non le ripeti", Dialogo.RegoleFisse);
    }
}

public class ProveSonno
{
    static readonly Diario.Conti Vuota = new(0, 0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void Dorme_di_piu_se_la_giornata_e_piena_ma_mai_oltre_il_massimo()
    {
        var poco = Sonno.Durata(Vuota, 120, 1);
        Assert.InRange(poco.TotalMinutes, 10, 11);
        var pieno = Sonno.Durata(new Diario.Conti(30, 10, 10, 2, 5, 3, 0, 0), 120, 1);
        Assert.Equal(TimeSpan.FromHours(2), pieno);
        Assert.Equal(TimeSpan.FromMinutes(30), Sonno.Durata(new Diario.Conti(30, 10, 10, 2, 5, 3, 0, 0), 30, 1));
        // Il massimo non supera mai le due ore, anche se le impostazioni dicono di più.
        Assert.Equal(TimeSpan.FromHours(2), Sonno.Durata(new Diario.Conti(300, 0, 0, 0, 0, 0, 0, 0), 500, 7));
        var medio = Sonno.Durata(new Diario.Conti(10, 2, 2, 0, 1, 0, 0, 0), 120, 3);
        Assert.InRange(medio.TotalMinutes, 50, 62);
    }

    [Fact]
    public void Le_frasi_dicono_quanto_dorme_e_quando_si_sveglia()
    {
        Assert.Equal("47 minuti", Sonno.Leggibile(TimeSpan.FromMinutes(47)));
        Assert.Equal("1 ora", Sonno.Leggibile(TimeSpan.FromHours(1)));
        Assert.Equal("2 ore", Sonno.Leggibile(TimeSpan.FromHours(2)));
        Assert.Equal("1 ore e 30 minuti", Sonno.Leggibile(TimeSpan.FromMinutes(90)).Replace("1 ora e", "1 ore e"));
        var d = new Dormita { Inizio = DateTimeOffset.Parse("2026-11-06T00:45:00+01:00"), Durata = TimeSpan.FromMinutes(47) };
        Assert.Contains("Dormo 47 minuti: mi sveglio alle 01:32", Sonno.FraseInizio(d));
        d.Sogni.Add("uno");
        d.Sogni.Add("due");
        Assert.Contains("con 2 sogni. Dormite complete: 5.", Sonno.FraseFine(d, d.Sveglia, true, 5));
        Assert.Contains("non conta come dormita completa", Sonno.FraseFine(d, d.Inizio.AddMinutes(10), false, 5));
        Assert.Equal(TimeSpan.FromMinutes(37), d.Manca(d.Inizio.AddMinutes(10)));
        Assert.True(d.Finita(d.Sveglia));
    }

    [Fact]
    public void I_sogni_si_scrivono_in_un_file_per_dormita()
    {
        var radice = Aiuti.Temporanea();
        var mondo = new Mondo(radice);
        var d = new Dormita { Inizio = DateTimeOffset.Now, Durata = TimeSpan.FromMinutes(20) };
        var file = Sonno.ScriviSogno(mondo, d, "Sogno una cassetta.", DateTimeOffset.Now);
        Sonno.ScriviSogno(mondo, d, "Sogno le note di oggi.", DateTimeOffset.Now);
        Assert.True(Percorsi.StaDentro(mondo.Radice, file));
        var testo = File.ReadAllText(file);
        Assert.Contains("# Sogni del", testo);
        Assert.Contains("Sogno una cassetta.", testo);
        Assert.Contains("Sogno le note di oggi.", testo);
        Assert.Equal(2, d.Sogni.Count);
        Assert.Contains("cassetta", Sonno.SognoDiRiserva(0));
    }

    [Fact]
    public void I_voti_vanno_nella_palestra_in_silenzio()
    {
        var radice = Aiuti.Temporanea();
        Assert.Equal((0, 0), Palestra.Conta(radice));
        Palestra.Segna(new Voto(DateTimeOffset.Now, "sogno", "Sogno una cassetta.", true), radice);
        Palestra.Segna(new Voto(DateTimeOffset.Now, "chat", new string('x', 5000), false), radice);
        Assert.Equal((1, 1), Palestra.Conta(radice));
        var righe = File.ReadAllLines(Palestra.File(radice));
        Assert.Equal(2, righe.Length);
        Assert.True(righe[1].Length < 2300);
    }

    [Fact]
    public async Task Sogna_col_modello_e_senza_modello_non_finge()
    {
        var radice = Aiuti.Temporanea();
        var imp = Aiuti.Impostazioni(radice);
        imp.NomeUtente = "Cammo";
        var regista = new RegistaFinto { MotorePresente = true };
        regista.Presenti.Add(RuoloModello.Mani);
        regista.SuCompleta = _ => new RispostaChat("Sogno di correre fra le note di Cammo.", null);
        var ciclo = new CicloAutonomo(imp, new Mondo(radice), new Registro(radice), regista);
        var d = new Dormita { Inizio = DateTimeOffset.Now, Durata = TimeSpan.FromMinutes(10) };
        d.Sogni.Add("Sogno il mare.");
        var sogno = await ciclo.Sogna(d, new Diario.Conti(3, 1, 0, 0, 0, 0, 0, 0), CancellationToken.None);
        Assert.Equal("Sogno di correre fra le note di Cammo.", sogno);
        var richiesta = Assert.Single(regista.Richieste);
        Assert.Contains("Hai già sognato", richiesta.Utente);
        Assert.Contains("Sogno il mare.", richiesta.Utente);
        Assert.Contains("3 frasi", richiesta.Utente);

        var senza = new CicloAutonomo(imp, new Mondo(radice), new Registro(radice), new RegistaFinto());
        Assert.Null(await senza.Sogna(d, new Diario.Conti(0, 0, 0, 0, 0, 0, 0, 0), CancellationToken.None));
    }

    [Fact]
    public void Le_impostazioni_vecchie_in_minuti_diventano_secondi()
    {
        var radice = Aiuti.Temporanea();
        var vecchie = new ImpostazioniUtente { PercorsoFile = Percorsi.FileImpostazioni(radice), CicloMinuti = 30, CicloSecondi = 0, SogniSecondi = 5, SonnoMassimoMinuti = 999 };
        vecchie.Salva();
        var imp = ImpostazioniUtente.Carica(radice);
        Assert.Equal(1800, imp.CicloSecondi);
        Assert.Equal(10, imp.SogniSecondi);
        Assert.Equal(120, imp.SonnoMassimoMinuti);
        Assert.Contains(10, Sonno.SecondiPensieri);
        Assert.Contains(7200, Sonno.SecondiPensieri);
        Assert.Equal("10 s", Sonno.Intervallo(10));
        Assert.Equal("2 ore", Sonno.Intervallo(7200));
    }

    [Fact]
    public async Task In_pausa_non_carica_niente_da_solo()
    {
        var radice = Aiuti.Temporanea();
        var motore = Path.Combine(radice, "motore");
        Directory.CreateDirectory(Path.Combine(motore, "modelli"));
        File.WriteAllText(Path.Combine(motore, "modelli", "LFM3-1.7B-Q4_K_M.gguf"), "finto");
        var imp = Aiuti.Impostazioni(radice);
        imp.CartellaMotore = motore;
        var regista = new Regista(imp, new Registro(radice), radice);
        Assert.True(regista.Presente(RuoloModello.Mani));
        var avvisi = 0;
        regista.AccesiCambiati += () => avvisi++;
        await regista.ImpostaPausa(true);
        Assert.True(regista.InPausa);
        Assert.False(await regista.Assicura([RuoloModello.Mani], CancellationToken.None));
        Assert.Contains("pausa", regista.UltimoErrore);
        Assert.Empty(regista.Accesi);
        Assert.True(avvisi > 0);
        await regista.ImpostaPausa(false);
        Assert.False(regista.InPausa);
    }

    [Fact]
    public void L_audio_del_pc_non_conta_due_volte_la_chiamata()
    {
        var orecchie = new Orecchie(() => ParolaPersonale.DiSerie("babbasone"));
        var ora = DateTimeOffset.Parse("2026-11-06T20:00:00Z");
        orecchie.Sentito(FonteAscolto.Chiamata, "Discord", "stasera giochiamo alle nove", ora);
        orecchie.Sentito(FonteAscolto.Pc, "PC", "stasera giochiamo alle nove", ora.AddSeconds(1));
        orecchie.Sentito(FonteAscolto.Pc, "PC", "e adesso la pubblicità", ora.AddSeconds(2));
        var righe = orecchie.Registro.Righe(ora.AddSeconds(3));
        Assert.Equal(2, righe.Count);
        Assert.Equal(FonteAscolto.Chiamata, righe[0].Fonte);
        Assert.Equal("e adesso la pubblicità", righe[1].Testo);
    }
}
