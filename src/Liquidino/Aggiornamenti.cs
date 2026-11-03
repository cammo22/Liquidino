using Velopack;
using Velopack.Sources;

namespace Liquidino;

/// <summary>
/// Unico contatto con la rete: le release di GitHub, per sapere se c'è una versione nuova.
/// Non è l'API di GitHub. Si legge releases.win.json, lo stesso file che pubblica Velopack.
/// Il controllo non blocca la finestra. Il tasto in alto dice «Aggiornamento X disponibile».
/// </summary>
public static class Aggiornamenti
{
    public static readonly TimeSpan OgniQuanto = TimeSpan.FromHours(4);
    public const string Pagina = "https://github.com/cammo22/Liquidino/releases/latest/download/";

    static readonly SemaphoreSlim _turno = new(1, 1);
    static readonly CancellationTokenSource _fermo = new();
    static UpdateInfo? _trovato;
    static VelopackAsset? _scaricato;
    static string? _disponibile;
    static int _avviato;
    static int _staApplicando;

    public static string? Disponibile => _disponibile;
    public static event Action<string>? Trovata;

    static UpdateManager Gestore() => new(new SimpleWebSource(new Uri(Pagina)));

    public static void Avvia()
    {
        if (Interlocked.Exchange(ref _avviato, 1) == 1) return;
        _ = Task.Run(() => Ciclo(_fermo.Token));
    }

    static async Task Ciclo(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Controlla(scarica: true, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch { /* senza rete si riprova fra quattro ore */ }
            try { await Task.Delay(OgniQuanto, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    public static async Task<string?> Controlla(bool scarica, CancellationToken ct)
    {
        await _turno.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var g = Gestore();
            if (!g.IsInstalled) return null;
            var nuovo = await g.CheckForUpdatesAsync().ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (nuovo is null) return null;
            _trovato = nuovo;
            var versione = nuovo.TargetFullRelease.Version.ToString();
            Segnala(versione);
            if (scarica && _scaricato?.Version != nuovo.TargetFullRelease.Version)
            {
                await g.DownloadUpdatesAsync(nuovo, cancelToken: ct).ConfigureAwait(false);
                _scaricato = nuovo.TargetFullRelease;
            }
            return versione;
        }
        finally { _turno.Release(); }
    }

    static void Segnala(string versione)
    {
        if (_disponibile == versione) return;
        _disponibile = versione;
        try { Trovata?.Invoke(versione); } catch { /* la finestra può essere già chiusa */ }
    }

    /// <summary>Il clic sul tasto in alto. Se c'è una versione, la installa e riapre. Altrimenti dice come stanno le cose.</summary>
    public static async Task<string> Installa()
    {
        var g = Gestore();
        if (!g.IsInstalled)
            return "Questa copia non è passata da Liquidino-Setup. Gli aggiornamenti automatici partono dopo quell'installazione.";
        await _turno.WaitAsync().ConfigureAwait(false);
        VelopackAsset? pacchetto;
        try
        {
            _trovato ??= await g.CheckForUpdatesAsync().ConfigureAwait(false);
            if (_trovato is null) return "Sei già all'ultima versione.";
            Segnala(_trovato.TargetFullRelease.Version.ToString());
            if (_scaricato?.Version != _trovato.TargetFullRelease.Version)
            {
                await g.DownloadUpdatesAsync(_trovato).ConfigureAwait(false);
                _scaricato = _trovato.TargetFullRelease;
            }
            pacchetto = _scaricato;
            if (pacchetto is not null) Interlocked.Exchange(ref _staApplicando, 1);
        }
        finally { _turno.Release(); }
        if (pacchetto is null) return "Sei già all'ultima versione.";
        try
        {
            Gestore().ApplyUpdatesAndRestart(pacchetto);
            return "Installo la versione nuova e riapro.";
        }
        catch
        {
            Interlocked.Exchange(ref _staApplicando, 0);
            throw;
        }
    }

    public static void AllaChiusura()
    {
        try { _fermo.Cancel(); } catch { /* il ciclo è già fermo */ }
        if (Volatile.Read(ref _staApplicando) == 1) return;
        if (_scaricato is null) return;
        if (!_turno.Wait(TimeSpan.FromSeconds(8))) return;
        try
        {
            if (!Gestore().IsInstalled) return;
            Gestore().WaitExitThenApplyUpdates(_scaricato, silent: true, restart: true, restartArgs: []);
        }
        catch { /* ci pensa il prossimo avvio */ }
        finally { _turno.Release(); }
    }
}
