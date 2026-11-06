using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Liquidino.Nucleo;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace Liquidino;

/// <summary>
/// Tutti i modelli in una tabella compatta, una riga ciascuno: luce, nome, a cosa serve, stato e tasti.
/// Sta in una schermata. La usano la finestra Modelli e la scheda Modelli delle impostazioni.
/// </summary>
public sealed class PannelloModelli : DockPanel
{
    readonly Ponte _ponte;
    readonly StackPanel _righe = new();
    readonly TextBlock _esito;
    readonly TextBlock _dove;

    public int Righe => _righe.Children.Count;

    public PannelloModelli(Ponte ponte)
    {
        _ponte = ponte;
        _esito = Mainframe.Nota("");
        _esito.Foreground = Stile.CianoP;
        _dove = Mainframe.Nota("");

        var spiega = Mainframe.Nota("Luce piena: acceso in memoria. Luce vuota: installato, spento. Tratteggio: da scaricare. "
            + "Si scarica solo col tuo clic e il tuo sì, da huggingface.co, e si installa solo se l'impronta torna. Elimina manda nel Cestino.");
        var cartelle = new WrapPanel();
        foreach (var (nome, dove) in new (string, Func<string>)[]
                 {
                     ("Cartella dei modelli", () => _ponte.Regista.Rilevazione.CartellaModelli),
                     ("Cartella del motore", () => _ponte.Impostazioni.CartellaMotore),
                     ("Cartella dei dati", Percorsi.RadiceDati),
                     ("Cartella del programma", () => AppContext.BaseDirectory)
                 })
        {
            var t = Mainframe.Tasto(nome);
            t.Click += (_, _) => Apri(dove());
            cartelle.Children.Add(t);
        }
        var testa = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        testa.Children.Add(spiega);
        testa.Children.Add(_dove);
        testa.Children.Add(cartelle);
        SetDock(testa, Dock.Top);
        SetDock(_esito, Dock.Bottom);
        Children.Add(testa);
        Children.Add(_esito);
        Children.Add(new Border { Background = Mainframe.BandaP, CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 4, 6, 4), Child = _righe });

        _ponte.ModelliCambiati += Riempi;
        Unloaded += (_, _) => _ponte.ModelliCambiati -= Riempi;
        Riempi();
    }

    void Riempi()
    {
        var r = _ponte.Regista.Rilevazione;
        _dove.Text = (r.Eseguibile is null ? "llama-server non trovato in " + _ponte.Impostazioni.CartellaMotore + "." : "Motore: " + r.Eseguibile + ".")
                     + " Modelli in: " + r.CartellaModelli + "." + (_ponte.Regista.InPausa ? " In pausa: niente si carica da solo." : "");
        _righe.Children.Clear();
        foreach (var s in CatalogoModelli.Tutti)
            _righe.Children.Add(RigaLfm(s, r.Modelli.FirstOrDefault(m => m.Ruolo == s.Ruolo), r.CartellaModelli));
        _righe.Children.Add(RigaFissa(CatalogoOrecchio.Parakeet, r.CartellaModelli));
    }

    static string ColoreDi(RuoloModello r) => r switch
    {
        RuoloModello.Mani => Stile.Oro,
        RuoloModello.Omni => Stile.Magenta,
        RuoloModello.D1 => Stile.Ciano,
        RuoloModello.Vl => Stile.Ambra,
        RuoloModello.Audio => Stile.Verde,
        _ => "#b48cff"
    };

    Grid Riga(string colore, bool acceso, bool presente, string nome, string ruolo, string aCosa, string stato, params Button[] tasti)
    {
        var g = new Grid { Margin = new Thickness(0, 1, 0, 1), MinHeight = 34 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var pennello = Stile.Pennello(colore);
        var luce = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = acceso ? pennello : presente ? new SolidColorBrush(Color.FromArgb(60, pennello.Color.R, pennello.Color.G, pennello.Color.B)) : Brushes.Transparent,
            Stroke = presente ? pennello : Stile.Inchiostro3,
            StrokeThickness = 1.5,
            StrokeDashArray = presente ? null : new DoubleCollection { 2, 2 },
            Effect = acceso ? new DropShadowEffect { Color = pennello.Color, BlurRadius = 10, ShadowDepth = 0 } : null,
            VerticalAlignment = VerticalAlignment.Center
        };
        g.Children.Add(luce);
        var nomi = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        nomi.Children.Add(new TextBlock { Text = nome, FontFamily = Stile.Testo, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Stile.Inchiostro });
        nomi.Children.Add(new TextBlock { Text = ruolo, FontFamily = Stile.Testo, FontSize = 12, Foreground = pennello });
        Grid.SetColumn(nomi, 1);
        g.Children.Add(nomi);
        var cosa = new TextBlock { Text = aCosa, FontFamily = Stile.Testo, FontSize = 13, Foreground = Stile.Inchiostro2, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, ToolTip = aCosa, Margin = new Thickness(0, 0, 8, 0) };
        Grid.SetColumn(cosa, 2);
        g.Children.Add(cosa);
        var st = new TextBlock { Text = stato, FontFamily = Stile.Testo, FontSize = 13, Foreground = acceso ? pennello : presente ? Stile.Inchiostro : Stile.Inchiostro3, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = stato };
        Grid.SetColumn(st, 3);
        g.Children.Add(st);
        var pannello = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var t in tasti) { t.Margin = new Thickness(4, 0, 0, 0); pannello.Children.Add(t); }
        Grid.SetColumn(pannello, 4);
        g.Children.Add(pannello);
        return g;
    }

    Grid RigaLfm(SchedaCatalogo scheda, SchedaModello? installato, string cartellaModelli)
    {
        var acceso = _ponte.Regista.Accesi.Contains(scheda.Ruolo);
        string stato;
        var tasti = new List<Button>();
        if (installato is null)
        {
            stato = "da scaricare";
            var scarica = Mainframe.Tasto("Scarica", principale: true);
            scarica.Click += async (_, _) => await Scarica(scheda, null);
            tasti.Add(scarica);
        }
        else
        {
            long peso = 0;
            foreach (var f in new[] { installato.Percorso, installato.Mmproj })
                if (!string.IsNullOrWhiteSpace(f) && File.Exists(f)) peso += new FileInfo(f).Length;
            stato = (acceso ? "acceso · " : "spento · ") + CatalogoModelli.Peso(peso);
            var accendi = Mainframe.Tasto(acceso ? "Spegni" : "Accendi");
            accendi.Click += async (_, _) => _esito.Text = await _ponte.AlternaModello(scheda.Ruolo);
            var elimina = Mainframe.Tasto("Elimina", pericolo: true);
            elimina.Click += async (_, _) => await EliminaLfm(scheda, installato, cartellaModelli);
            tasti.Add(accendi);
            tasti.Add(elimina);
        }
        return Riga(ColoreDi(scheda.Ruolo), acceso, installato is not null, scheda.Nome, NomiModello.Etichetta(scheda.Ruolo),
            scheda.APerCosa + (scheda.NelGiro ? "" : " (Non serve per usare Liquidino oggi.)"), stato, tasti.ToArray());
    }

    Grid RigaFissa(SchedaFissa scheda, string cartellaModelli)
    {
        var presente = scheda.Presente(cartellaModelli);
        var acceso = _ponte.Udito.Accese;
        var tasti = new List<Button>();
        string stato;
        if (!presente)
        {
            stato = "da scaricare";
            var scarica = Mainframe.Tasto("Scarica", principale: true);
            scarica.Click += async (_, _) => await Scarica(null, scheda);
            tasti.Add(scarica);
        }
        else
        {
            long peso = 0;
            foreach (var f in scheda.File) peso += new FileInfo(Path.Combine(scheda.Cartella(cartellaModelli), f)).Length;
            stato = (acceso ? "acceso · " : "spento · ") + CatalogoModelli.Peso(peso);
            var orecchie = Mainframe.Tasto(acceso ? "Spegni" : "Accendi");
            orecchie.ToolTip = "Accende o spegne le Orecchie.";
            orecchie.Click += async (_, _) => { await _ponte.AlternaOrecchie(); Riempi(); };
            var elimina = Mainframe.Tasto("Elimina", pericolo: true);
            elimina.Click += async (_, _) => await EliminaFissa(scheda, cartellaModelli);
            tasti.Add(orecchie);
            tasti.Add(elimina);
        }
        return Riga("#e7e0d2", acceso, presente, scheda.Nome, "orecchie · CPU", scheda.APerCosa, stato, tasti.ToArray());
    }

    async Task Scarica(SchedaCatalogo? lfm, SchedaFissa? fissa)
    {
        if (Scaricamenti.InCorso is not null)
        {
            Scaricamenti.Annulla();
            _esito.Text = "Fermo il download…";
            return;
        }
        _esito.Text = await Scaricamenti.Scarica(Window.GetWindow(this), _ponte, lfm, fissa, testo => Dispatcher.BeginInvoke(() => _esito.Text = testo + "  (premi un altro Scarica per fermarlo)"));
        _ponte.Regista.Rileggi();
        Riempi();
    }

    async Task EliminaLfm(SchedaCatalogo scheda, SchedaModello installato, string cartellaModelli)
    {
        var file = CatalogoModelli.FileDa(installato, cartellaModelli);
        if (file.Count == 0)
        {
            _esito.Text = Testo.FalloTu(installato.NomeFile + " non sta nella cartella dei modelli, quindi non lo tocco");
            return;
        }
        var domanda = "Elimino " + scheda.Nome + "?\n\n" + string.Join("\n", file.Select(f => "• " + Path.GetFileName(f)))
                      + "\n\nI file vanno nel Cestino di Windows: se cambi idea li ripristini da lì.";
        if (MessageBox.Show(Window.GetWindow(this)!, domanda, "Liquidino · Elimina", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            _esito.Text = "Spengo il motore, così i file si possono spostare…";
            await _ponte.Regista.Ferma();
            await Task.Run(() =>
            {
                foreach (var f in file)
                    if (File.Exists(f)) Cestino(f);
                var cartella = scheda.Cartella(cartellaModelli);
                if (Directory.Exists(cartella) && !Directory.EnumerateFileSystemEntries(cartella).Any()) Directory.Delete(cartella);
            });
            _ponte.Registro.Scrivi("modelli", "eliminato " + scheda.Nome + " (nel Cestino)", "ok");
            await _ponte.RicaricaMotore();
            _esito.Text = scheda.Nome + " è nel Cestino di Windows.";
            Riempi();
        }
        catch (Exception e)
        {
            _esito.Text = Testo.FalloTu("non sono riuscito a eliminarlo. " + e.Message);
            _ponte.Registro.SegnaErrore("elimina " + scheda.Nome + ": " + e.Message);
        }
    }

    async Task EliminaFissa(SchedaFissa scheda, string cartellaModelli)
    {
        var cartella = scheda.Cartella(cartellaModelli);
        if (!Percorsi.StaDentro(cartellaModelli, cartella)) return;
        var domanda = "Elimino " + scheda.Nome + "?\n\n" + string.Join("\n", scheda.File.Select(f => "• " + f))
                      + "\n\nLe Orecchie si spengono. I file vanno nel Cestino di Windows: se cambi idea li ripristini da lì.";
        if (MessageBox.Show(Window.GetWindow(this)!, domanda, "Liquidino · Elimina", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            // Parakeet tiene aperti i file: prima si chiude, poi si spostano.
            var eranoAccese = _ponte.Udito.Accese;
            await Task.Run(() => _ponte.Udito.Dispose());
            if (eranoAccese)
            {
                _ponte.Impostazioni.OrecchieAccese = false;
                _ponte.Impostazioni.Salva();
            }
            await Task.Run(() =>
            {
                foreach (var f in scheda.File.Select(f => Path.Combine(cartella, f)))
                    if (File.Exists(f) && Percorsi.StaDentro(cartella, f)) Cestino(f);
                if (Directory.Exists(cartella) && !Directory.EnumerateFileSystemEntries(cartella).Any()) Directory.Delete(cartella);
            });
            _ponte.Registro.Scrivi("modelli", "eliminato " + scheda.Nome + " (nel Cestino)", "ok");
            _esito.Text = scheda.Nome + " è nel Cestino di Windows. Le Orecchie restano spente finché non lo riscarichi.";
            Riempi();
        }
        catch (Exception e)
        {
            _esito.Text = Testo.FalloTu("non sono riuscito a eliminarlo. " + e.Message);
            _ponte.Registro.SegnaErrore("elimina " + scheda.Nome + ": " + e.Message);
        }
    }

    static void Cestino(string file) =>
        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(file, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

    void Apri(string cartella)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cartella)) throw new InvalidOperationException("la cartella non è indicata");
            Directory.CreateDirectory(cartella);
            using (Process.Start(new ProcessStartInfo("explorer.exe", "\"" + cartella + "\"") { UseShellExecute = true })) { }
        }
        catch (Exception e) { _esito.Text = Testo.FalloTu("non riesco ad aprire " + cartella + ". " + e.Message); }
    }
}

/// <summary>La finestra Modelli: la stessa tabella delle impostazioni, da sola.</summary>
public sealed class FinestraModelli : Window
{
    readonly PannelloModelli _pannello;

    internal int Schede => _pannello.Righe;

    public FinestraModelli(Ponte ponte)
    {
        Title = "Modelli";
        Width = 960;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        _pannello = new PannelloModelli(ponte) { Margin = new Thickness(14, 10, 14, 10) };
        var chiudi = Stile.Bottone("Chiudi");
        chiudi.Click += (_, _) => Close();
        var testata = Mainframe.Testata("modelli", chiudi);
        testata.MouseLeftButtonDown += (_, _) => { try { DragMove(); } catch { /* già rilasciato */ } };
        var tutto = new DockPanel();
        DockPanel.SetDock(testata, Dock.Top);
        tutto.Children.Add(testata);
        tutto.Children.Add(_pannello);
        Content = Mainframe.Cornice(tutto);
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
    }
}
