using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Liquidino.Nucleo;

namespace Liquidino;

/// <summary>
/// Tutti i modelli di Liquidino in una pagina: a cosa serve ognuno, se c'è, quanto pesa.
/// Scarica (solo col tuo clic e il tuo sì), Elimina (nel Cestino di Windows) e le cartelle a portata di mano.
/// </summary>
public sealed class FinestraModelli : Window
{
    readonly Ponte _ponte;
    readonly StackPanel _elenco = new();
    readonly TextBlock _stato;
    readonly TextBlock _esito;
    CancellationTokenSource? _download;

    internal int Schede => CatalogoModelli.Tutti.Count;

    public FinestraModelli(Ponte ponte)
    {
        _ponte = ponte;
        Title = "Modelli";
        Width = 820;
        Height = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;

        _stato = Mainframe.Nota("");
        _esito = Mainframe.Nota("");
        _esito.Foreground = Mainframe.CianoP;

        var spiegazione = Mainframe.Nota(
            "I modelli sono i «cervelli» di Liquidino: file GGUF di Liquid AI che girano sul tuo PC con llama.cpp. " +
            "Ognuno fa un mestiere: qui sotto c'è scritto quale. Non serve averli tutti: per parlare bastano Omni oppure LFM3-1.7B. " +
            "Niente si scarica da solo: Scarica chiede prima il tuo sì e dice quanto pesa. I file arrivano da huggingface.co, " +
            "da una revisione fissata, e si installano solo se la loro impronta SHA-256 è quella dichiarata. Elimina li manda nel Cestino di Windows.");
        spiegazione.Foreground = Stile.Inchiostro;
        spiegazione.FontSize = 15;

        var cartelle = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        var modelli = Mainframe.Tasto("Cartella dei modelli");
        modelli.Click += (_, _) => Apri(_ponte.Regista.Rilevazione.CartellaModelli);
        var motore = Mainframe.Tasto("Cartella del motore");
        motore.Click += (_, _) => Apri(_ponte.Impostazioni.CartellaMotore);
        var dati = Mainframe.Tasto("Cartella dei dati");
        dati.ToolTip = Percorsi.RadiceDati();
        dati.Click += (_, _) => Apri(Percorsi.RadiceDati());
        var programma = Mainframe.Tasto("Cartella del programma");
        programma.ToolTip = AppContext.BaseDirectory;
        programma.Click += (_, _) => Apri(AppContext.BaseDirectory);
        foreach (var t in new[] { modelli, motore, dati, programma }) cartelle.Children.Add(t);

        var testa = Mainframe.Sezione("Dove stanno", out var dove);
        dove.Children.Add(spiegazione);
        dove.Children.Add(_stato);
        dove.Children.Add(cartelle);

        var corpo = new StackPanel { Margin = new Thickness(20, 0, 20, 0) };
        corpo.Children.Add(testa);
        corpo.Children.Add(_elenco);

        var chiudi = Mainframe.Tasto("Chiudi", pericolo: true);
        chiudi.Click += (_, _) => Close();
        var testata = Mainframe.Testata("MODELLI", chiudi);
        var piede = new Border { Margin = new Thickness(20, 4, 20, 12), Child = _esito };

        var tutto = new DockPanel();
        DockPanel.SetDock(testata, Dock.Top);
        DockPanel.SetDock(piede, Dock.Bottom);
        tutto.Children.Add(testata);
        tutto.Children.Add(piede);
        tutto.Children.Add(new ScrollViewer { Content = corpo, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = Mainframe.Cornice(tutto);

        Riempi();
        Closing += (_, _) => _download?.Cancel();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is Button) return;
            try { DragMove(); } catch { /* già rilasciato */ }
        };
    }

    void Riempi()
    {
        var r = _ponte.Regista.Rilevazione;
        _stato.Text = (r.Eseguibile is null
                ? "Motore: llama-server non trovato in " + _ponte.Impostazioni.CartellaMotore + ". Senza motore i modelli non partono: indica la cartella giusta nel Mainframe."
                : "Motore: " + r.Eseguibile + ".")
            + " Modelli in: " + r.CartellaModelli + ".";
        _elenco.Children.Clear();
        foreach (var scheda in CatalogoModelli.Tutti)
            _elenco.Children.Add(Scheda(scheda, r.Modelli.FirstOrDefault(m => m.Ruolo == scheda.Ruolo), r.CartellaModelli));
    }

    Border Scheda(SchedaCatalogo scheda, SchedaModello? installato, string cartellaModelli)
    {
        var sezione = Mainframe.Sezione(scheda.Nome + "  ·  " + NomiModello.Etichetta(scheda.Ruolo), out var righe);
        righe.Children.Add(Mainframe.Nota(scheda.APerCosa + (scheda.NelGiro ? "" : " (Non serve per usare Liquidino oggi.)")));

        string stato;
        if (installato is null) stato = "non installato";
        else
        {
            long peso = 0;
            foreach (var f in new[] { installato.Percorso, installato.Mmproj })
                if (!string.IsNullOrWhiteSpace(f) && File.Exists(f)) peso += new FileInfo(f).Length;
            stato = "installato · " + installato.NomeFile + (installato.Mmproj is null ? "" : " + proiettore") + " · " + CatalogoModelli.Peso(peso);
        }
        righe.Children.Add(Mainframe.Riga("Stato", new TextBlock
        {
            Text = stato,
            FontFamily = Stile.Testo,
            FontSize = 15,
            Foreground = installato is null ? Stile.Inchiostro2 : Mainframe.CianoP,
            TextWrapping = TextWrapping.Wrap
        }));
        righe.Children.Add(Mainframe.Riga("Da", new TextBlock
        {
            Text = "huggingface.co/" + scheda.Repo,
            FontFamily = Stile.Testo,
            FontSize = 15,
            Foreground = Stile.Inchiostro2,
            TextTrimming = TextTrimming.CharacterEllipsis
        }));

        var avanzamento = Mainframe.Nota("");
        var tasti = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        if (installato is null)
        {
            var scarica = Mainframe.Tasto("Scarica");
            scarica.Click += async (_, _) => await Scarica(scheda, cartellaModelli, scarica, avanzamento);
            tasti.Children.Add(scarica);
        }
        else
        {
            var elimina = Mainframe.Tasto("Elimina", pericolo: true);
            elimina.Click += async (_, _) => await Elimina(scheda, installato, cartellaModelli);
            tasti.Children.Add(elimina);
            var cartella = Mainframe.Tasto("Apri la sua cartella");
            cartella.Click += (_, _) => Apri(Path.GetDirectoryName(installato.Percorso) ?? cartellaModelli);
            tasti.Children.Add(cartella);
        }
        righe.Children.Add(tasti);
        righe.Children.Add(avanzamento);
        return sezione;
    }

    async Task Scarica(SchedaCatalogo scheda, string cartellaModelli, Button tasto, TextBlock avanzamento)
    {
        if (_download is not null)
        {
            if (ReferenceEquals(tasto.Tag, "in corso")) { _download.Cancel(); avanzamento.Text = "Annullo…"; }
            else _esito.Text = "Sto già scaricando un altro modello: aspetta che finisca.";
            return;
        }
        _download = new CancellationTokenSource();
        var ct = _download.Token;
        tasto.Tag = "in corso";
        tasto.Content = "Annulla";
        try
        {
            using var scaricatore = new ScaricatoreModelli();
            avanzamento.Text = "Chiedo a Hugging Face quali file ci sono…";
            var scelta = await scaricatore.Prepara(scheda, ct);
            if (scelta is null)
            {
                avanzamento.Text = "Questo fallo tu: in " + scheda.Repo + " non trovo un GGUF da scaricare. Mettilo a mano nella cartella dei modelli.";
                return;
            }
            var domanda = "Scarico " + scheda.Nome + " da huggingface.co/" + scheda.Repo + ":\n\n"
                + "• " + scelta.Pesi.Percorso + " (" + CatalogoModelli.Peso(scelta.Pesi.Byte) + ")\n"
                + (scelta.Proiettore is null ? "" : "• " + scelta.Proiettore.Percorso + " (" + CatalogoModelli.Peso(scelta.Proiettore.Byte) + ")\n")
                + "\nTotale " + CatalogoModelli.Peso(scelta.Totale) + ", revisione " + scelta.Revisione[..7] + ".\nVa in " + scheda.Cartella(cartellaModelli) + ".\n\nProcedo?";
            if (MessageBox.Show(this, domanda, "Liquidino · Scarica", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                avanzamento.Text = "Va bene, non scarico niente.";
                return;
            }
            _ponte.Registro.Scrivi("modelli", "scarico " + scheda.Nome + " (" + CatalogoModelli.Peso(scelta.Totale) + ")", "in corso");
            var progresso = new Progress<Avanzamento>(a => avanzamento.Text = a.File + " · " + CatalogoModelli.Peso(a.Fatti) + " di " + CatalogoModelli.Peso(a.Totale)
                + (a.Totale > 0 ? " · " + (int)(a.Fatti * 100 / a.Totale) + "%" : ""));
            await scaricatore.Scarica(scelta, cartellaModelli, progresso, ct);
            _ponte.Registro.Scrivi("modelli", "installato " + scheda.Nome, "ok");
            _esito.Text = scheda.Nome + " è installato e controllato. Riaccendo il motore perché lo trovi.";
            await _ponte.RicaricaMotore();
            Riempi();
        }
        catch (OperationCanceledException)
        {
            avanzamento.Text = "Annullato. Il file a metà è stato tolto.";
            _ponte.Registro.Scrivi("modelli", "download di " + scheda.Nome + " annullato", "annullato");
        }
        catch (Exception e)
        {
            avanzamento.Text = "Questo fallo tu: non sono riuscito a scaricarlo. " + e.Message;
            _ponte.Registro.SegnaErrore("download " + scheda.Nome + ": " + e.Message);
        }
        finally
        {
            _download?.Dispose();
            _download = null;
            tasto.Tag = null;
            if (tasto.IsLoaded) tasto.Content = "Scarica";
        }
    }

    async Task Elimina(SchedaCatalogo scheda, SchedaModello installato, string cartellaModelli)
    {
        var file = CatalogoModelli.FileDa(installato, cartellaModelli);
        if (file.Count == 0)
        {
            _esito.Text = "Questo fallo tu: " + installato.NomeFile + " non sta nella cartella dei modelli, quindi non lo tocco.";
            return;
        }
        var domanda = "Elimino " + scheda.Nome + "?\n\n" + string.Join("\n", file.Select(f => "• " + Path.GetFileName(f)))
            + "\n\nI file vanno nel Cestino di Windows: se cambi idea li ripristini da lì.";
        if (MessageBox.Show(this, domanda, "Liquidino · Elimina", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            _esito.Text = "Spengo il motore, così i file si possono spostare…";
            await _ponte.Regista.Ferma();
            await Task.Run(() =>
            {
                foreach (var f in file)
                    if (File.Exists(f))
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(f, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                var cartella = scheda.Cartella(cartellaModelli);
                if (Directory.Exists(cartella) && !Directory.EnumerateFileSystemEntries(cartella).Any()) Directory.Delete(cartella);
            });
            _ponte.Registro.Scrivi("modelli", "eliminato " + scheda.Nome + " (nel Cestino)", "ok");
            _esito.Text = scheda.Nome + " è nel Cestino di Windows.";
            await _ponte.RicaricaMotore();
            Riempi();
        }
        catch (Exception e)
        {
            _esito.Text = "Questo fallo tu: non sono riuscito a eliminarlo. " + e.Message;
            _ponte.Registro.SegnaErrore("elimina " + scheda.Nome + ": " + e.Message);
        }
    }

    void Apri(string cartella)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cartella)) throw new InvalidOperationException("la cartella non è indicata");
            Directory.CreateDirectory(cartella);
            using (Process.Start(new ProcessStartInfo("explorer.exe", "\"" + cartella + "\"") { UseShellExecute = true })) { }
        }
        catch (Exception e) { _esito.Text = "Questo fallo tu: non riesco ad aprire " + cartella + ". " + e.Message; }
    }
}
