namespace Liquidino.Nucleo;

public sealed record EsitoDialogo(
    string Testo,
    StatoCompagno Stato,
    bool ChiestaConferma,
    bool MostraMondo,
    bool FalloTu,
    bool VuoiVoce,
    bool ScaricaDopo,
    string? Trascrizione,
    string? FileCreato)
{
    public static EsitoDialogo Vuoto() => new("", StatoCompagno.Sveglio, false, false, false, false, false, null, null);
}

/// <summary>
/// Un turno di conversazione: d1 sceglie dal catalogo. La chiacchiera parte subito.
/// Sotto 0,6 si chiede conferma solo per le azioni che cambiano qualcosa.
/// </summary>
public sealed class Dialogo
{
    readonly ImpostazioniUtente _imp;
    readonly Mondo _mondo;
    readonly Registro _registro;
    readonly IRegista _regista;
    readonly CatalogoAzioni _catalogo;
    readonly List<(string Ruolo, string Testo)> _storia = [];
    (string Azione, string Testo, double Sicurezza)? _inAttesa;

    public Dialogo(ImpostazioniUtente imp, Mondo mondo, Registro registro, IRegista regista, CatalogoAzioni catalogo)
    {
        _imp = imp;
        _mondo = mondo;
        _registro = registro;
        _regista = regista;
        _catalogo = catalogo;
    }

    public void DimenticaSessione() => _storia.Clear();

    public async Task<EsitoDialogo> Messaggio(string testo, CancellationToken ct)
    {
        testo = (testo ?? "").Trim();
        if (testo.Length == 0) return EsitoDialogo.Vuoto();
        _registro.Scrivi("turno", "ricevuto: " + (testo.Length > 80 ? testo[..80] : testo), "ok");
        if (ct.IsCancellationRequested)
            return Chiudi("Turno annullato: è stato premuto STOP o Esc prima che partissi. Il modello non è stato caricato.", StatoCompagno.Sveglio, fallo: true, voce: false);

        if (Testo.ChiedeIlPc(testo))
        {
            var perche = _imp.PermessoPc
                ? "il tasto del permesso è acceso, ma le mani sul PC arrivano in una fase dopo. Per ora non clicco e non apro programmi"
                : "per usare il PC serve il permesso, col tasto vicino all'avatar, e comunque le mani arrivano dopo. Adesso non tocco niente fuori dal mio mondo";
            _registro.Scrivi("permesso", _imp.PermessoPc ? "permesso acceso, azione rifiutata" : "permesso spento", "rifiutato");
            return Chiudi(Testo.FalloTu(perche), StatoCompagno.Sveglio, fallo: true);
        }

        if (_inAttesa is { } attesa)
        {
            if (Testo.ESi(testo))
            {
                _inAttesa = null;
                _registro.Scrivi("conferma", "hai detto sì", "ok", attesa.Azione, attesa.Sicurezza);
                return await Esegui(attesa.Azione, attesa.Testo, attesa.Sicurezza, "confermata", ct);
            }
            if (Testo.ENo(testo))
            {
                _inAttesa = null;
                _registro.Scrivi("conferma", "hai detto no", "ok", attesa.Azione, attesa.Sicurezza);
                return Chiudi("Va bene, non lo faccio.", StatoCompagno.Sveglio);
            }
            _inAttesa = null;
        }

        SceltaAzione scelta;
        var parlante = _regista.Presente(RuoloModello.Omni) ? RuoloModello.Omni
            : _regista.Presente(RuoloModello.Mani) ? RuoloModello.Mani : (RuoloModello?)null;
        if (_regista.MotorePresente && (_regista.Presente(RuoloModello.D1) || parlante is not null))
        {
            try
            {
                var insieme = new List<RuoloModello>();
                if (_regista.Presente(RuoloModello.D1)) insieme.Add(RuoloModello.D1);
                if (parlante is { } p && !insieme.Contains(p)) insieme.Add(p);
                var acceso = await _regista.Assicura(insieme, ct);
                var letta = acceso && _regista.Presente(RuoloModello.D1)
                    ? await _regista.Decidi(testo, _catalogo, ct) : null;
                scelta = letta ?? DecisoreLocale.Scegli(testo);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                _registro.SegnaErrore("decisione: " + e.Message);
                scelta = DecisoreLocale.Scegli(testo);
            }
        }
        else scelta = DecisoreLocale.Scegli(testo);

        _registro.Scrivi("decisione", scelta.Fonte + " → " + scelta.Azione, "ok", scelta.Azione, scelta.Sicurezza);

        if (scelta.ChiediConferma && CatalogoAzioni.HaEffetti(scelta.Azione))
        {
            _inAttesa = (scelta.Azione, testo, scelta.Sicurezza);
            return new EsitoDialogo(DomandaConferma(scelta.Azione), StatoCompagno.Sveglio, true, false, false, true, false, null, null);
        }
        return await Esegui(scelta.Azione, testo, scelta.Sicurezza, scelta.Fonte, ct);
    }

    public async Task<EsitoDialogo> DaAudio(byte[] wav, CancellationToken ct)
    {
        if (wav.Length < 1000)
            return Chiudi(Testo.FalloTu("non ho sentito niente. Clicca di nuovo «Parla» e riprova."), StatoCompagno.Sveglio, fallo: true);
        if (!_regista.Presente(RuoloModello.Omni) || !_regista.MotorePresente)
            return Chiudi(Testo.FalloTu("per ascoltarti mi serve Omni, e non l'ho trovato nella cartella dei modelli."), StatoCompagno.Sveglio, fallo: true);
        string? detto;
        try { detto = await _regista.Trascrivi(wav, ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            var avvisa = _registro.SegnaErrore("trascrizione: " + e.Message);
            return Chiudi(AggiungiEvoluzione(Testo.FalloTu("non sono riuscito a trascrivere la voce. Puoi scrivermelo."), avvisa), StatoCompagno.Sveglio, fallo: true);
        }
        if (string.IsNullOrWhiteSpace(detto))
            return Chiudi(Testo.FalloTu("ho sentito un suono ma non ho capito le parole. Ripeti, o scrivilo."), StatoCompagno.Sveglio, fallo: true);
        var esito = await Messaggio(detto, ct);
        return esito with { Trascrizione = detto };
    }

    public async Task<EsitoDialogo> Guarda(string percorso, CancellationToken ct)
    {
        var ext = Path.GetExtension(percorso).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp"))
            return Chiudi(Testo.FalloTu("so guardare solo le immagini (png, jpg, webp). Questo file non lo è."), StatoCompagno.Sveglio, fallo: true);
        var info = new FileInfo(percorso);
        if (!info.Exists)
            return Chiudi(Testo.FalloTu("il file non c'è più."), StatoCompagno.Sveglio, fallo: true);
        if (info.Length > 8 * 1024 * 1024)
            return Chiudi(Testo.FalloTu("l'immagine supera gli 8 MB, per questa prova è troppo pesante."), StatoCompagno.Sveglio, fallo: true);
        var ruolo = _regista.Presente(RuoloModello.Omni) ? RuoloModello.Omni
            : _regista.Presente(RuoloModello.Vl) ? RuoloModello.Vl : (RuoloModello?)null;
        if (ruolo is null || !_regista.MotorePresente)
            return Chiudi(Testo.FalloTu("per vedere l'immagine mi serve Omni o VL, e non li ho trovati fra i modelli."), StatoCompagno.Sveglio, fallo: true);
        try
        {
            if (!await _regista.Assicura([ruolo.Value], ct))
                return Chiudi(Testo.FalloTu("il modello che guarda non si è acceso."), StatoCompagno.Sveglio, fallo: true);
            var b64 = Convert.ToBase64String(await File.ReadAllBytesAsync(percorso, ct));
            var mime = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                _ => "image/png"
            };
            var risp = await _regista.Completa(new RichiestaChat(
                ruolo.Value,
                Sistema(),
                [],
                "Descrivi questa immagine in italiano, in due frasi semplici.",
                b64, null, null, null, 0, 180, true, mime), ct);
            var detto = Testo.PulisciRisposta(risp?.Testo);
            if (detto.Length == 0)
                return Chiudi(Testo.FalloTu("l'ho guardata ma non ho saputo dire niente."), StatoCompagno.Sveglio, fallo: true);
            _registro.Scrivi("sguardo", Path.GetFileName(percorso), "ok", ruolo.ToString());
            return Chiudi(detto, StatoCompagno.Sveglio);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            var avvisa = _registro.SegnaErrore("sguardo: " + e.Message);
            return Chiudi(AggiungiEvoluzione(Testo.FalloTu("non sono riuscito a guardare il file."), avvisa), StatoCompagno.Sveglio, fallo: true);
        }
    }

    async Task<EsitoDialogo> Esegui(string azione, string testo, double sicurezza, string fonte, CancellationToken ct)
    {
        try
        {
            return azione switch
            {
                "crea_nota" => await Nota(testo, ct),
                "ricorda" => await Memoria(testo, false, ct),
                "dimentica" => await Memoria(testo, true, ct),
                "mostra_creazioni" => Chiudi("Ecco il mio mondo, qui a fianco.", StatoCompagno.Sveglio) with { MostraMondo = true },
                "che_ore_sono" => Chiudi(Testo.OraLocale(DateTimeOffset.Now), StatoCompagno.Sveglio),
                "dormi" => new EsitoDialogo("Vado a dormire. Se mi vuoi, cliccami.", StatoCompagno.Dorme, false, false, false, true, true, null, null),
                _ => await Rispondi(testo, ct)
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            var avvisa = _registro.SegnaErrore(azione + ": " + e.Message);
            var dettaglio = e.Message.Replace('\n', ' ').Trim();
            if (dettaglio.Length > 180) dettaglio = dettaglio[..180];
            return Chiudi(AggiungiEvoluzione(Testo.FalloTu("l'azione «" + NomeBello(azione) + "» non è andata a buon fine. " + dettaglio), avvisa), StatoCompagno.Sveglio, fallo: true);
        }
    }

    async Task<EsitoDialogo> Rispondi(string testo, CancellationToken ct)
    {
        var ruolo = _regista.Presente(RuoloModello.Omni) ? RuoloModello.Omni
            : _regista.Presente(RuoloModello.Mani) ? RuoloModello.Mani : (RuoloModello?)null;
        if (ruolo is null || !_regista.MotorePresente)
        {
            var offline = RispostaOffline();
            RicordaTurno(testo, offline);
            return Chiudi(offline, StatoCompagno.Sveglio, voce: false);
        }
        if (!await _regista.Assicura([ruolo.Value], ct))
        {
            var perche = _regista.UltimoErrore ?? "il modello non è partito e non ho un dettaglio dal motore";
            return Chiudi(Testo.FalloTu("non riesco a caricare " + NomiModello.Etichetta(ruolo.Value) + ". " + perche), StatoCompagno.Sveglio, fallo: true, voce: false);
        }
        var risp = await _regista.Completa(new RichiestaChat(
            ruolo.Value, Sistema(), _storia.ToList(), testo, null, null, null, null,
            ruolo == RuoloModello.Omni ? 0 : 0.2, 400, true), ct);
        var detto = Testo.PulisciRisposta(risp?.Testo);
        if (detto.Length == 0)
            return Chiudi(Testo.FalloTu("la risposta è arrivata vuota. Riprova con una frase più corta."), StatoCompagno.Sveglio, fallo: true);
        RicordaTurno(testo, detto);
        _registro.Scrivi("risposta", "chiacchiera con " + NomiModello.Etichetta(ruolo.Value), "ok", "rispondi");
        return Chiudi(detto, StatoCompagno.Sveglio);
    }

    async Task<EsitoDialogo> Nota(string testo, CancellationToken ct)
    {
        var (titolo, corpo, cartella) = Estrattore.NotaDaTesto(testo);
        var compilato = await Compila(testo, "crea_nota", ct);
        if (compilato is not null && Estrattore.ProvaParametri(compilato, out var campi))
        {
            if (campi.TryGetValue("titolo", out var t) && t.Trim().Length > 0) titolo = t.Trim();
            if (campi.TryGetValue("testo", out var c) && c.Trim().Length > 0) corpo = c.Trim();
            if (campi.TryGetValue("cartella", out var k) && k.Trim().Length > 0) cartella = k.Trim();
        }
        var nota = _mondo.CreaNota(titolo, corpo, cartella);
        _registro.Scrivi("azione", "nota in " + nota.Cartella + "/" + Path.GetFileName(nota.Percorso), "ok", "crea_nota");
        var frase = "Ho scritto «" + nota.Titolo + "» in " + nota.Cartella + ".";
        RicordaTurno(testo, frase);
        return Chiudi(frase, StatoCompagno.Sveglio) with { FileCreato = nota.Percorso, MostraMondo = true };
    }

    async Task<EsitoDialogo> Memoria(string testo, bool dimentica, CancellationToken ct)
    {
        var fatto = Estrattore.FattoDaTesto(testo, dimentica);
        var nome = dimentica ? "dimentica" : "ricorda";
        var compilato = await Compila(testo, nome, ct);
        if (compilato is not null && Estrattore.ProvaParametri(compilato, out var campi) &&
            campi.TryGetValue("fatto", out var f) && f.Trim().Length > 0)
            fatto = f.Trim();
        if (fatto.Length < 2)
            return Chiudi(Testo.FalloTu("non ho capito che cosa " + (dimentica ? "dimenticare" : "ricordare") + ". Dimmelo in una frase."), StatoCompagno.Sveglio, fallo: true);

        if (dimentica)
        {
            var n = _mondo.Dimentica(fatto);
            _registro.Scrivi("memoria", n > 0 ? "cancellato un ricordo" : "nessun ricordo trovato", n > 0 ? "ok" : "vuoto", "dimentica");
            var frase = n > 0 ? "Fatto, l'ho tolto dalla memoria. Il registro non tiene il testo cancellato." : "Non ho trovato niente del genere nella memoria.";
            return Chiudi(frase, StatoCompagno.Sveglio);
        }
        _mondo.Ricorda(fatto);
        _registro.Scrivi("memoria", "aggiunto un ricordo", "ok", "ricorda");
        return Chiudi("Me lo segno. Sta in cosa-so-di-te.md, lo puoi leggere e cancellare.", StatoCompagno.Sveglio) with { MostraMondo = true };
    }

    async Task<string?> Compila(string testo, string azione, CancellationToken ct)
    {
        try
        {
            var scheda = _catalogo.Trova(azione);
            if (scheda is null || !scheda.VuoleParametri || scheda.Schema is null) return null;
            if (!_regista.MotorePresente || !_regista.Presente(RuoloModello.Mani)) return null;
            var insieme = _regista.Presente(RuoloModello.D1)
                ? new[] { RuoloModello.D1, RuoloModello.Mani }
                : new[] { RuoloModello.Mani };
            if (!await _regista.Assicura(insieme, ct)) return null;
            var sistema = "Compila solo lo strumento «" + azione + "». Niente ragionamenti, niente testo extra. Italiano.\n" +
                          (scheda.Esempio is { Length: > 0 } es ? "Esempio: " + es : "");
            var risp = await _regista.Completa(new RichiestaChat(
                RuoloModello.Mani, sistema, [], testo, null, null, azione, scheda.Schema, 0.1, 220, true), ct);
            return risp?.ArgomentiStrumento ?? (Estrattore.ProvaParametri(risp?.Testo, out _) ? risp!.Testo : null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            _registro.SegnaErrore("compilazione: " + e.Message);
            return null;
        }
    }

    string Sistema()
    {
        var modello = CaricaPrompt();
        var memoria = _mondo.LeggiMemoria();
        if (memoria.Length > 3500) memoria = memoria[..3500];
        return modello
            .Replace("{{utente}}", string.IsNullOrWhiteSpace(_imp.NomeUtente) ? "amico" : _imp.NomeUtente)
            .Replace("{{carattere}}", _imp.Carattere)
            .Replace("{{descrizione_carattere}}", ImpostazioniUtente.DescrizioneCarattere(_imp.Carattere))
            .Replace("{{memoria}}", string.IsNullOrWhiteSpace(memoria) ? "(ancora niente)" : memoria);
    }

    string CaricaPrompt()
    {
        var p = Percorsi.Prompt(_mondo.RadiceDati);
        if (File.Exists(p))
        {
            var t = File.ReadAllText(p);
            if (t.Contains("{{carattere}}", StringComparison.Ordinal)) return t;
        }
        var accanto = Path.Combine(AppContext.BaseDirectory, "prompt", "sistema.md");
        if (File.Exists(accanto)) return File.ReadAllText(accanto);
        return PromptDiSerie;
    }

    string RispostaOffline()
    {
        var nome = string.IsNullOrWhiteSpace(_imp.NomeUtente) ? "" : _imp.NomeUtente + ", ";
        var coda = "Il cervello grande è spento. Questo fallo tu: controlla che llama.cpp e i GGUF siano nella cartella del motore (Impostazioni). Intanto, se mi dici «prendi nota», la nota la scrivo lo stesso.";
        return _imp.Carattere switch
        {
            "allegro" => nome + "ci sono lo stesso, anche senza il cervello grande. " + coda,
            "ironico" => nome + "senza modelli sono un soprammobile simpatico. " + coda,
            "pazzerello" => nome + "sto qui a girare sul desktop, il cervello è in pausa. " + coda,
            "saggio" => nome + "resto con te, ma le parole grandi arrivano dai modelli locali. " + coda,
            _ => nome + "sono qui, calmo, in attesa del motore. " + coda
        };
    }

    void RicordaTurno(string utente, string assistente)
    {
        _storia.Add(("user", utente));
        _storia.Add(("assistant", assistente));
        while (_storia.Count > 8) _storia.RemoveAt(0);
    }

    EsitoDialogo Chiudi(string testo, StatoCompagno stato, bool fallo = false, bool voce = true) =>
        new(testo, stato, false, false, fallo, voce && testo.Length > 0, false, null, null);

    static string AggiungiEvoluzione(string testo, bool avvisa) =>
        avvisa ? testo + " " + Registro.FraseEvoluzione : testo;

    public static string NomeBello(string azione) => azione switch
    {
        "crea_nota" => "crea nota",
        "mostra_creazioni" => "mostra le creazioni",
        "ricorda" => "ricorda",
        "dimentica" => "dimentica",
        "dormi" => "dormi",
        "che_ore_sono" => "l'ora",
        _ => "rispondi"
    };

    public static string DomandaConferma(string azione) => azione switch
    {
        "crea_nota" => "Vuoi che me lo segni in una nota?",
        "ricorda" => "Vuoi che me lo ricordi?",
        "dimentica" => "Vuoi che lo dimentichi?",
        "cestino" => "Vuoi che lo metta nel cestino?",
        "usa_pc" => "Vuoi che usi il PC per questo?",
        _ => "Vuoi che lo faccia?"
    };

    public const string PromptDiSerie = """
        Sei Liquidino, un compagno da desktop. Parli italiano, dai del tu, stai sul computer di {{utente}} e non esci da lì.
        Carattere: {{carattere}}. {{descrizione_carattere}}
        Non vai in internet. Non dici di aver fatto una cosa se il programma non l'ha fatta.
        Se non puoi fare qualcosa, la frase comincia con «Questo fallo tu:» e spieghi il perché in una riga.
        Ricordi di {{utente}} solo quello che c'è qui sotto, nient'altro:
        {{memoria}}
        Risposte corte, da compagnia, non da manuale.
        """;
}
