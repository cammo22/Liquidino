using System.Diagnostics;

namespace Liquidino.Nucleo;

public interface IRegista
{
    bool MotorePresente { get; }
    bool Presente(RuoloModello ruolo);
    IReadOnlyCollection<RuoloModello> Accesi { get; }
    string Descrizione();
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

    public string Descrizione()
    {
        if (!MotorePresente) return "motore non trovato";
        if (_accesi.Count == 0) return "motore fermo, modelli spenti";
        return "accesi: " + string.Join(", ", _accesi.Select(NomiModello.Etichetta));
    }

    public void Rileggi() => _rilevazione = RilevatoreModelli.Cerca(_imp.CartellaMotore);

    public async Task<bool> Assicura(IReadOnlyList<RuoloModello> ruoli, CancellationToken ct)
    {
        await _porta.WaitAsync(ct);
        try { return await AssicuraInterno(ruoli, ct); }
        finally { _porta.Release(); }
    }

    async Task<bool> AssicuraInterno(IReadOnlyList<RuoloModello> ruoli, CancellationToken ct)
    {
        foreach (var r in ruoli.Distinct())
            if (!Presente(r)) return false;
        if (!await Garantito(ct)) return false;
        var piano = PoliticaCarico.Calcola(_accesi, ruoli);
        foreach (var spegni in piano.Spegni)
            await ScaricaUno(spegni, ct);
        foreach (var accendi in piano.Accendi)
        {
            if (!await AccendiUno(accendi, ct)) return false;
        }
        if (_accesi.Count > PoliticaCarico.MassimoInsieme)
            throw new InvalidOperationException("Mai più di due modelli insieme.");
        return ruoli.All(_accesi.Contains);
    }

    public async Task ScaricaTutto(CancellationToken ct)
    {
        await _porta.WaitAsync(ct);
        try
        {
            foreach (var r in _accesi.ToList())
                await ScaricaUno(r, ct);
        }
        catch (Exception e) { _registro.SegnaErrore("scarico: " + e.Message); }
        finally { _porta.Release(); }
    }

    public async Task Ferma()
    {
        try { await ScaricaTutto(CancellationToken.None); } catch { /* il processo sta per chiudersi */ }
        if (_avviatoDaNoi && _processo is { HasExited: false })
        {
            try { _processo.Kill(entireProcessTree: true); } catch { /* già chiuso */ }
            _registro.Scrivi("motore", "llama-server fermato", "ok");
        }
        _cliente?.Dispose();
        _cliente = null;
        _processo = null;
        _avviatoDaNoi = false;
    }

    public async Task<SceltaAzione?> Decidi(string stato, CatalogoAzioni catalogo, CancellationToken ct)
    {
        await _porta.WaitAsync(ct);
        try { return await DecidiInterno(stato, catalogo, ct); }
        finally { _porta.Release(); }
    }

    async Task<SceltaAzione?> DecidiInterno(string stato, CatalogoAzioni catalogo, CancellationToken ct)
    {
        if (_cliente is null || !Presente(RuoloModello.D1)) return null;
        var json = await _cliente.Decidi(NomiModello.IdDi(RuoloModello.D1), stato, catalogo, ct);
        return LettoreDecisione.Leggi(json, catalogo.Azioni.Select(a => a.Nome).ToList());
    }

    public async Task<RispostaChat?> Completa(RichiestaChat richiesta, CancellationToken ct)
    {
        await _porta.WaitAsync(ct);
        try
        {
            if (_cliente is null) return null;
            return await _cliente.Chat(NomiModello.IdDi(richiesta.Modello), richiesta, ct);
        }
        finally { _porta.Release(); }
    }

    public async Task<string?> Trascrivi(byte[] wav, CancellationToken ct)
    {
        await _porta.WaitAsync(ct);
        try
        {
            if (!Presente(RuoloModello.Omni)) return null;
            if (!await AssicuraInterno([RuoloModello.Omni], ct)) return null;
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
                true), ct);
            var testo = Testo.PulisciRisposta(risp.Testo);
            return string.IsNullOrWhiteSpace(testo) ? null : testo;
        }
        finally { _porta.Release(); }
    }

    public async Task<byte[]?> Sintetizza(string testo, string voce, CancellationToken ct)
    {
        await _porta.WaitAsync(ct);
        try
        {
            if (!Presente(RuoloModello.Omni)) return null;
            if (!await AssicuraInterno([RuoloModello.Omni], ct) || _cliente is null) return null;
            return await _cliente.Sintetizza(NomiModello.IdDi(RuoloModello.Omni), testo, voce, ct);
        }
        finally { _porta.Release(); }
    }

    async Task<bool> Garantito(CancellationToken ct)
    {
        if (_cliente is not null && await _cliente.Vivo(ct)) return true;
        _cliente?.Dispose();
        _cliente = null;
        _accesi.Clear();
        if (_rilevazione.Eseguibile is null) return false;
        var preset = Percorsi.Preset(_radiceDati);
        PresetMotore.Scrivi(preset, Percorsi.Slot(_radiceDati), _rilevazione.Modelli, _imp);
        var exe = _rilevazione.Eseguibile;
        var porta = _imp.Porta;
        _cliente = new ClienteLlama(porta);
        if (await _cliente.Vivo(ct))
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
            if (await _cliente.Vivo(CancellationToken.None)) 
            {
                _registro.Scrivi("motore", "llama-server acceso sulla porta " + porta, "ok");
                return true;
            }
            await Task.Delay(300, ct);
        }
        _registro.SegnaErrore("llama-server non si è acceso. Guarda motore/server.log.");
        if (_avviatoDaNoi && _processo is { HasExited: false })
        {
            try { _processo.Kill(entireProcessTree: true); } catch { /* già chiuso */ }
        }
        _cliente?.Dispose();
        _cliente = null;
        _processo = null;
        _avviatoDaNoi = false;
        return false;
    }

    async Task<bool> AccendiUno(RuoloModello ruolo, CancellationToken ct)
    {
        if (_cliente is null) return false;
        var id = NomiModello.IdDi(ruolo);
        var slot = id + ".bin";
        var ok = await _cliente.Carica(id, ct);
        if (!ok)
        {
            _registro.SegnaErrore("non sono riuscito a caricare " + id);
            return false;
        }
        _accesi.Add(ruolo);
        var fileSlot = Path.Combine(Percorsi.Slot(_radiceDati), slot);
        if (File.Exists(fileSlot))
        {
            try { await _cliente.RipristinaSlot(id, slot, ct); }
            catch { /* lo slot vecchio può non andar bene dopo un aggiornamento del modello */ }
        }
        _registro.Scrivi("modello", "caricato " + NomiModello.Etichetta(ruolo), "ok", ruolo.ToString());
        return true;
    }

    async Task ScaricaUno(RuoloModello ruolo, CancellationToken ct)
    {
        if (_cliente is null) { _accesi.Remove(ruolo); return; }
        var id = NomiModello.IdDi(ruolo);
        try { await _cliente.SalvaSlot(id, id + ".bin", ct); }
        catch { /* salvare lo slot è un di più: se fallisce si scarica lo stesso */ }
        try { await _cliente.Scarica(id, ct); }
        catch (Exception e) { _registro.SegnaErrore("scarico " + id + ": " + e.Message); }
        _accesi.Remove(ruolo);
        _registro.Scrivi("modello", "scaricato " + NomiModello.Etichetta(ruolo), "ok", ruolo.ToString());
    }

    static readonly object LucchettoLog = new();

    static async Task Pompa(StreamReader letto, string log)
    {
        try
        {
            while (!letto.EndOfStream)
            {
                var riga = await letto.ReadLineAsync();
                if (riga is null) break;
                lock (LucchettoLog) File.AppendAllText(log, riga + Environment.NewLine);
            }
        }
        catch { /* il processo è stato chiuso */ }
    }
}
