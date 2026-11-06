using Liquidino.Nucleo;
using Xunit;

namespace Liquidino.Prove;

public class ProveCreazioni
{
    [Fact]
    public void Filtri_per_cartella_e_ricerca_nel_nome_e_dentro()
    {
        var radice = Aiuti.Temporanea();
        var mondo = new Mondo(radice);
        mondo.CreaNota("Pane e latte", "Comprare il pane.", "note");
        mondo.CreaNota("Il drago", "C'era una volta un drago gentile.", "storie");
        mondo.CreaNota("Pensiero", "Oggi il cielo è viola.", "idee");
        var voci = mondo.Elenca();

        Assert.Single(FiltroMondo.Applica(voci, "storie", null));
        Assert.Equal(voci.Count, FiltroMondo.Applica(voci, FiltroMondo.Tutte, "").Count);
        Assert.Contains(FiltroMondo.Applica(voci, null, "PANE"), v => v.Cartella == "note");
        // dentro il file, senza badare agli accenti
        var viola = FiltroMondo.Applica(voci, FiltroMondo.Tutte, "cielo e viola");
        Assert.Equal("idee", Assert.Single(viola).Cartella);
        Assert.Empty(FiltroMondo.Applica(voci, "note", "drago"));
    }

    [Fact]
    public void L_anteprima_legge_solo_dentro_il_mondo()
    {
        var radice = Aiuti.Temporanea();
        var mondo = new Mondo(radice);
        var nota = mondo.CreaNota("Lunga", new string('a', 2000), "note");
        var anteprima = mondo.Anteprima(nota.Percorso, 100)!;
        Assert.StartsWith("# Lunga", anteprima);
        Assert.EndsWith("…", anteprima);
        Assert.True(anteprima.Length <= 101);

        var fuori = Path.Combine(Aiuti.Temporanea(), "segreto.txt");
        File.WriteAllText(fuori, "segreto");
        Assert.Null(mondo.Anteprima(fuori));
        Assert.Null(mondo.Anteprima(Path.Combine(mondo.Radice, "..", "..", "segreto.txt")));

        var immagine = Path.Combine(mondo.Radice, "note", "foto.png");
        File.WriteAllBytes(immagine, new byte[3000]);
        Assert.StartsWith("File PNG", mondo.Anteprima(immagine));
    }

    [Fact]
    public void Il_titolo_si_legge_senza_la_data()
    {
        Assert.Equal("Pane e latte", Mondo.TitoloDi("2026-11-04-153012-Pane e latte.md"));
        Assert.Equal("cosa-so-di-te.md", Mondo.TitoloDi("cosa-so-di-te.md"));
    }

    [Fact]
    public void I_voti_restano_e_fuori_dal_mondo_non_si_vota()
    {
        var radice = Aiuti.Temporanea();
        var mondo = new Mondo(radice);
        var voti = new VotiMondo(mondo);
        var a = mondo.CreaNota("Uno", "Primo pensiero gentile.", "idee");
        var b = mondo.CreaNota("Due", "Secondo pensiero.", "idee");
        Assert.True(voti.Vota(a.Percorso, 1));
        Assert.True(voti.Vota(b.Percorso, -5));
        Assert.Equal(1, new VotiMondo(mondo).Voto(a.Percorso));
        Assert.Equal(-1, new VotiMondo(mondo).Voto(b.Percorso));
        Assert.Equal([a.Percorso], voti.Piaciute(3));
        voti.Vota(a.Percorso, 0);
        Assert.Null(voti.Voto(a.Percorso));

        var fuori = Path.Combine(Aiuti.Temporanea(), "x.md");
        File.WriteAllText(fuori, "x");
        Assert.False(voti.Vota(fuori, 1));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(fuori)!, "voti.json")));
    }

    [Fact]
    public async Task Il_diario_conta_la_giornata_senza_le_tue_frasi()
    {
        var radice = Aiuti.Temporanea();
        var mondo = new Mondo(radice);
        var registro = new Registro(radice);
        var dialogo = new Dialogo(Aiuti.Impostazioni(radice), mondo, registro, new RegistaFinto(), CatalogoAzioni.Predefinito());
        await dialogo.Messaggio("prendi nota: il regalo per Giulia", CancellationToken.None);
        await dialogo.Messaggio("ricordati che amo le castagne", CancellationToken.None);
        await dialogo.Messaggio("ciao", CancellationToken.None);
        registro.SegnaErrore("una prova");

        var oggi = DateOnly.FromDateTime(DateTime.Now);
        var conti = Diario.Conta(radice, oggi);
        Assert.Equal(3, conti.Turni);
        Assert.Equal(1, conti.Note);
        Assert.Equal(1, conti.Ricordi);
        Assert.Equal(1, conti.Errori);

        var percorso = Diario.Scrivi(mondo, oggi, DateTimeOffset.Now);
        Assert.True(Percorsi.StaDentro(mondo.Radice, percorso));
        var testo = File.ReadAllText(percorso);
        Assert.Contains("Abbiamo parlato 3 volte", testo);
        Assert.Contains("Note scritte: 1", testo);
        Assert.Contains("note: il regalo per Giulia", testo); // il nome del file creato, non la chat
        Assert.DoesNotContain("castagne", testo);
        Assert.Contains(mondo.Elenca(), v => v.Cartella == "diario");
    }

    [Fact]
    public void Di_notte_il_diario_racconta_ieri()
    {
        var mattina = new DateTimeOffset(2026, 11, 5, 2, 30, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 11, 4), Diario.GiornoDaRaccontare(mattina, 7));
        var sera = new DateTimeOffset(2026, 11, 4, 23, 5, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 11, 4), Diario.GiornoDaRaccontare(sera, 7));
    }

    [Fact]
    public async Task Il_rifacimento_mette_il_vecchio_nel_cestino_e_scrive_il_nuovo()
    {
        var radice = Aiuti.Temporanea();
        var mondo = new Mondo(radice);
        var regista = new RegistaFinto { MotorePresente = true, SuCompleta = _ => new RispostaChat("Un pensiero nuovo, più allegro e corto.", null) };
        regista.Presenti.Add(RuoloModello.Mani);
        var ciclo = new CicloAutonomo(Aiuti.Impostazioni(radice), mondo, new Registro(radice), regista);
        var vecchio = mondo.CreaNota("Pensiero", "Un pensiero un po' triste.", "idee");
        ciclo.Voti.Vota(vecchio.Percorso, -1);

        var esito = await ciclo.Rifai(vecchio.Percorso, "più allegro", CancellationToken.None);
        Assert.True(esito.Fatto, esito.Motivo);
        Assert.False(File.Exists(vecchio.Percorso));
        Assert.Contains(mondo.Elenca(), v => v.Cartella == "cestino");
        Assert.Contains("più allegro e corto", File.ReadAllText(esito.File!));
        var richiesta = Assert.Single(regista.Richieste);
        Assert.Contains("un po' triste", richiesta.Utente);
        Assert.Contains("più allegro", richiesta.Utente);
        Assert.DoesNotContain("più allegro", Aiuti.RegistroDiOggi(radice));
    }

    [Fact]
    public async Task Le_note_dettate_non_si_rifanno_e_senza_mani_lo_dice()
    {
        var radice = Aiuti.Temporanea();
        var mondo = new Mondo(radice);
        var ciclo = new CicloAutonomo(Aiuti.Impostazioni(radice), mondo, new Registro(radice), new RegistaFinto());
        var nota = mondo.CreaNota("Pane", "Comprare il pane.", "note");
        var no = await ciclo.Rifai(nota.Percorso, "più corto", CancellationToken.None);
        Assert.False(no.Fatto);
        Assert.Contains("note", no.Motivo);
        Assert.True(File.Exists(nota.Percorso));

        var idea = mondo.CreaNota("Pensiero", "Un pensiero.", "idee");
        var senza = await ciclo.Rifai(idea.Percorso, "più corto", CancellationToken.None);
        Assert.False(senza.Fatto);
        Assert.Contains("mani", senza.Motivo);
        Assert.True(File.Exists(idea.Percorso));
    }

    [Fact]
    public async Task I_pensieri_piaciuti_diventano_esempio_per_quelli_nuovi()
    {
        var radice = Aiuti.Temporanea();
        var mondo = new Mondo(radice);
        var imp = Aiuti.Impostazioni(radice);
        imp.PrimoAvvio = DateTimeOffset.Now;
        var regista = new RegistaFinto { MotorePresente = true, SuCompleta = _ => new RispostaChat("Oggi resto qui accanto a te, in silenzio.", null) };
        regista.Presenti.Add(RuoloModello.Mani);
        var ciclo = new CicloAutonomo(imp, mondo, new Registro(radice), regista);
        var bello = mondo.CreaNota("Pensiero", "La luce del mattino sul tavolo.", "idee");
        ciclo.Voti.Vota(bello.Percorso, 1);
        var esito = await ciclo.Gira(new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.Zero), false, CancellationToken.None);
        Assert.True(esito.Fatto);
        Assert.Contains("La luce del mattino", Assert.Single(regista.Richieste).Utente);
    }
}

public class ProveNomiNote
{
    [Fact]
    public void Due_note_uguali_nello_stesso_secondo_non_si_sovrascrivono()
    {
        var mondo = new Mondo(Aiuti.Temporanea());
        var a = mondo.CreaNota("Pensiero", "primo", "idee");
        var b = mondo.CreaNota("Pensiero", "secondo", "idee");
        var c = mondo.CreaNota("Pensiero", "terzo", "idee");
        Assert.Equal(3, new[] { a.Percorso, b.Percorso, c.Percorso }.Distinct().Count());
        Assert.Contains("primo", File.ReadAllText(a.Percorso));
        Assert.Contains("secondo", File.ReadAllText(b.Percorso));
        Assert.Contains("terzo", File.ReadAllText(c.Percorso));
    }
}

public class ProvePensieroChiesto
{
    [Fact]
    public async Task Il_pensiero_chiesto_passa_anche_col_ciclo_spento_e_il_pc_occupato()
    {
        var radice = Aiuti.Temporanea();
        var imp = Aiuti.Impostazioni(radice);
        imp.CicloAutonomo = false;
        imp.PrimoAvvio = DateTimeOffset.Now;
        var ciclo = new CicloAutonomo(imp, new Mondo(radice), new Registro(radice), new RegistaFinto());
        var notte = new DateTimeOffset(2026, 11, 4, 23, 30, 0, TimeSpan.Zero);
        Assert.False((await ciclo.Gira(notte, true, CancellationToken.None)).Fatto);
        var chiesto = await ciclo.Gira(notte, true, CancellationToken.None, richiesto: true);
        Assert.True(chiesto.Fatto);
        Assert.True(chiesto.Parla); // chiesto da te: lo legge ad alta voce, se la voce è accesa
    }
}
