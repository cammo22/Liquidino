using System.Windows.Threading;
using Liquidino.Nucleo;

namespace Liquidino;

/// <summary>Tiene insieme finestre, regista e dialogo. I modelli si accendono solo quando servono.</summary>
public sealed class Ponte
{
    public static string Versione =>
        typeof(Ponte).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    public ImpostazioniUtente Impostazioni { get; }
    public Mondo Mondo { get; }
    public Registro Registro { get; }
    public Regista Regista { get; }
    public Dialogo Dialogo { get; }
    public CicloAutonomo Ciclo { get; }
    public Microfono Microfono { get; } = new();
    public ManiWindows Mani { get; } = new();
    public LettoreVoce Lettore { get; } = new();
    public OrecchieWindows Udito { get; }
    public StatoCompagno Stato { get; private set; } = StatoCompagno.Dorme;
    public string? EtichettaAggiornamento { get; private set; }

    public event Action<StatoCompagno>? StatoCambiato;
    public event Action? MondoCambiato;
    public event Action<string>? Pensiero;
    public event Action? PermessoCambiato;
    public event Action? AscoltoCambiato;
    public event Action<byte[]>? VocePronta;
    public event Action<string>? Annuncio;
    public event Action<EsitoDialogo>? Risposto;
    public event Action? OrecchieCambiate;
    public bool StaAscoltando { get; private set; }
    public FinestraCasa? Casa { get; set; }
    public ComandiLocali Comandi { get; } = new();

    CancellationTokenSource _cts = new();
    DispatcherTimer? _ciclo;
    DispatcherTimer? _notte;
    bool _cicloInCorso;
    int _turni;

    public Ponte(ImpostazioniUtente impostazioni)
    {
        Impostazioni = impostazioni;
        Mondo = new Mondo();
        Registro = new Registro();
        Regista = new Regista(impostazioni, Registro);
        Dialogo = new Dialogo(impostazioni, Mondo, Registro, Regista, CatalogoAzioni.Carica(Percorsi.Catalogo()), Mani);
        Ciclo = new CicloAutonomo(impostazioni, Mondo, Registro, Regista);
        Udito = new OrecchieWindows(impostazioni, () => Regista.Rilevazione.CartellaModelli, Registro);
        Udito.Cambiate += () => SulFiloUi(() => OrecchieCambiate?.Invoke());
        Udito.Decisione += (d, fonte, chi) => SulFiloUi(() => _ = DaOrecchie(d, chi));
        Dialogo.Udito = () => Udito.Accese ? Udito.Logica.Registro.Testo(DateTimeOffset.Now) : null;
        Lettore.Finito += () =>
        {
            Microfono.SilenziaFino(DateTime.UtcNow.AddMilliseconds(500));
            Udito.SilenziaFino(DateTime.UtcNow.AddMilliseconds(700));
            if (Stato == StatoCompagno.Parla) ImpostaStato(StatoCompagno.Sveglio);
        };
    }

    DateTimeOffset _ultimoGesto = DateTimeOffset.Now;

    public void SegnaGesto() => _ultimoGesto = DateTimeOffset.Now;

    public bool StaLavorando =>
        StaAscoltando || Stato is StatoCompagno.Ascolta or StatoCompagno.Pensa or StatoCompagno.Parla;

    public void AvviaOrologi()
    {
        _ = Task.Run(Mani.Prepara);
        _ciclo = new DispatcherTimer { Interval = TimeSpan.FromMinutes(Impostazioni.CicloMinuti) };
        _ciclo.Tick += async (_, _) =>
        {
            try { await CicloOrario(); }
            catch (Exception e) { Registro.SegnaErrore("ciclo: " + e.Message); }
        };
        if (Impostazioni.CicloAutonomo) _ciclo.Start();
        if (Impostazioni.OrecchieAccese) _ = AccendiOrecchie(annuncia: false);

        _notte = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _notte.Tick += async (_, _) =>
        {
            try { await ControllaQuiete(); }
            catch { /* la notte si ricontrolla fra un minuto */ }
        };
        _notte.Start();
    }

    public void ImpostaStato(StatoCompagno stato)
    {
        Stato = stato;
        SulFiloUi(() => StatoCambiato?.Invoke(stato));
    }

    void SulFiloUi(Action azione)
    {
        var filo = System.Windows.Application.Current?.Dispatcher;
        try
        {
            if (filo is null || filo.CheckAccess()) azione();
            else filo.BeginInvoke(azione);
        }
        catch { /* la finestra si sta chiudendo */ }
    }

    public CancellationToken Token() => _cts.Token;

    public void Annulla()
    {
        try { _cts.Cancel(); } catch { /* già annullato */ }
        _cts = new CancellationTokenSource();
        Regista.AnnullaOperazioni();
        _ = Task.Run(() =>
        {
            try { if (Microfono.StaRegistrando) Microfono.Ferma(); } catch { /* il microfono può essere già chiuso */ }
            try { Lettore.Ferma(); } catch { /* il lettore può essere già chiuso */ }
            try { Udito.Cavo?.Zitto(); } catch { /* il cavo può essere già chiuso */ }
        });
        StaAscoltando = false;
        SulFiloUi(() => AscoltoCambiato?.Invoke());
        if (Stato is StatoCompagno.Pensa or StatoCompagno.Ascolta or StatoCompagno.Parla)
            ImpostaStato(StatoCompagno.Sveglio);
        _ = Task.Run(() => Registro.Scrivi("annulla", "turno interrotto: STOP o Esc, il caricamento viene fermato", "annullato"));
    }

    /// <summary>Ritorna subito. Si può premere più volte: non aspetta il motore e non blocca la finestra.</summary>
    public void Stop()
    {
        Annulla();
        // STOP ferma tutto, anche le orecchie: si riaccendono solo col tasto 👂.
        if (Udito.Accese)
        {
            Udito.Spegni();
            Impostazioni.OrecchieAccese = false;
            _ = Task.Run(() => Impostazioni.Salva());
        }
        Dialogo.DimenticaSessione();
        ImpostaStato(StatoCompagno.Dorme);
        _ = Task.Run(() => Registro.Scrivi("stop", "STOP premuto: modelli scaricati", "ok"));
        _ = Comandi.StopAsync(ct => Regista.ScaricaTutto(ct), TimeSpan.FromSeconds(4));
    }

    public void AlternaPermesso()
    {
        Impostazioni.PermessoPc = !Impostazioni.PermessoPc;
        Comandi.ImpostaPermesso(Impostazioni.PermessoPc);
        var acceso = Impostazioni.PermessoPc;
        _ = Task.Run(() =>
        {
            Impostazioni.Salva();
            Registro.Scrivi("permesso", acceso ? "permesso del PC acceso" : "permesso del PC spento", "ok");
        });
        PermessoCambiato?.Invoke();
    }

    /// <summary>Il tasto 👂: accende o spegne le Orecchie.</summary>
    public async Task AlternaOrecchie()
    {
        SegnaGesto();
        if (Udito.Accese)
        {
            Udito.Spegni();
            Impostazioni.OrecchieAccese = false;
            _ = Task.Run(() => Impostazioni.Salva());
            Annuncia("Orecchie spente: non sento niente, e quello che avevo sentito l'ho dimenticato.");
            return;
        }
        await AccendiOrecchie(annuncia: true);
    }

    async Task AccendiOrecchie(bool annuncia)
    {
        string frase;
        try { frase = await Udito.Accendi(); }
        catch (Exception e)
        {
            Registro.SegnaErrore("orecchie: " + e.Message);
            frase = Testo.FalloTu("le Orecchie non si accendono: " + e.Message);
        }
        Impostazioni.OrecchieAccese = Udito.Accese;
        _ = Task.Run(() => Impostazioni.Salva());
        if (annuncia || !Udito.Accese) Annuncia(frase);
        SulFiloUi(() => OrecchieCambiate?.Invoke());
    }

    /// <summary>Il tasto 📞: entra o esce dalla modalità chiamata.</summary>
    public async Task AlternaChiamata()
    {
        SegnaGesto();
        string frase;
        try { frase = Udito.InChiamata ? await Udito.ChiudiChiamata() : await Udito.ApriChiamata(); }
        catch (Exception e)
        {
            Registro.SegnaErrore("chiamata: " + e.Message);
            frase = Testo.FalloTu("la modalità chiamata non parte: " + e.Message);
        }
        Impostazioni.OrecchieAccese = Udito.Accese;
        Annuncia(frase);
        SulFiloUi(() => OrecchieCambiate?.Invoke());
    }

    DispatcherTimer? _dimmi;

    /// <summary>Quello che le orecchie hanno deciso: un comando tuo, il nome da solo, o qualcuno in chiamata.</summary>
    async Task DaOrecchie(DecisioneOrecchio d, string chi)
    {
        try
        {
            switch (d.Cosa)
            {
                case CosaFare.Svegliato:
                    if (Stato is StatoCompagno.Pensa or StatoCompagno.Parla || StaAscoltando) return;
                    SegnaGesto();
                    ImpostaStato(StatoCompagno.Ascolta);
                    Annuncia("Dimmi.");
                    _dimmi?.Stop();
                    _dimmi = new DispatcherTimer { Interval = Udito.Logica.AttesaDopoNome + TimeSpan.FromSeconds(1) };
                    _dimmi.Tick += (_, _) =>
                    {
                        _dimmi?.Stop();
                        if (Stato == StatoCompagno.Ascolta && !StaAscoltando) ImpostaStato(StatoCompagno.Sveglio);
                    };
                    _dimmi.Start();
                    break;
                case CosaFare.Comando:
                    // Mentre pensa a un'altra cosa non si accavalla: la frase si perde e lo dice il registro.
                    if (Stato == StatoCompagno.Pensa)
                    {
                        Registro.Scrivi("orecchie", "comando a voce mentre pensavo: ignorato", "rifiutato", "orecchie");
                        return;
                    }
                    _dimmi?.Stop();
                    Registro.Scrivi("orecchie", "comando a voce: " + d.Testo.Length + " caratteri", "ok", "orecchie");
                    await Messaggio(d.Testo, sentito: d.Testo);
                    break;
                case CosaFare.RispostaChiamata:
                    if (Stato is StatoCompagno.Pensa or StatoCompagno.Parla) return;
                    var ct = Token();
                    ImpostaStato(StatoCompagno.Pensa);
                    try
                    {
                        var esito = await Dialogo.RispondiInChiamata(chi, d.Testo, ct);
                        if (esito.Testo.Length == 0) { ImpostaStato(StatoCompagno.Sveglio); return; }
                        await Dopo(esito, ct);
                    }
                    catch (OperationCanceledException) { TurnoAnnullato(); }
                    break;
            }
        }
        catch (Exception e) { Registro.SegnaErrore("orecchie: " + e.Message); }
    }

    public void AlternaAscolto()
    {
        if (StaAscoltando) _ = ChiudiAscoltoAsync(invia: true);
        else _ = ApriAscoltoAsync();
    }

    async Task ApriAscoltoAsync()
    {
        if (StaAscoltando) return;
        SegnaGesto();
        try
        {
            await Task.Run(() => Microfono.Inizia()).ConfigureAwait(false);
            StaAscoltando = true;
            ImpostaStato(StatoCompagno.Ascolta);
            SulFiloUi(() => AscoltoCambiato?.Invoke());
        }
        catch (Exception e)
        {
            StaAscoltando = false;
            _ = Task.Run(() => Registro.SegnaErrore("microfono: " + e.Message));
            SulFiloUi(() => AscoltoCambiato?.Invoke());
        }
    }

    async Task ChiudiAscoltoAsync(bool invia)
    {
        if (!StaAscoltando && !Microfono.StaRegistrando) return;
        StaAscoltando = false;
        byte[]? wav = null;
        try { wav = await Task.Run(() => Microfono.Ferma()).ConfigureAwait(false); }
        catch { /* niente da mandare */ }
        if (Stato == StatoCompagno.Ascolta) ImpostaStato(StatoCompagno.Sveglio);
        SulFiloUi(() => AscoltoCambiato?.Invoke());
        if (invia && wav is { Length: >= 1000 })
        {
            var copia = wav;
            SulFiloUi(() => VocePronta?.Invoke(copia));
        }
    }

    public async Task<EsitoDialogo> Messaggio(string testo, string? sentito = null)
    {
        var ct = Token();
        SegnaGesto();
        ImpostaStato(StatoCompagno.Pensa);
        try
        {
            var esito = await Dialogo.Messaggio(testo, ct);
            // Detto a voce con le Orecchie: la frase compare in chat come tua e la risposta si legge ad alta voce.
            if (sentito is not null) esito = esito with { Trascrizione = sentito, VuoiVoce = esito.Testo.Length > 0 };
            await Dopo(esito, ct);
            return esito;
        }
        catch (OperationCanceledException)
        {
            return TurnoAnnullato();
        }
    }

    EsitoDialogo TurnoAnnullato()
    {
        var testo = "Turno annullato: è stato premuto STOP o Esc. Se il modello non era già pronto, non l'ho caricato.";
        ImpostaStato(StatoCompagno.Dorme);
        SulFiloUi(() => Annuncio?.Invoke(testo));
        return new EsitoDialogo(testo, StatoCompagno.Dorme, false, false, true, false, false, null, null);
    }

    public async Task<EsitoDialogo> DaAudio(byte[] wav)
    {
        var ct = Token();
        SegnaGesto();
        ImpostaStato(StatoCompagno.Pensa);
        try
        {
            var esito = await Dialogo.DaAudio(wav, ct);
            await Dopo(esito, ct);
            return esito;
        }
        catch (OperationCanceledException)
        {
            return TurnoAnnullato();
        }
    }

    public async Task<EsitoDialogo> Guarda(string percorso)
    {
        var ct = Token();
        SegnaGesto();
        ImpostaStato(StatoCompagno.Pensa);
        try
        {
            var esito = await Dialogo.Guarda(percorso, ct);
            await Dopo(esito, ct);
            return esito;
        }
        catch (OperationCanceledException)
        {
            return TurnoAnnullato();
        }
    }

    public void Annuncia(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return;
        SulFiloUi(() => Annuncio?.Invoke(testo));
    }

    public void SegnaAvvio()
    {
        var testo = Regista.Riassunto();
        Registro.Scrivi("avvio", testo, "ok");
        if (!Regista.MotorePresente || (!Regista.Presente(RuoloModello.Mani) && !Regista.Presente(RuoloModello.Omni)))
            Annuncia("Questo fallo tu: " + testo + ". Senza questi file non posso caricare un modello.");
    }

    async Task Dopo(EsitoDialogo esito, CancellationToken ct)
    {
        if (esito.FileCreato is not null || esito.MostraMondo) SulFiloUi(() => MondoCambiato?.Invoke());
        SulFiloUi(() => Risposto?.Invoke(esito));
        if (esito.VuoiVoce && esito.Testo.Length > 0 && !ct.IsCancellationRequested)
            await Parla(esito.Testo, ct);
        if (esito.ScaricaDopo)
        {
            Dialogo.DimenticaSessione();
            await Regista.ScaricaTutto(CancellationToken.None);
            ImpostaStato(StatoCompagno.Dorme);
            Registro.Scrivi("sonno", "è andato a dormire", "ok");
        }
        else if (Stato == StatoCompagno.Pensa)
            ImpostaStato(esito.Stato);
        _turni++;
    }

    /// <summary>
    /// Legge la frase: Omni se c'è, altrimenti (o se la sua voce non esce) le voci italiane di Windows.
    /// Se non esce niente torna sveglio: non resta fermo nello stato «parla».
    /// </summary>
    public async Task Parla(string testo, CancellationToken ct)
    {
        var frase = TagliaPerVoce(testo);
        var motore = SceltaVoce.Scegli(Regista.MotorePresente, Regista.Presente(RuoloModello.Omni), Impostazioni.VoceWindows, VoceWindows.Disponibile);
        if (motore == MotoreVoce.Nessuno || frase.Length == 0) return;
        var suona = false;
        try
        {
            ImpostaStato(StatoCompagno.Parla);
            byte[]? bytes = null;
            if (motore == MotoreVoce.Omni)
            {
                try { bytes = await Regista.Sintetizza(frase, Impostazioni.Voce, ct); }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) { Registro.SegnaErrore("voce di Omni: " + e.Message); }
                if (bytes is not { Length: >= 44 })
                    motore = SceltaVoce.DopoErrore(Impostazioni.VoceWindows, VoceWindows.Disponibile);
            }
            if (motore == MotoreVoce.Windows)
                bytes = await Task.Run(() => VoceWindows.Sintetizza(frase, Impostazioni.Voce), ct);
            if (bytes is not { Length: >= 44 }) return;
            Lettore.Ferma();
            var path = Path.Combine(Path.GetTempPath(), "liquidino-voce-" + Guid.NewGuid().ToString("N") + ".wav");
            await File.WriteAllBytesAsync(path, bytes, ct);
            Microfono.SilenziaFino(DateTime.UtcNow.AddMinutes(2));
            Udito.SilenziaFino(DateTime.UtcNow.AddMinutes(2));
            if (Udito.InChiamata) Udito.SuonaNelMicrofono(bytes);
            Lettore.Suona(path);
            suona = true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            Registro.SegnaErrore("voce: " + e.Message);
        }
        finally
        {
            // Mentre suona, lo stato torna sveglio quando il lettore finisce.
            if (!suona && Stato == StatoCompagno.Parla) ImpostaStato(StatoCompagno.Sveglio);
        }
    }

    public async Task CicloOrario()
    {
        if (_cicloInCorso || !Impostazioni.CicloAutonomo) return;
        if (Stato is StatoCompagno.Ascolta or StatoCompagno.Pensa or StatoCompagno.Parla) return;
        _cicloInCorso = true;
        try
        {
            var esito = await Ciclo.Gira(DateTimeOffset.Now, MisuraPc.Occupato(), Token());
            if (!esito.Fatto || esito.Testo is null) return;
            SulFiloUi(() => MondoCambiato?.Invoke());
            var detto = esito.Testo;
            SulFiloUi(() => Pensiero?.Invoke(detto));
            if (esito.Parla) await Parla(esito.Testo, Token());
        }
        finally { _cicloInCorso = false; }
    }

    async Task ControllaQuiete()
    {
        var ora = DateTimeOffset.Now;
        var lavora = StaLavorando;
        if (Quiete.PuoAddormentarsi(Impostazioni.ENotte(ora), lavora, _ultimoGesto, ora))
        {
            if (Regista.Accesi.Count == 0 && Stato == StatoCompagno.Dorme) return;
            Lettore.Ferma();
            Dialogo.DimenticaSessione();
            await Regista.ScaricaTutto(CancellationToken.None);
            ImpostaStato(StatoCompagno.Dorme);
            Registro.Scrivi("notte", "di notte dorme, modelli scaricati", "ok");
            ScriviDiario(ora);
            return;
        }
        if (Quiete.PuoScaricare(lavora, Regista.Accesi.Count, _ultimoGesto, ora))
        {
            await Regista.ScaricaTutto(CancellationToken.None);
            Registro.Scrivi("motore", "inattività: modelli scaricati", "ok");
        }
    }

    /// <summary>Il diario della giornata appena finita, quando va a dormire. Un errore qui non sveglia nessuno.</summary>
    void ScriviDiario(DateTimeOffset ora)
    {
        try
        {
            var giorno = Diario.GiornoDaRaccontare(ora, Impostazioni.OraNotteFine);
            var percorso = Diario.Scrivi(Mondo, giorno, ora);
            Registro.Scrivi("diario", Path.GetFileName(percorso), "ok");
            SulFiloUi(() => MondoCambiato?.Invoke());
        }
        catch (Exception e) { Registro.SegnaErrore("diario: " + e.Message); }
    }

    /// <summary>Rifà un pensiero con la tua indicazione. STOP o Esc lo fermano.</summary>
    public async Task<EsitoCiclo> Rifai(string percorso, string indicazione)
    {
        SegnaGesto();
        ImpostaStato(StatoCompagno.Pensa);
        try
        {
            var esito = await Ciclo.Rifai(percorso, indicazione, Token());
            if (esito.Fatto) SulFiloUi(() => MondoCambiato?.Invoke());
            return esito;
        }
        catch (OperationCanceledException)
        {
            return new EsitoCiclo(false, "è stato premuto STOP o Esc, quindi non l'ho rifatto", null, null, false);
        }
        finally
        {
            if (Stato == StatoCompagno.Pensa) ImpostaStato(StatoCompagno.Sveglio);
        }
    }

    /// <summary>Il pensiero chiesto da te. Non lancia eccezioni: dal tasto non deve uscire la finestra d'errore.</summary>
    public async Task PensieroAdesso()
    {
        SegnaGesto();
        if (_cicloInCorso)
        {
            Annuncia("Sto già scrivendo un pensiero.");
            return;
        }
        _cicloInCorso = true;
        var ct = Token();
        try
        {
            ImpostaStato(StatoCompagno.Pensa);
            var esito = await Ciclo.Gira(DateTimeOffset.Now, false, ct, richiesto: true);
            if (!esito.Fatto || esito.Testo is null)
            {
                Annuncia(string.IsNullOrWhiteSpace(esito.Motivo) ? "Adesso non ho un pensiero." : "Adesso non scrivo: " + esito.Motivo + ".");
                return;
            }
            SulFiloUi(() =>
            {
                MondoCambiato?.Invoke();
                Pensiero?.Invoke(esito.Testo);
            });
            if (esito.Parla) await Parla(esito.Testo, ct);
        }
        catch (OperationCanceledException) { Annuncia("Mi sono fermato: STOP o Esc."); }
        catch (Exception e)
        {
            Registro.SegnaErrore("pensiero: " + e.Message);
            Annuncia("Questo fallo tu: il pensiero non è uscito. " + e.Message);
        }
        finally
        {
            _cicloInCorso = false;
            if (Stato == StatoCompagno.Pensa) ImpostaStato(StatoCompagno.Sveglio);
        }
    }

    /// <summary>
    /// La prova di carico vera: per ogni gradino riaccende il motore con quel contesto e lo riempie al 90%.
    /// Tiene il primo che regge. Se nessuno regge, o premi STOP, rimette il contesto di prima.
    /// </summary>
    public async Task<EsitoProvaContesto> ProvaContestoVera(Action<string> passo)
    {
        if (!Regista.MotorePresente || !Regista.Presente(RuoloModello.Mani))
            return new EsitoProvaContesto(false, 0, 0, null, ["Per la prova servono llama-server e LFM3-1.7B nella cartella del motore."]);
        void Passo(string testo) => SulFiloUi(() => passo(testo));
        SegnaGesto();
        var prima = Impostazioni.ContestoMani;
        var ct = Token();
        ImpostaStato(StatoCompagno.Pensa);
        try
        {
            var esito = await ProvaContesto.Esegui(SceltaContesto.ObiettivoMani, async (contesto, t) =>
            {
                Impostazioni.ContestoMani = contesto;
                await RicaricaMotore().ConfigureAwait(false);
                SegnaGesto();
                return await Regista.ProvaRiempimento(ProvaContesto.Obiettivo(contesto), t).ConfigureAwait(false);
            }, Passo, ct);
            if (esito.Riuscita)
            {
                Impostazioni.ContestoMani = esito.Contesto;
                Impostazioni.FraseContesto = esito.Frase();
                await Task.Run(Impostazioni.Salva);
            }
            else
            {
                Impostazioni.ContestoMani = prima;
                await RicaricaMotore();
            }
            Registro.Scrivi("contesto", esito.Frase(), esito.Riuscita ? "ok" : "errore");
            return esito;
        }
        catch (OperationCanceledException)
        {
            Impostazioni.ContestoMani = prima;
            try { await RicaricaMotore(); } catch { /* il motore ripartirà al prossimo messaggio */ }
            return new EsitoProvaContesto(false, 0, 0, null, ["Prova fermata con STOP o Esc: ho rimesso il contesto di prima."]);
        }
        catch (Exception e)
        {
            Impostazioni.ContestoMani = prima;
            Registro.SegnaErrore("prova di carico: " + e.Message);
            try { await RicaricaMotore(); } catch { /* il motore ripartirà al prossimo messaggio */ }
            return new EsitoProvaContesto(false, 0, 0, null, ["La prova si è interrotta: " + e.Message]);
        }
        finally
        {
            if (Stato == StatoCompagno.Pensa) ImpostaStato(StatoCompagno.Sveglio);
        }
    }

    public async Task RicaricaMotore()
    {
        await Task.Run(() => Impostazioni.Salva()).ConfigureAwait(false);
        await Regista.Ferma().ConfigureAwait(false);
        Regista.Rileggi();
        SulFiloUi(() =>
        {
            _ciclo?.Stop();
            if (_ciclo is null) return;
            _ciclo.Interval = TimeSpan.FromMinutes(Impostazioni.CicloMinuti);
            if (Impostazioni.CicloAutonomo) _ciclo.Start();
        });
    }

    int _spento;

    /// <summary>Chiude timer, microfono e llama-server. Se non risponde entro 4 secondi, kill dell'albero.</summary>
    public void Spegni()
    {
        if (Interlocked.Exchange(ref _spento, 1) == 1) return;
        _ciclo?.Stop();
        _notte?.Stop();
        try { _cts.Cancel(); } catch { /* già annullato */ }
        try { Microfono.Dispose(); } catch { /* il microfono può essere già chiuso */ }
        try { Udito.Dispose(); } catch { /* le orecchie possono essere già chiuse */ }
        try { Lettore.Dispose(); } catch { /* il lettore può essere già chiuso */ }
        try
        {
            var compito = Task.Run(() => Regista.Ferma());
            if (!compito.Wait(TimeSpan.FromSeconds(4)))
                Regista.UccidiSubito();
        }
        catch { Regista.UccidiSubito(); }
    }

    public string RigaStato()
    {
        var modelli = Regista.Descrizione();
        var fase = Impostazioni.InAmicizia(DateTimeOffset.Now) ? "fase di amicizia" : "può creare";
        return Stati.Etichetta(Stato) + " · " + modelli + " · " + fase + " · errori " + Registro.Errori;
    }

    static string TagliaPerVoce(string testo)
    {
        var t = testo.Trim();
        if (t.Length <= 500) return t;
        return t[..500];
    }
}
