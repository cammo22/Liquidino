using System.Windows;
using Liquidino.Nucleo;

namespace Liquidino;

/// <summary>
/// Una strada sola per scaricare un modello, dalle luci dell'avatar e dalla pagina Modelli:
/// chiede a Hugging Face, ti chiede il sì col peso vero, scarica con l'impronta controllata, riaccende il motore.
/// Un download alla volta.
/// </summary>
public static class Scaricamenti
{
    static CancellationTokenSource? _incorso;
    public static string? InCorso { get; private set; }

    public static void Annulla() => _incorso?.Cancel();

    /// <summary>Ritorna la frase da mostrare alla fine (o «Questo fallo tu: …»).</summary>
    public static async Task<string> Scarica(Window? padre, Ponte ponte, SchedaCatalogo? lfm, SchedaFissa? fissa, Action<string> avanzamento)
    {
        var nome = lfm?.Nome ?? fissa!.Nome;
        var repo = lfm?.Repo ?? fissa!.Repo;
        if (_incorso is not null) return "Sto già scaricando " + InCorso + ": aspetta che finisca.";
        _incorso = new CancellationTokenSource();
        InCorso = nome;
        var ct = _incorso.Token;
        var cartellaModelli = ponte.Regista.Rilevazione.CartellaModelli;
        try
        {
            using var scaricatore = new ScaricatoreModelli();
            avanzamento("Chiedo a Hugging Face i file di " + nome + "…");
            string elenco;
            long totale;
            string revisione;
            Func<IProgress<Avanzamento>, Task> via;
            if (lfm is not null)
            {
                var scelta = await scaricatore.Prepara(lfm, ct);
                if (scelta is null) return Testo.FalloTu("in " + repo + " non trovo un GGUF da scaricare. Mettilo a mano nella cartella dei modelli");
                elenco = "• " + scelta.Pesi.Percorso + " (" + CatalogoModelli.Peso(scelta.Pesi.Byte) + ")"
                         + (scelta.Proiettore is null ? "" : "\n• " + scelta.Proiettore.Percorso + " (" + CatalogoModelli.Peso(scelta.Proiettore.Byte) + ")");
                totale = scelta.Totale;
                revisione = scelta.Revisione;
                via = p => scaricatore.Scarica(scelta, cartellaModelli, p, ct);
            }
            else
            {
                var scelta = await scaricatore.Prepara(fissa!, ct);
                if (scelta is null) return Testo.FalloTu("in " + repo + " manca uno dei file che servono, o non ha l'impronta");
                elenco = string.Join("\n", scelta.File.Select(f => "• " + f.Percorso + " (" + CatalogoModelli.Peso(f.Byte) + ")"));
                totale = scelta.Totale;
                revisione = scelta.Revisione;
                via = p => scaricatore.Scarica(scelta, cartellaModelli, p, ct);
            }
            var domanda = "Scarico " + nome + " da huggingface.co/" + repo + "?\n\n" + elenco + "\n\nTotale " + CatalogoModelli.Peso(totale)
                          + ", revisione " + revisione[..7] + ".\nVa in " + cartellaModelli + ".";
            var risposta = padre is not null
                ? MessageBox.Show(padre, domanda, "Liquidino · Scarica", MessageBoxButton.YesNo, MessageBoxImage.Question)
                : MessageBox.Show(domanda, "Liquidino · Scarica", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (risposta != MessageBoxResult.Yes) return "Va bene, non scarico niente.";
            ponte.Registro.Scrivi("modelli", "scarico " + nome + " (" + CatalogoModelli.Peso(totale) + ")", "in corso");
            await via(new Progress<Avanzamento>(a => avanzamento(nome + " · " + CatalogoModelli.Peso(a.Fatti) + " di " + CatalogoModelli.Peso(a.Totale)
                + (a.Totale > 0 ? " · " + (int)(a.Fatti * 100 / a.Totale) + "%" : ""))));
            ponte.Registro.Scrivi("modelli", "installato " + nome, "ok");
            if (lfm is not null) await ponte.RicaricaMotore();
            return nome + " è installato e controllato.";
        }
        catch (OperationCanceledException)
        {
            ponte.Registro.Scrivi("modelli", "download di " + nome + " annullato", "annullato");
            return "Download annullato. Il file a metà è stato tolto.";
        }
        catch (Exception e)
        {
            ponte.Registro.SegnaErrore("download " + nome + ": " + e.Message);
            return Testo.FalloTu("non sono riuscito a scaricare " + nome + ". " + e.Message);
        }
        finally
        {
            _incorso?.Dispose();
            _incorso = null;
            InCorso = null;
        }
    }
}
