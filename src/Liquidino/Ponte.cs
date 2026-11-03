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
    public LettoreVoce Lettore { get; } = new();
    public StatoCompagno Stato { get; private set; } = StatoCompagno.Dorme;
    public string? EtichettaAggiornamento { get; private set; }

    public event Action<StatoCompagno>? StatoCambiato;
    public event Action? MondoCambiato;
    public event Action<string>? Pensiero;
    public event Action? PermessoCambiato;
    public event Action? AscoltoCambiato;
    public event Action<byte[]>? VocePronta;
    public event Action<string>? Annuncio;
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
        Dialogo = new Dialogo(impostazioni, Mondo, Registro, Regista, CatalogoAzioni.Carica(Percorsi.Catalogo()));
        Ciclo = new CicloAutonomo(impostazioni, Mondo, Registro, Regista);
        Lettore.Finito += () =>
        {
            if (Stato == StatoCompagno.Parla) ImpostaStato(StatoCompagno.Sveglio);
        };
    }

    public void AvviaOrologi()
    {
        _ciclo = new DispatcherTimer { Interval = TimeSpan.FromMinutes(Impostazioni.CicloMinuti) };
        _ciclo.Tick += async (_, _) =>
        {
            try { await CicloOrario(); }
            catch (Exception e) { Registro.SegnaErrore("ciclo: " + e.Message); }
        };
        if (Impostazioni.CicloAutonomo) _ciclo.Start();

        _notte = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _notte.Tick += async (_, _) =>
        {
            try { await ControllaNotte(); }
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

    public void AlternaAscolto()
    {
        if (StaAscoltando) _ = ChiudiAscoltoAsync(invia: true);
        else _ = ApriAscoltoAsync();
    }

    async Task ApriAscoltoAsync()
    {
        if (StaAscoltando) return;
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

    public async Task<EsitoDialogo> Messaggio(string testo)
    {
        var ct = Token();
        ImpostaStato(StatoCompagno.Pensa);
        try
        {
            var esito = await Dialogo.Messaggio(testo, ct);
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
        if (esito.FalloTu) Annuncia(esito.Testo);
        if (esito.FileCreato is not null || esito.MostraMondo) SulFiloUi(() => MondoCambiato?.Invoke());
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

    public async Task Parla(string testo, CancellationToken ct)
    {
        if (!Regista.Presente(RuoloModello.Omni) || !Regista.MotorePresente) return;
        try
        {
            ImpostaStato(StatoCompagno.Parla);
            var bytes = await Regista.Sintetizza(TagliaPerVoce(testo), Impostazioni.Voce, ct);
            if (bytes is null || bytes.Length < 44) return;
            var path = Path.Combine(Path.GetTempPath(), "liquidino-voce.wav");
            await File.WriteAllBytesAsync(path, bytes, ct);
            Lettore.Suona(path);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            Registro.SegnaErrore("voce: " + e.Message);
            if (Stato == StatoCompagno.Parla) ImpostaStato(StatoCompagno.Sveglio);
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

    async Task ControllaNotte()
    {
        if (!Impostazioni.ENotte(DateTimeOffset.Now)) return;
        if (Stato is StatoCompagno.Ascolta or StatoCompagno.Pensa or StatoCompagno.Parla) return;
        if (Regista.Accesi.Count == 0 && Stato == StatoCompagno.Dorme) return;
        Lettore.Ferma();
        Dialogo.DimenticaSessione();
        await Regista.ScaricaTutto(CancellationToken.None);
        ImpostaStato(StatoCompagno.Dorme);
        Registro.Scrivi("notte", "di notte dorme, modelli scaricati", "ok");
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
