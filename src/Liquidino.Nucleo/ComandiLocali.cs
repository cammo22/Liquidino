namespace Liquidino.Nucleo;

/// <summary>
/// Permesso e STOP, senza restare appesi. Il secondo STOP non aspetta il primo:
/// annulla quello in corso e torna subito. Il lavoro vero ha un tempo massimo.
/// </summary>
public sealed class ComandiLocali
{
    int _dentro;
    public bool Permesso { get; private set; }
    public int StopPartiti { get; private set; }

    public void AlternaPermesso() => Permesso = !Permesso;

    public void ImpostaPermesso(bool acceso) => Permesso = acceso;

    /// <summary>
    /// Scarica i modelli, al massimo per il tempo indicato. Se uno STOP è già in corso,
    /// la nuova chiamata esce subito e lascia finire la prima.
    /// </summary>
    public async Task StopAsync(Func<CancellationToken, Task> scarica, TimeSpan tempoMassimo)
    {
        if (Interlocked.Exchange(ref _dentro, 1) == 1)
            return;

        StopPartiti++;
        using var tempo = new CancellationTokenSource();
        if (tempoMassimo > TimeSpan.Zero) tempo.CancelAfter(tempoMassimo);
        try
        {
            await scarica(tempo.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            /* tempo scaduto: la finestra non resta ad aspettare */
        }
        finally
        {
            Interlocked.Exchange(ref _dentro, 0);
        }
    }
}
