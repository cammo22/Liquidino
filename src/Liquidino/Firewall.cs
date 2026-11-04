using System.ComponentModel;
using System.Diagnostics;
using Liquidino.Nucleo;

namespace Liquidino;

/// <summary>
/// La regola di Windows Firewall per llama-server. Leggerla non chiede niente; crearla chiede l'amministratore
/// una volta sola (la finestra di Windows «Vuoi consentire…»). Se dici no, non succede niente.
/// </summary>
public static class Firewall
{
    /// <summary>Vero se la regola c'è per quell'eseguibile, falso se manca, null se non si riesce a sapere.</summary>
    public static async Task<bool?> Controlla(string eseguibile)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("powershell.exe", RegolaFirewall.Argomenti(RegolaFirewall.ScriptControlla(eseguibile)))
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p is null) return null;
            using var tempo = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try { await p.WaitForExitAsync(tempo.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                try { p.Kill(); } catch { /* già uscito */ }
                return null;
            }
            return p.ExitCode switch
            {
                0 => true,
                3 or 4 => false,
                _ => null
            };
        }
        catch { return null; }
    }

    /// <summary>Crea (o rifà) la regola. Ritorna la frase da mostrare.</summary>
    public static async Task<string> Crea(string eseguibile)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("powershell.exe", RegolaFirewall.Argomenti(RegolaFirewall.ScriptCrea(eseguibile)))
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });
            if (p is null) return "Questo fallo tu: Windows non ha avviato PowerShell.";
            using var tempo = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            await p.WaitForExitAsync(tempo.Token).ConfigureAwait(false);
            return p.ExitCode == 0
                ? "Fatto: llama-server non può più andare in internet. Con me continua a parlare, su 127.0.0.1."
                : "Questo fallo tu: PowerShell ha risposto con l'errore " + p.ExitCode + ". La regola non c'è.";
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 1223)
        {
            return "Hai detto no alla richiesta di Windows: la regola non c'è, e va bene così.";
        }
        catch (OperationCanceledException)
        {
            return "Questo fallo tu: PowerShell non ha finito in tempo. Riprova.";
        }
        catch (Exception e)
        {
            return "Questo fallo tu: non sono riuscito a creare la regola. " + e.Message;
        }
    }
}
