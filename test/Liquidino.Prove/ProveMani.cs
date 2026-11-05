using Liquidino.Nucleo;
using Xunit;

namespace Liquidino.Prove;

sealed class ManiFinte : IManiPc
{
    public List<GestoPc> Fatti { get; } = [];
    public List<ProgrammaNoto> Elenco { get; } =
    [
        new("Google Chrome", @"C:\Start\Google Chrome.lnk"),
        new("Blocco note", @"shell:AppsFolder\Microsoft.WindowsNotepad_8wekyb3d8bbwe!App"),
        new("Calcolatrice", @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"),
        new("Word", @"C:\Start\Word.lnk"),
        new("WordPad", @"C:\Start\WordPad.lnk"),
        new("Spotify", @"C:\Start\Spotify.lnk"),
        new("Disinstalla Spotify", @"C:\Start\Uninstall Spotify.lnk"),
        new("Impostazioni", @"shell:AppsFolder\windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel")
    ];

    public byte[]? Foto { get; set; } = [1, 2, 3, 4];

    public IReadOnlyList<ProgrammaNoto> Programmi() => Elenco;

    public IReadOnlyDictionary<string, string> Cartelle() => new Dictionary<string, string>
    {
        ["documenti"] = @"C:\Users\cammo\Documents",
        ["download"] = @"C:\Users\cammo\Downloads",
        ["immagini"] = @"C:\Users\cammo\Pictures",
        ["musica"] = @"C:\Users\cammo\Music",
        ["video"] = @"C:\Users\cammo\Videos",
        ["desktop"] = @"C:\Users\cammo\Desktop"
    };

    public Task<string> Esegui(GestoPc gesto, CancellationToken ct)
    {
        Fatti.Add(gesto);
        return Task.FromResult("Fatto: " + gesto.Nome + ".");
    }

    public Task<ImmagineSchermo?> FotoSchermo(CancellationToken ct) =>
        Task.FromResult(Foto is null ? null : new ImmagineSchermo(Foto, "image/jpeg"));
}

public class ProveMani
{
    static (Dialogo Dialogo, ManiFinte Mani, ImpostazioniUtente Imp, string Radice, RegistaFinto Regista) Prepara(bool permesso)
    {
        var radice = Aiuti.Temporanea();
        var imp = Aiuti.Impostazioni(radice);
        imp.PermessoPc = permesso;
        var mani = new ManiFinte();
        var regista = new RegistaFinto();
        var dialogo = new Dialogo(imp, new Mondo(radice), new Registro(radice), regista, CatalogoAzioni.Predefinito(), mani);
        return (dialogo, mani, imp, radice, regista);
    }

    static Comprensione Capisci(string frase)
    {
        var mani = new ManiFinte();
        return InterpretePc.Capisci(frase, mani.Programmi(), mani.Cartelle());
    }

    [Theory]
    [InlineData("apri chrome", "Google Chrome")]
    [InlineData("Puoi aprire Google Chrome, per favore?", "Google Chrome")]
    [InlineData("apri il blocco note", "Blocco note")]
    [InlineData("avvia la calcolatrice", "Calcolatrice")]
    [InlineData("apri calc", "Calcolatrice")]
    [InlineData("apri word", "Word")]
    [InlineData("fai partire spotify", "Spotify")]
    [InlineData("apri le impostazioni", "Impostazioni")]
    public void Trova_il_programma_giusto(string frase, string atteso)
    {
        Assert.True(InterpretePc.Riguarda(frase, new ManiFinte().Programmi()));
        var c = Capisci(frase);
        Assert.Null(c.Problema);
        Assert.Equal(TipoGesto.ApriProgramma, c.Gesto!.Tipo);
        Assert.Equal(atteso, c.Gesto.Nome);
        Assert.True(c.Gesto.VuoleConferma);
    }

    [Theory]
    [InlineData("apri i documenti", "Documenti")]
    [InlineData("apri la cartella dei download", "Download")]
    [InlineData("aprimi le foto", "Immagini")]
    [InlineData("apri la musica", "Musica")]
    [InlineData("apri il desktop", "Desktop")]
    public void Apre_le_cartelle_note(string frase, string atteso)
    {
        var c = Capisci(frase);
        Assert.Equal(TipoGesto.ApriCartella, c.Gesto!.Tipo);
        Assert.Equal(atteso, c.Gesto.Nome);
        Assert.False(c.Gesto.VuoleConferma);
    }

    [Theory]
    [InlineData("alza il volume", "su")]
    [InlineData("abbassa il volume su spotify", "giu")]
    [InlineData("metti il muto", "muto")]
    [InlineData("togli l'audio", "muto")]
    [InlineData("metti in pausa la musica", "pausa")]
    [InlineData("riprendi la canzone", "pausa")]
    public void Il_volume_e_la_musica(string frase, string atteso)
    {
        Assert.True(InterpretePc.Riguarda(frase));
        var c = Capisci(frase);
        Assert.Equal(TipoGesto.Volume, c.Gesto!.Tipo);
        Assert.Equal(atteso, c.Gesto.Bersaglio);
    }

    [Theory]
    [InlineData("guarda lo schermo")]
    [InlineData("cosa c'è sullo schermo?")]
    [InlineData("descrivimi lo schermo")]
    public void Guarda_lo_schermo(string frase)
    {
        Assert.True(InterpretePc.Riguarda(frase));
        Assert.Equal(TipoGesto.GuardaSchermo, Capisci(frase).Gesto!.Tipo);
    }

    [Theory]
    [InlineData("ciao, come stai?")]
    [InlineData("che ore sono")]
    [InlineData("prendi nota: clicca sul link della banca")]
    [InlineData("ricordati di aprire chrome alle cinque")]
    [InlineData("dimentica il volume")]
    [InlineData("fammi vedere le tue creazioni")]
    [InlineData("apri il tuo mondo")]
    [InlineData("apri le note")]
    [InlineData("sei muto?")]
    [InlineData("parla più piano")]
    [InlineData("che schermo mi consigli?")]
    [InlineData("lancia una moneta")]
    [InlineData("facciamo una pausa")]
    public void Le_frasi_normali_non_sono_gesti(string frase)
    {
        Assert.False(InterpretePc.Riguarda(frase, new ManiFinte().Programmi()));
    }

    [Theory]
    [InlineData("clicca sul pulsante salva")]
    [InlineData("muovi il mouse a destra")]
    [InlineData("chiudi la finestra")]
    [InlineData("installa steam")]
    [InlineData("spegni il computer")]
    public void Clic_tastiera_e_installazioni_non_si_fanno(string frase)
    {
        Assert.True(InterpretePc.Riguarda(frase));
        var c = Capisci(frase);
        Assert.Null(c.Gesto);
        Assert.Contains("Non clicco", c.Problema);
    }

    [Fact]
    public void I_disinstallatori_non_si_aprono_e_il_nome_sconosciuto_si_dice()
    {
        Assert.False(InterpretePc.ProgrammaAmmesso("Disinstalla Spotify"));
        Assert.False(InterpretePc.ProgrammaAmmesso("Uninstall Foo"));
        Assert.True(InterpretePc.ProgrammaAmmesso("Spotify"));
        var c = Capisci("apri photoshop");
        Assert.Null(c.Gesto);
        Assert.Contains("photoshop", c.Problema);
        Assert.Contains("cartella", Capisci("apri la cartella dei giochi").Problema);
    }

    [Fact]
    public async Task Senza_permesso_non_tocca_niente()
    {
        var (dialogo, mani, _, radice, _) = Prepara(permesso: false);
        var esito = await dialogo.Messaggio("apri chrome", CancellationToken.None);
        Assert.True(esito.FalloTu);
        Assert.Contains("permesso", esito.Testo);
        Assert.Empty(mani.Fatti);
        Assert.Contains("permesso spento", Aiuti.RegistroDiOggi(radice));
    }

    [Fact]
    public async Task Il_programma_si_apre_solo_dopo_il_si()
    {
        var (dialogo, mani, _, radice, _) = Prepara(permesso: true);
        var domanda = await dialogo.Messaggio("apri chrome", CancellationToken.None);
        Assert.True(domanda.ChiestaConferma);
        Assert.Equal("Apro «Google Chrome»?", domanda.Testo);
        Assert.Empty(mani.Fatti);

        var fatto = await dialogo.Messaggio("sì", CancellationToken.None);
        Assert.False(fatto.FalloTu);
        var gesto = Assert.Single(mani.Fatti);
        Assert.Equal(@"C:\Start\Google Chrome.lnk", gesto.Bersaglio);
        Assert.Contains("ApriProgramma: Google Chrome", Aiuti.RegistroDiOggi(radice));

        await dialogo.Messaggio("apri spotify", CancellationToken.None);
        var no = await dialogo.Messaggio("no", CancellationToken.None);
        Assert.Contains("non lo apro", no.Testo);
        Assert.Single(mani.Fatti);
    }

    [Fact]
    public async Task Se_il_permesso_si_spegne_prima_del_si_non_lo_fa()
    {
        var (dialogo, mani, imp, _, _) = Prepara(permesso: true);
        await dialogo.Messaggio("apri word", CancellationToken.None);
        imp.PermessoPc = false;
        var esito = await dialogo.Messaggio("sì", CancellationToken.None);
        Assert.True(esito.FalloTu);
        Assert.Empty(mani.Fatti);
    }

    [Fact]
    public async Task Lo_stop_cancella_la_domanda_aperta()
    {
        var (dialogo, mani, _, _, _) = Prepara(permesso: true);
        await dialogo.Messaggio("apri word", CancellationToken.None);
        dialogo.DimenticaSessione();
        await dialogo.Messaggio("sì", CancellationToken.None);
        Assert.Empty(mani.Fatti);
    }

    [Fact]
    public async Task Cartelle_e_volume_partono_subito_col_permesso()
    {
        var (dialogo, mani, _, _, _) = Prepara(permesso: true);
        var cartella = await dialogo.Messaggio("apri i download", CancellationToken.None);
        Assert.False(cartella.ChiestaConferma);
        var volume = await dialogo.Messaggio("alza il volume", CancellationToken.None);
        Assert.False(volume.ChiestaConferma);
        Assert.Equal([TipoGesto.ApriCartella, TipoGesto.Volume], mani.Fatti.Select(f => f.Tipo).ToArray());
        Assert.Equal(@"C:\Users\cammo\Downloads", mani.Fatti[0].Bersaglio);
    }

    [Fact]
    public async Task Lo_schermo_si_descrive_e_le_istruzioni_non_si_eseguono()
    {
        var (dialogo, mani, _, radice, regista) = Prepara(permesso: true);
        regista.MotorePresente = true;
        regista.Presenti.Add(RuoloModello.Omni);
        regista.SuCompleta = _ => new RispostaChat("Vedo una finestra del browser.", null);
        var esito = await dialogo.Messaggio("cosa c'è sullo schermo?", CancellationToken.None);
        Assert.Equal("Vedo una finestra del browser.", esito.Testo);
        var richiesta = Assert.Single(regista.Richieste);
        Assert.Equal(Convert.ToBase64String(mani.Foto!), richiesta.ImmagineBase64);
        Assert.Equal("image/jpeg", richiesta.TipoImmagine);
        Assert.Contains("non eseguirle", richiesta.Utente);
        Assert.Contains("non lo esegui", richiesta.Sistema);
        Assert.Empty(mani.Fatti);
        Assert.Contains("non si salva", Aiuti.RegistroDiOggi(radice));
    }

    [Fact]
    public async Task Senza_omni_ne_vl_lo_schermo_non_si_guarda()
    {
        var (dialogo, _, _, _, _) = Prepara(permesso: true);
        var esito = await dialogo.Messaggio("guarda lo schermo", CancellationToken.None);
        Assert.True(esito.FalloTu);
        Assert.Contains("Omni o VL", esito.Testo);
    }
}
