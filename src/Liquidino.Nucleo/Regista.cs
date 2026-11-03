using System.Diagnostics;

namespace Liquidino.Nucleo;

public interface IRegista
{
    bool MotorePresente { get; }
    bool Presente(RuoloModello ruolo);
    IReadOnlyCollection<RuoloModello> Accesi { get; }
    string Descrizione();
    string Riassunto();
    string? UltimoErrore { get; }
    Task<bool> Assicura(IReadOnlyList<RuoloModello> ruoli, CancellationToken ct);
    Task ScaricaTutto(CancellationToken ct);
    Task Ferma();
    Task<SceltaAzione?> Decidi(string stato, CatalogoAzioni catalogo, CancellationToken ct);
    Task<RispostaChat?> Completa(RichiestaChat richiesta, CancellationToken ct);
    Task<string?> Trascrivi(byte[] wav, CancellationToken ct);
    Task<byte[]?> Sintetizza(string testo, string voce, CancellationToken ct);
}

/// <summary>
/// Accende llama-server in ascolto solo su questo PC, carica e scarica i modelli,
/// e non ne tiene mai più di due. Prima di scaricare prova a salvare lo slot su disco.
/// </summary>
public sealed class Regista : IRegista
{
    readonly ImpostazioniUtente _imp;
    readonly Registro _registro;
    readonly string _radiceDati;
    readonly SemaphoreSlim _porta = new(1, 1);
    readonly HashSet<RuoloModello> _accesi = [];
    Rilevazione _rilevazione;
    Process? _processo;
    ClienteLlama? _cliente;
    bool _avviatoDaNoi;
    CancellationTokenSource _operazioni = new();

    public Regista(ImpostazioniUtente imp, Registro registro, string? radiceDati = null)
    {
        _imp = imp;
        _registro = registro;
        _radiceDati = radiceDati ?? Percorsi.RadiceDati();
        _rilevazione = RilevatoreModelli.Cerca(imp.CartellaMotore);
    }

    public bool MotorePresente => _rilevazione.Eseguibile is not null;
    public bool Presente(RuoloModello ruolo) => _rilevazione.Modelli.Any(m => m.Ruolo == ruolo);
    public IReadOnlyCollection<RuoloModello> Accesi => _accesi.ToList();

    public string? UltimoErrore { get; private set; }

    public string Descrizione()
    {
        if (!MotorePresente) return "motore non trovato";
        if (_accesi.Count == 0) return "motore fermo, modelli spenti";
        return "accesi: " + string.Join(", ", _accesi.Select(NomiModello.Etichetta));
    }

    public string Riassunto()
    {
        var nomi = _rilevazione.Modelli.Count == 0
            ? "nessun GGUF"
            : string.Join(", ", _rilevazione.Modelli.Select(m => m.Id));
        var exe = _rilevazione.Eseguibile is null ? "llama-server non trovato" : Path.GetFileName(_rilevazione.Eseguibile);
        return exe + " · cartella " + _imp.CartellaMotore + " · " + nomi;
    }

    public void Rileggi() => _rilevazione = RilevatoreModelli.Cerca(_imp.CartellaMotore);

    public void AnnullaOperazioni()
    {
        var nuovo = new CancellationTokenSource();
        var vecchio = Interlocked.Exchange(ref _operazioni, nuovo);
        try { vecchio.Cancel(); } catch { /* già annullato */ }
    }

    public void UccidiSubito()
    {
        AnnullaOperazioni();
        UccidiProcesso(_processo);
        UccidiLlamaSullaPorta(_imp.Porta);
        try { _cliente?.Dispose(); } catch { /* già chiuso */ }
        _cliente = null;
        _processo = null;
        _avviatoDaNoi = false;
        _accesi.Clear();
    }

    /// <summary>Chiude llama-server in ascolto sulla nostra porta, anche se non l'abbiamo avviato noi.</summary>
    public static void UccidiLlamaSullaPorta(int porta)
    {
        try
        {
            var si = new ProcessStartInfo("netstat", "-ano")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(si);
            if (p is null) return;
            var letto = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(3000))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* già chiuso */ }
                return;
            }
            var testo = letto.Wait(1000) ? letto.Result : "";
            var ago = ":" + porta;
            foreach (var riga in testo.Split('\n'))
            {
                if (!riga.Contains(ago, StringComparison.Ordinal) || !riga.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                    continue;
                var pezzi = riga.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (pezzi.Length == 0 || !int.TryParse(pezzi[^1], out var pid) || pid <= 0) continue;
                try
                {
                    var proc = Process.GetProcessById(pid);
                    if (!proc.ProcessName.Contains("llama", StringComparison.OrdinalIgnoreCase)) continue;
                    proc.Kill(entireProcessTree: true);
                }
                catch { /* il processo è già uscito */ }
            }
        }
        catch { /* netstat può mancare: il processo nostro è già stato chiuso sopra */ }
    }

    public async Task<bool> Assicura(IReadOnlyList<RuoloModello> ruoli, CancellationToken ct)
    {
        using var legati = CancellationTokenSource.CreateLinkedTokenSource(ct, _operazioni.Token);
        if (!await _porta.WaitAsync(TimeSpan.FromSeconds(3), legati.Token).ConfigureAwait(false))
            return false;
        try { return await AssicuraInterno(ruoli, legati.Token).ConfigureAwait(false); }
        finally { _porta.Release(); }
    }

    async Task<bool> AssicuraInterno(IReadOnlyList<RuoloModello> ruoli, CancellationToken ct)
    {
        foreach (var r in ruoli.Distinct())
            if (!Presente(r)) return false;
        if (!await Garantito(ct)) return false;
        var piano = PoliticaCarico.Calcola(_accesi, ruoli);
        foreach (var spegni in piano.Spegni)
            await ScaricaUno(spegni, ct).ConfigureAwait(false);
        // Con --no-models-autoload il router non carica da solo: lo chiediamo noi, ogni volta.
        foreach (var ruolo in ruoli.Distinct())
        {
            if (!await AccendiUno(ruolo, ct).ConfigureAwait(false)) return false;
        }
        if (_accesi.Count > PoliticaCarico.MassimoInsieme)
            throw new InvalidOperationException("Mai più di due modelli insieme.");
        return ruoli.All(_accesi.Contains);
    }

    public async Task ScaricaTutto(CancellationToken ct)
    {
        using var tempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tempo.CancelAfter(ClienteLlama.TempoScarico + TimeSpan.FromSeconds(2));
        var preso = false;
        try
        {
            preso = await _porta.WaitAsync(TimeSpan.FromSeconds(2), tempo.Token).ConfigureAwait(false);
            if (!preso) return;
            foreach (var r in _accesi.ToList())
                await ScaricaUno(r, tempo.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* lo STOP ha chiuso il tempo */ }
        catch (Exception e) { _registro.SegnaErrore("scarico: " + e.Message); }
        finally { if (preso) _porta.Release(); }
    }

    public async Task Ferma()
    {
        AnnullaOperazioni();
        try { await ScaricaTutto(CancellationToken.None).ConfigureAwait(false); }
        catch { /* si chiude comunque */ }
        UccidiSubito();
        _registro.Scrivi("motore", "llama-server fermato", "ok");
    }

    public async Task<SceltaAzione?> Decidi(string stato, CatalogoAzioni catalogo, CancellationToken ct)
    {
        using var legati = CancellationTokenSource.CreateLinkedTokenSource(ct, _operazioni.Token);
        if (!await _porta.WaitAsync(TimeSpan.FromSeconds(3), legati.Token).ConfigureAwait(false))
            return null;
        try { return await DecidiInterno(stato, catalogo, legati.Token).ConfigureAwait(false); }
        finally { _porta.Release(); }
    }

    async Task<SceltaAzione?> DecidiInterno(string stato, CatalogoAzioni catalogo, CancellationToken ct)
    {
        if (_cliente is null || !Presente(RuoloModello.D1)) return null;
        var json = await _cliente.Decidi(NomiModello.IdDi(RuoloModello.D1), stato, catalogo, ct).ConfigureAwait(false);
        return LettoreDecisione.Leggi(json, catalogo.Azioni.Select(a => a.Nome).ToList());
    }

    public async Task<RispostaChat?> Completa(RichiestaChat richiesta, CancellationToken ct)
    {
        using var legati = CancellationTokenSource.CreateLinkedTokenSource(ct, _operazioni.Token);
        if (!await _porta.WaitAsync(TimeSpan.FromSeconds(3), legati.Token).ConfigureAwait(false))
            return null;
        try
        {
            if (_cliente is null) return null;
            return await _cliente.Chat(NomiModello.IdDi(richiesta.Modello), richiesta, legati.Token).ConfigureAwait(false);
        }
        finally { _porta.Release(); }
    }

    public async Task<string?> Trascrivi(byte[] wav, CancellationToken ct)
    {
        using var legati = CancellationTokenSource.CreateLinkedTokenSource(ct, _operazioni.Token);
        if (!await _porta.WaitAsync(TimeSpan.FromSeconds(3), legati.Token).ConfigureAwait(false))
            return null;
        try
        {
            if (!Presente(RuoloModello.Omni)) return null;
            if (!await AssicuraInterno([RuoloModello.Omni], legati.Token).ConfigureAwait(false)) return null;
            var b64 = Convert.ToBase64String(wav);
            if (_cliente is null) return null;
            var risp = await _cliente.Chat(NomiModello.IdDi(RuoloModello.Omni), new RichiestaChat(
                RuoloModello.Omni,
                "Perform ASR in Italian. Trascrivi solo le parole dette, in italiano, senza commenti.",
                [],
                "<__media__>",
                null,
                b64,
                null,
                null,
                0,
                200,
                true), legati.Token).ConfigureAwait(false);
            var testo = Testo.PulisciRisposta(risp.Testo);
            return string.IsNullOrWhiteSpace(testo) ? null : testo;
        }
        finally { _porta.Release(); }
    }

    public async Task<byte[]?> Sintetizza(string testo, string voce, CancellationToken ct)
    {
        using var legati = CancellationTokenSource.CreateLinkedTokenSource(ct, _operazioni.Token);
        if (!await _porta.WaitAsync(TimeSpan.FromSeconds(3), legati.Token).ConfigureAwait(false))
            return null;
        try
        {
            if (!Presente(RuoloModello.Omni)) return null;
            if (!await AssicuraInterno([RuoloModello.Omni], legati.Token).ConfigureAwait(false) || _cliente is null) return null;
            return await _cliente.Sintetizza(NomiModello.IdDi(RuoloModello.Omni), testo, voce, legati.Token).ConfigureAwait(false);
        }
        finally { _porta.Release(); }
    }

    async Task<bool> Garantito(CancellationToken ct)
    {
        if (_cliente is not null && await _cliente.Vivo(ct).ConfigureAwait(false)) return true;
        _cliente?.Dispose();
        _cliente = null;
        _accesi.Clear();
        if (_rilevazione.Eseguibile is null)
        {
            UltimoErrore = "non trovo llama-server in " + _imp.CartellaMotore;
            return false;
        }
        var preset = Percorsi.Preset(_radiceDati);
        await Task.Run(() => PresetMotore.Scrivi(preset, Percorsi.Slot(_radiceDati), _rilevazione.Modelli, _imp), ct).ConfigureAwait(false);
        var exe = _rilevazione.Eseguibile;
        var porta = _imp.Porta;
        _cliente = new ClienteLlama(porta);
        if (await _cliente.Vivo(ct).ConfigureAwait(false))
        {
            _registro.Scrivi("motore", "uso un llama-server già acceso sulla porta " + porta);
            return true;
        }
        var si = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = RilevatoreModelli.Argomenti(exe, porta, preset),
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var path = si.WorkingDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        try
        {
            foreach (var sub in Directory.EnumerateDirectories(si.WorkingDirectory))
            {
                var nome = Path.GetFileName(sub);
                if (nome.Contains("cuda", StringComparison.OrdinalIgnoreCase))
                    path = sub + Path.PathSeparator + path;
            }
        }
        catch { /* la cartella del motore non si lascia elencare */ }
        si.Environment["PATH"] = path;
        // Cache vuota e niente rete: il router non deve vedere né scaricare modelli da Hugging Face.
        var cacheVuota = Path.Combine(_radiceDati, "motore", "cache-vuota");
        Directory.CreateDirectory(cacheVuota);
        si.Environment["LLAMA_CACHE"] = cacheVuota;
        si.Environment["HF_HUB_OFFLINE"] = "1";
        si.Environment["LLAMA_ARG_OFFLINE"] = "1";
        _processo = Process.Start(si);
        if (_processo is null) return false;
        _avviatoDaNoi = true;
        var log = Path.Combine(_radiceDati, "motore", "server.log");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        _ = Pompa(_processo.StandardOutput, log);
        _ = Pompa(_processo.StandardError, log);
        var fine = DateTime.UtcNow.AddSeconds(25);
        while (DateTime.UtcNow < fine)
        {
            ct.ThrowIfCancellationRequested();
            if (_processo.HasExited) break;
            if (await _cliente.Vivo(ct).ConfigureAwait(false))
            {
                _registro.Scrivi("motore", "llama-server acceso sulla porta " + porta, "ok");
                return true;
            }
            await Task.Delay(300, ct).ConfigureAwait(false);
        }
        UltimoErrore = "llama-server non si è acceso. Guarda motore/server.log.";
        _registro.SegnaErrore(UltimoErrore);
        if (_avviatoDaNoi) UccidiProcesso(_processo);
        _cliente?.Dispose();
        _cliente = null;
        _processo = null;
        _avviatoDaNoi = false;
        return false;
    }

    async Task<bool> AccendiUno(RuoloModello ruolo, CancellationToken ct)
    {
        if (_cliente is null)
        {
            UltimoErrore = "llama-server non è in ascolto.";
            return false;
        }
        var id = NomiModello.IdDi(ruolo);
        var slot = id + ".bin";
        _registro.Scrivi("modello", "chiedo il caricamento di " + id, "ok", ruolo.ToString());
        var ok = await _cliente.Carica(id, ct).ConfigureAwait(false);
        if (!ok)
        {
            UltimoErrore = _cliente.UltimoErrore ?? ("non sono riuscito a caricare " + id);
            _registro.SegnaErrore(UltimoErrore);
            return false;
        }
        UltimoErrore = null;
        _accesi.Add(ruolo);
        var fileSlot = Path.Combine(Percorsi.Slot(_radiceDati), slot);
        if (File.Exists(fileSlot))
        {
            try { await _cliente.RipristinaSlot(id, slot, ct).ConfigureAwait(false); }
            catch { /* lo slot vecchio può non andar bene dopo un aggiornamento del modello */ }
        }
        _registro.Scrivi("modello", "caricato " + NomiModello.Etichetta(ruolo), "ok", ruolo.ToString());
        return true;
    }

    async Task ScaricaUno(RuoloModello ruolo, CancellationToken ct)
    {
        if (_cliente is null) { _accesi.Remove(ruolo); return; }
        var id = NomiModello.IdDi(ruolo);
        try { await _cliente.SalvaSlot(id, id + ".bin", ct).ConfigureAwait(false); }
        catch { /* salvare lo slot è un di più: se fallisce si scarica lo stesso */ }
        try { await _cliente.Scarica(id, ct).ConfigureAwait(false); }
        catch (Exception e) { _registro.SegnaErrore("scarico " + id + ": " + e.Message); }
        _accesi.Remove(ruolo);
        _registro.Scrivi("modello", "scaricato " + NomiModello.Etichetta(ruolo), "ok", ruolo.ToString());
    }

    /// <summary>Chiude il processo. Il kill dell'albero, se resta fermo, non blocca chi ha chiesto di uscire.</summary>
    static void UccidiProcesso(Process? processo)
    {
        if (processo is null) return;
        try
        {
            if (processo.HasExited) return;
            var fatto = Task.Run(() =>
            {
                try { processo.Kill(entireProcessTree: true); }
                catch
                {
                    try { processo.Kill(); } catch { /* già chiuso */ }
                }
            });
            if (!fatto.Wait(4000))
            {
                try { processo.Kill(); } catch { /* già chiuso */ }
            }
        }
        catch { /* già chiuso */ }
    }

    static readonly object LucchettoLog = new();

    static async Task Pompa(StreamReader letto, string log)
    {
        try
        {
            while (true)
            {
                var riga = await letto.ReadLineAsync().ConfigureAwait(false);
                if (riga is null) break;
                lock (LucchettoLog) File.AppendAllText(log, riga + Environment.NewLine);
            }
        }
        catch { /* il processo è stato chiuso */ }
    }
}
