using System.Diagnostics;
using System.Threading.Channels;
using Liquidino.Nucleo;

namespace Liquidino;

/// <summary>
/// Le Orecchie di Liquidino: il tuo microfono, l'audio del PC (tutto tranne lui) e, in chiamata, l'audio del programma
/// della chiamata. Ogni fonte ha il suo rilevatore di voce; le frasi finite vanno in fila a Parakeet, poi a <see cref="Orecchie"/>
/// che decide. Niente si salva: il testo sentito resta in memoria qualche minuto, l'audio si butta subito.
/// </summary>
public sealed class OrecchieWindows : IDisposable
{
    readonly ImpostazioniUtente _imp;
    readonly Func<string> _cartellaModelli;
    readonly Registro _registro;
    readonly FinestraSilenzio _silenzio = new();
    readonly object _uno = new();
    ParolaPersonale _parola;
    TrascrittoreParakeet? _parakeet;
    CatturaMicrofono? _mic;
    CatturaProcesso? _pc;
    CatturaProcesso? _chiamata;
    Channel<Pezzo>? _fila;
    CancellationTokenSource? _lavoro;
    Task? _lavoratore;

    sealed record Pezzo(FonteAscolto Fonte, string Chi, float[] Campioni, DateTimeOffset Quando);

    public Orecchie Logica { get; }
    public bool Accese { get; private set; }
    public bool InChiamata => Logica.InChiamata;
    public string? NomeChiamata { get; private set; }
    public string? NomeMicrofono => _mic?.Nome;
    public bool SenteIlPc => _pc is not null;
    public VoceNelMicrofono? Cavo { get; private set; }

    /// <summary>Una decisione da eseguire: arriva da un filo di lavoro, chi la riceve torna sul filo della finestra.</summary>
    public event Action<DecisioneOrecchio, FonteAscolto, string>? Decisione;
    public event Action? Cambiate;

    public OrecchieWindows(ImpostazioniUtente imp, Func<string> cartellaModelli, Registro registro)
    {
        _imp = imp;
        _cartellaModelli = cartellaModelli;
        _registro = registro;
        _parola = ParolaSveglia.Carica(imp.ParolaSveglia);
        Logica = new Orecchie(() => _parola);
    }

    public ParolaPersonale Parola => _parola;

    /// <summary>Rilegge la parola (dopo una taratura o se l'hai cambiata nelle impostazioni).</summary>
    public void RicaricaParola() => _parola = ParolaSveglia.Carica(_imp.ParolaSveglia);

    /// <summary>Mentre parla lui, il microfono non conta: la sua voce non diventa un comando.</summary>
    public void SilenziaFino(DateTime utc) => _silenzio.FinoA(utc);

    public bool ParakeetPresente => CatalogoOrecchio.Parakeet.Presente(_cartellaModelli());

    /// <summary>Parakeet, caricato una volta sola e tenuto finché le orecchie servono.</summary>
    public async Task<TrascrittoreParakeet> Trascrittore()
    {
        lock (_uno) if (_parakeet is not null) return _parakeet;
        var cartella = _cartellaModelli();
        var nuovo = await Task.Run(() => new TrascrittoreParakeet(cartella)).ConfigureAwait(false);
        lock (_uno)
        {
            if (_parakeet is not null) { nuovo.Dispose(); return _parakeet; }
            _parakeet = nuovo;
            return nuovo;
        }
    }

    /// <summary>Accende le orecchie. Ritorna la frase da dire: cosa sente, o cosa manca («Questo fallo tu: …»).</summary>
    public async Task<string> Accendi()
    {
        if (Accese) return "Le Orecchie sono già accese.";
        RicaricaParola();
        if (!ParakeetPresente)
            return Testo.FalloTu("per le Orecchie mi serve Parakeet. Scaricalo dalla luce bianca sotto l'avatar (o Impostazioni → Modelli), poi riaccendile");
        try { await Trascrittore().ConfigureAwait(false); }
        catch (Exception e)
        {
            _registro.SegnaErrore("parakeet: " + e.Message);
            return Testo.FalloTu("il modello Parakeet non parte: " + e.Message + ". Prova a riscaricarlo dalla pagina Modelli");
        }

        _lavoro = new CancellationTokenSource();
        _fila = Channel.CreateUnbounded<Pezzo>(new UnboundedChannelOptions { SingleReader = true });
        _lavoratore = Task.Run(() => Lavora(_fila.Reader, _lavoro.Token));
        var note = new List<string>();

        var dispositivo = Dispositivi.Microfono(_imp.Microfono);
        if (dispositivo is null) note.Add("non trovo un microfono, quindi non sento «" + _parola.Parola + "»");
        else
        {
            try
            {
                _mic = new CatturaMicrofono(dispositivo);
                var rilevatore = new RilevatoreVoce();
                _mic.Campioni16k += c =>
                {
                    if (_silenzio.Scarta(DateTime.UtcNow)) { rilevatore.Azzera(); return; }
                    foreach (var f in rilevatore.Aggiungi(c)) Metti(FonteAscolto.Tu, "tu", f);
                };
                _mic.Errore += e => _registro.SegnaErrore("microfono delle orecchie: " + e.Message);
                _mic.Avvia();
            }
            catch (Exception e)
            {
                _mic?.Dispose();
                _mic = null;
                _registro.SegnaErrore("microfono delle orecchie: " + e.Message);
                note.Add("il microfono non si apre (" + e.Message + ")");
            }
        }

        if (_imp.AscoltaPc)
        {
            var errore = await ApriPc().ConfigureAwait(false);
            if (errore is not null) note.Add("l'audio del PC non si apre (" + errore + ")");
        }

        if (_mic is null && _pc is null)
        {
            Spegni();
            return Testo.FalloTu(string.Join("; ", note));
        }
        Accese = true;
        _guardaChiamata = new System.Threading.Timer(_ => ControllaChiamata(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
        _registro.Scrivi("orecchie", "accese" + (_mic is null ? "" : ", microfono") + (_pc is null ? "" : ", audio del PC"), "ok", "orecchie");
        Cambiate?.Invoke();
        var frase = _mic is null
            ? "Orecchie accese solo sull'audio del PC."
            : "Orecchie accese: chiamami «" + _parola.Parola + "»" + (_pc is null ? "." : ". Sento anche l'audio del PC, senza la mia voce.");
        return note.Count == 0 ? frase : frase + " Questo fallo tu: " + string.Join("; ", note) + ".";
    }

    async Task<string?> ApriPc()
    {
        try
        {
            var pc = await CatturaProcesso.Apri(Environment.ProcessId, includi: false, CancellationToken.None).ConfigureAwait(false);
            var rilevatore = new RilevatoreVoce();
            pc.Campioni16k += c => { foreach (var f in rilevatore.Aggiungi(c)) Metti(FonteAscolto.Pc, "PC", f); };
            pc.Errore += e => _registro.SegnaErrore("audio del PC: " + e.Message);
            pc.Avvia();
            _pc = pc;
            return null;
        }
        catch (Exception e)
        {
            _registro.SegnaErrore("audio del PC: " + e.Message);
            return e.Message;
        }
    }

    void Metti(FonteAscolto fonte, string chi, FraseSentita frase) =>
        _fila?.Writer.TryWrite(new Pezzo(fonte, chi, frase.Campioni, DateTimeOffset.Now));

    /// <summary>
    /// Trascrive in ordine. Prima l'audio del PC e della chiamata, poi il microfono: così l'eco delle casse
    /// è già nel registro quando arriva la stessa frase dal microfono. Se l'audio del PC si accumula, si saltano i pezzi vecchi.
    /// </summary>
    async Task Lavora(ChannelReader<Pezzo> fila, CancellationToken ct)
    {
        var attesa = new List<Pezzo>();
        try
        {
            while (await fila.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (fila.TryRead(out var p)) attesa.Add(p);
                if (attesa.Any(p => p.Fonte == FonteAscolto.Tu) && (_pc is not null || _chiamata is not null))
                {
                    await Task.Delay(400, ct).ConfigureAwait(false);
                    while (fila.TryRead(out var p)) attesa.Add(p);
                }
                var vecchiPc = attesa.Where(p => p.Fonte == FonteAscolto.Pc).SkipLast(3).ToList();
                foreach (var v in vecchiPc) attesa.Remove(v);
                // Prima la chiamata, poi il PC (che contiene di nuovo la chiamata), per ultimo il microfono.
                var ordine = attesa.OrderBy(p => p.Fonte switch { FonteAscolto.Chiamata => 0, FonteAscolto.Pc => 1, _ => 2 }).ThenBy(p => p.Quando).ToList();
                attesa.Clear();
                foreach (var pezzo in ordine)
                {
                    ct.ThrowIfCancellationRequested();
                    string testo;
                    try { testo = (await Trascrittore().ConfigureAwait(false)).Trascrivi(pezzo.Campioni); }
                    catch (ObjectDisposedException) { return; }
                    catch (Exception e)
                    {
                        _registro.SegnaErrore("trascrizione delle orecchie: " + e.Message);
                        continue;
                    }
                    if (pezzo.Fonte == FonteAscolto.Chiamata && ParolaSveglia.Normalizza(testo).Length > 0)
                    {
                        _ultimaDallaChiamata = pezzo.Quando;
                        if (!Logica.InChiamata) { Logica.InChiamata = true; Cambiate?.Invoke(); }
                    }
                    var decisione = Logica.Sentito(pezzo.Fonte, pezzo.Chi, testo, pezzo.Quando);
                    if (decisione.Cosa != CosaFare.Niente) Decisione?.Invoke(decisione, pezzo.Fonte, pezzo.Chi);
                }
            }
        }
        catch (OperationCanceledException) { /* orecchie spente */ }
        catch (Exception e) { _registro.SegnaErrore("orecchie: " + e.Message); }
    }

    System.Threading.Timer? _guardaChiamata;
    DateTimeOffset _ultimaDallaChiamata = DateTimeOffset.MinValue;
    int _controllo;

    /// <summary>
    /// Ogni 5 secondi guarda se c'è un programma da chiamata aperto (Discord prima di tutti). Se c'è, lo sente a parte,
    /// e se hai acceso «la mia voce nel microfono» apre VB-CABLE. Niente tasto: fa da solo, finché le Orecchie sono accese.
    /// È «in chiamata» solo se da lì sono arrivate voci negli ultimi 3 minuti.
    /// </summary>
    void ControllaChiamata()
    {
        if (Interlocked.Exchange(ref _controllo, 1) == 1) return;
        try
        {
            if (!Accese) return;
            if (Logica.InChiamata && DateTimeOffset.Now - _ultimaDallaChiamata > TimeSpan.FromMinutes(3))
            {
                Logica.InChiamata = false;
                Cambiate?.Invoke();
            }
            var processi = new List<(int, string)>();
            foreach (var p in Process.GetProcesses())
            {
                try { processi.Add((p.Id, p.ProcessName)); }
                catch { /* processo già chiuso */ }
                finally { p.Dispose(); }
            }
            var app = AppChiamata.Scegli(processi);
            if (app is null)
            {
                if (_chiamata is not null)
                {
                    ChiudiPezziChiamata();
                    _registro.Scrivi("chiamata", "il programma della chiamata si è chiuso", "ok", "chiamata");
                    Cambiate?.Invoke();
                }
                return;
            }
            if (_chiamata is not null && NomeChiamata == app.Value.Nome) return;
            ChiudiPezziChiamata();
            try
            {
                var (pid, nome) = app.Value;
                var chiamata = CatturaProcesso.Apri(pid, includi: true, CancellationToken.None).GetAwaiter().GetResult();
                var rilevatore = new RilevatoreVoce();
                chiamata.Campioni16k += c => { foreach (var f in rilevatore.Aggiungi(c)) Metti(FonteAscolto.Chiamata, nome, f); };
                chiamata.Errore += e => _registro.SegnaErrore("audio della chiamata: " + e.Message);
                chiamata.Avvia();
                _chiamata = chiamata;
                NomeChiamata = nome;
                _registro.Scrivi("chiamata", "sento " + nome + " a parte", "ok", "chiamata");
            }
            catch (Exception e)
            {
                _registro.SegnaErrore("audio della chiamata: " + e.Message);
                return;
            }
            if (_imp.VoceNelMicrofono && Dispositivi.Cavo() is { } cavo)
            {
                try
                {
                    Cavo = new VoceNelMicrofono(cavo) { Guadagno = _imp.VolumeVoceMicrofono / 100f };
                    if (_mic is not null) _mic.CampioniOriginali += AlCavo;
                }
                catch (Exception e) { _registro.SegnaErrore("voce nel microfono: " + e.Message); }
            }
            Cambiate?.Invoke();
        }
        catch (Exception e) { _registro.SegnaErrore("controllo della chiamata: " + e.Message); }
        finally { Interlocked.Exchange(ref _controllo, 0); }
    }

    void AlCavo(float[] intrecciati, int frequenza, int canali) => Cavo?.DalMicrofono(intrecciati, frequenza, canali);

    void ChiudiPezziChiamata()
    {
        if (_mic is not null) _mic.CampioniOriginali -= AlCavo;
        Cavo?.Dispose();
        Cavo = null;
        _chiamata?.Dispose();
        _chiamata = null;
        NomeChiamata = null;
        Logica.InChiamata = false;
    }

    /// <summary>La sua frase nel microfono, se è in chiamata col cavo acceso.</summary>
    public void SuonaNelMicrofono(byte[] wav)
    {
        try { Cavo?.Suona(wav); }
        catch (Exception e) { _registro.SegnaErrore("voce nel microfono: " + e.Message); }
    }

    /// <summary>Spegne tutto e scorda quello che ha sentito.</summary>
    public void Spegni()
    {
        var erano = Accese;
        Accese = false;
        _guardaChiamata?.Dispose();
        _guardaChiamata = null;
        ChiudiPezziChiamata();
        _mic?.Dispose();
        _mic = null;
        _pc?.Dispose();
        _pc = null;
        try { _lavoro?.Cancel(); } catch { /* già annullato */ }
        _fila?.Writer.TryComplete();
        _fila = null;
        Logica.Registro.Svuota();
        Logica.ChiudiAttesa();
        if (erano)
        {
            _registro.Scrivi("orecchie", "spente: quello che avevo sentito è dimenticato", "ok", "orecchie");
            Cambiate?.Invoke();
        }
    }

    /// <summary>Libera anche Parakeet: serve prima di eliminarlo dalla pagina Modelli.</summary>
    public void Dispose()
    {
        Spegni();
        try { _lavoratore?.Wait(2000); } catch { /* il lavoratore si chiude da solo */ }
        lock (_uno)
        {
            _parakeet?.Dispose();
            _parakeet = null;
        }
    }
}
