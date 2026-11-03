using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Liquidino.Nucleo;

namespace Liquidino;

/// <summary>Chat e, a fianco, il desktop delle creazioni: i file del suo mondo.</summary>
public sealed class FinestraCasa : Window
{
    readonly Ponte _ponte;
    readonly FinestraAvatar _avatar;
    readonly StackPanel _messaggi = new() { Margin = new Thickness(8) };
    readonly ScrollViewer _scorrimento;
    readonly TextBox _input;
    readonly TextBlock _stato;
    readonly TextBlock _aggiornamento;
    readonly ListBox _lista = new()
    {
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Foreground = Stile.Inchiostro,
        FontFamily = Stile.Testo,
        FontSize = 15
    };
    readonly Button _parla;
    bool _occupato;

    public FinestraCasa(Ponte ponte, FinestraAvatar avatar)
    {
        _ponte = ponte;
        _avatar = avatar;
        Title = "Liquidino";
        Width = 980;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = true;

        var barra = new DockPanel { Height = 48, Background = Stile.BarraP };
        var marca = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        marca.Children.Add(Stile.Titolo("DaP", chiaro: true));
        var nome = Stile.Titolo("  LIQUIDINO", chiaro: true);
        nome.FontSize = 16;
        marca.Children.Add(nome);
        DockPanel.SetDock(marca, Dock.Left);
        barra.Children.Add(marca);

        var destra = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        var novita = Stile.Bottone("novità ✦");
        novita.Click += (_, _) => new FinestraNovita().Show();
        _aggiornamento = new TextBlock
        {
            Text = "Aggiornamenti",
            FontFamily = Stile.Testo,
            FontSize = 15,
            Foreground = Stile.InchiostroBarraP,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0),
            Cursor = Cursors.Hand
        };
        _aggiornamento.MouseLeftButtonUp += async (_, _) => await ClicAggiornamento();
        Action<string> ascoltaAgg = versione => SulFilo(() => MostraAggiornamento(versione));
        Aggiornamenti.Trovata += ascoltaAgg;
        Closed += (_, _) => Aggiornamenti.Trovata -= ascoltaAgg;
        if (Aggiornamenti.Disponibile is { } gia) MostraAggiornamento(gia);
        var impostazioni = Stile.Bottone("Impostazioni");
        impostazioni.Click += (_, _) => new FinestraImpostazioni(_ponte).ShowDialog();
        var chiudi = Stile.Bottone("Chiudi");
        chiudi.Click += (_, _) => Hide();
        destra.Children.Add(novita);
        destra.Children.Add(_aggiornamento);
        destra.Children.Add(impostazioni);
        destra.Children.Add(chiudi);
        DockPanel.SetDock(destra, Dock.Right);
        barra.Children.Add(destra);

        var stati = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        foreach (var s in new[] { "dorme", "ascolta", "pensa", "parla" })
            stati.Children.Add(new TextBlock { Text = s, Margin = new Thickness(8, 0, 8, 0), FontFamily = Stile.Marchio, FontSize = 11, Foreground = Stile.Inchiostro3, Tag = s });
        barra.Children.Add(stati);
        barra.MouseLeftButtonDown += (_, _) => { try { DragMove(); } catch { /* già rilasciato */ } };

        _scorrimento = new ScrollViewer { Content = _messaggi, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Stile.Fondo };
        _input = Stile.Campo("Scrivi qui. Invio per mandare. Un clic su Parla, o Ctrl+Spazio.");
        _input.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { e.Handled = true; await Invia(); }
            else if (e.Key == Key.Escape) _ponte.Annulla();
        };
        var invia = Stile.Bottone("Invia", principale: true);
        invia.Click += async (_, _) => await Invia();
        _parla = Stile.Bottone("Parla");
        _parla.Click += (_, _) => _ponte.AlternaAscolto();
        AggiornaParla();
        var stop = Stile.Bottone("STOP", pericolo: true);
        stop.Click += (_, _) => { _ponte.Stop(); Aggiungi("Liquidino", "Mi sono fermato e ho spento i modelli.", false); AggiornaStato(); };

        var rigaInput = new DockPanel { Margin = new Thickness(8), LastChildFill = true };
        var tasti = new StackPanel { Orientation = Orientation.Horizontal };
        tasti.Children.Add(invia);
        tasti.Children.Add(_parla);
        tasti.Children.Add(stop);
        DockPanel.SetDock(tasti, Dock.Right);
        rigaInput.Children.Add(tasti);
        rigaInput.Children.Add(_input);

        var chat = new DockPanel { Background = Stile.Fondo };
        DockPanel.SetDock(rigaInput, Dock.Bottom);
        chat.Children.Add(rigaInput);
        chat.Children.Add(_scorrimento);

        var mondo = new DockPanel { Background = Stile.Carta2, Width = 320 };
        var testaMondo = new StackPanel { Margin = new Thickness(12, 10, 12, 6) };
        testaMondo.Children.Add(new TextBlock { Text = "IL MIO MONDO", FontFamily = Stile.Marchio, FontSize = 13, Foreground = Stile.OroP });
        testaMondo.Children.Add(Stile.Riga("Note, idee e memoria. Si possono cancellare.", secondario: true));
        DockPanel.SetDock(testaMondo, Dock.Top);
        var tastiMondo = new WrapPanel { Margin = new Thickness(8) };
        var aggiorna = Stile.Bottone("Aggiorna");
        aggiorna.Click += (_, _) => RiempiMondo();
        var apri = Stile.Bottone("Apri cartella");
        apri.Click += (_, _) => Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = "\"" + _ponte.Mondo.Radice + "\"", UseShellExecute = true });
        var cestino = Stile.Bottone("Nel cestino");
        cestino.Click += (_, _) => MettiNelCestino();
        var pensiero = Stile.Bottone("Un pensiero adesso");
        pensiero.Click += async (_, _) =>
        {
            Aggiungi("Liquidino", "Ci penso un attimo.", false);
            await _ponte.CicloOrario();
            RiempiMondo();
            AggiornaStato();
        };
        tastiMondo.Children.Add(aggiorna);
        tastiMondo.Children.Add(apri);
        tastiMondo.Children.Add(cestino);
        tastiMondo.Children.Add(pensiero);
        DockPanel.SetDock(tastiMondo, Dock.Bottom);
        mondo.Children.Add(testaMondo);
        mondo.Children.Add(tastiMondo);
        mondo.Children.Add(_lista);

        _stato = new TextBlock
        {
            FontFamily = Stile.Testo,
            FontSize = 14,
            Foreground = Stile.Inchiostro2,
            Margin = new Thickness(12, 4, 12, 6),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        DockPanel.SetDock(_stato, Dock.Bottom);

        var corpo = new Grid();
        corpo.ColumnDefinitions.Add(new ColumnDefinition());
        corpo.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        corpo.Children.Add(chat);
        Grid.SetColumn(mondo, 1);
        corpo.Children.Add(mondo);

        var tutto = new DockPanel();
        DockPanel.SetDock(barra, Dock.Top);
        DockPanel.SetDock(_stato, Dock.Bottom);
        tutto.Children.Add(barra);
        tutto.Children.Add(_stato);
        tutto.Children.Add(corpo);

        Content = new Border
        {
            Background = Stile.Fondo,
            BorderBrush = Stile.Pennello("#33ffffff"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Child = tutto
        };

        ponte.StatoCambiato += _ => SulFilo(AggiornaStato);
        ponte.MondoCambiato += () => SulFilo(RiempiMondo);
        ponte.AscoltoCambiato += () => SulFilo(AggiornaParla);
        ponte.Pensiero += testo => SulFilo(() => Aggiungi("Liquidino", testo, false));
        ponte.Risposto += esito => SulFilo(() =>
        {
            if (!string.IsNullOrWhiteSpace(esito.Trascrizione))
                Aggiungi("tu", esito.Trascrizione, true);
            if (!string.IsNullOrWhiteSpace(esito.Testo))
                Aggiungi("Liquidino", esito.Testo, false);
            if (esito.MostraMondo) RiempiMondo();
            AggiornaStato();
        });
        Loaded += (_, _) => SistemaPosizione();
        RiempiMondo();
        AggiornaStato();
        var orecchio = OrecchioLeggero.Stato();
        Aggiungi("Liquidino", "Ciao" + (string.IsNullOrWhiteSpace(ponte.Impostazioni.NomeUtente) ? "" : " " + ponte.Impostazioni.NomeUtente) + ". " + orecchio.Messaggio + " " + ponte.Impostazioni.FraseContesto, false);
    }

    public void Aggiungi(string chi, string testo, bool tuo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return;
        var b = new Border
        {
            Background = tuo ? Stile.Pennello("#2a2412") : Stile.Carta2,
            BorderBrush = tuo ? Stile.OroP : Stile.MagentaP,
            BorderThickness = new Thickness(tuo ? 0 : 2, 0, 0, 0),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(tuo ? 48 : 0, 4, tuo ? 0 : 48, 4),
            HorizontalAlignment = tuo ? HorizontalAlignment.Right : HorizontalAlignment.Left
        };
        var pila = new StackPanel();
        pila.Children.Add(new TextBlock { Text = chi, FontFamily = Stile.Marchio, FontSize = 10, Foreground = tuo ? Stile.OroP : Stile.MagentaP });
        pila.Children.Add(new TextBlock { Text = testo, FontFamily = Stile.Testo, FontSize = 17, Foreground = Stile.Inchiostro, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 });
        b.Child = pila;
        _messaggi.Children.Add(b);
        _scorrimento.ScrollToEnd();
    }

    public void AggiornaStato()
    {
        _stato.Text = _ponte.RigaStato();
        if (Content is Border { Child: DockPanel radice })
        {
            foreach (var dock in radice.Children.OfType<DockPanel>())
            {
                foreach (var pila in dock.Children.OfType<StackPanel>())
                {
                    foreach (var t in pila.Children.OfType<TextBlock>())
                    {
                        if (t.Tag is not string nome) continue;
                        var acceso = nome == Stati.Etichetta(_ponte.Stato);
                        t.Foreground = acceso ? Stile.OroP : Stile.Pennello("#8a8172");
                    }
                }
            }
        }
    }

    public void MostraCreazioni()
    {
        Show();
        Activate();
        _lista.Focus();
    }

    public void InviaVoce(byte[] wav) => _ = Gira(() => _ponte.DaAudio(wav));

    void SulFilo(Action azione)
    {
        try
        {
            if (Dispatcher.CheckAccess()) azione();
            else Dispatcher.BeginInvoke(azione);
        }
        catch { /* la finestra si sta chiudendo */ }
    }

    void AggiornaParla()
    {
        var acceso = _ponte.StaAscoltando;
        _parla.Content = acceso ? "Ti ascolto" : "Parla";
        _parla.BorderBrush = acceso ? Stile.VerdeP : Stile.Pennello("#3a3648");
        _parla.Foreground = acceso ? Stile.VerdeP : Stile.Inchiostro;
    }

    async Task Invia()
    {
        var testo = Stile.TestoCampo(_input);
        if (testo.Length == 0 || _occupato) return;
        _input.Text = "";
        _input.Foreground = Stile.Inchiostro;
        Aggiungi("tu", testo, true);
        await Gira(() => _ponte.Messaggio(testo));
    }

    async Task Gira(Func<Task<EsitoDialogo>> lavoro)
    {
        if (_occupato) return;
        _occupato = true;
        try
        {
            await lavoro();
        }
        catch (Exception e)
        {
            _ponte.Registro.SegnaErrore(e.Message);
            var detto = "Questo fallo tu: " + e.Message;
            Aggiungi("Liquidino", detto, false);
            _ponte.Annuncia(detto);
        }
        finally
        {
            _occupato = false;
            AggiornaStato();
        }
    }

    void RiempiMondo()
    {
        _lista.Items.Clear();
        foreach (var v in _ponte.Mondo.Elenca())
        {
            _lista.Items.Add(new ListBoxItem
            {
                Content = v.Cartella + "   " + v.Nome,
                Tag = v.Percorso,
                Foreground = Stile.Inchiostro,
                FontFamily = Stile.Testo,
                FontSize = 15,
                Padding = new Thickness(8, 3, 8, 3)
            });
        }
    }

    void MettiNelCestino()
    {
        if (_lista.SelectedItem is not ListBoxItem voce || voce.Tag is not string percorso) return;
        var dest = _ponte.Mondo.NelCestino(percorso);
        if (dest is null)
        {
            Aggiungi("Liquidino", "Questo fallo tu: quel file non sta nel mio mondo, non lo sposto.", false);
            return;
        }
        _ponte.Registro.Scrivi("cestino", Path.GetFileName(dest), "ok");
        RiempiMondo();
    }

    void MostraAggiornamento(string versione)
    {
        _aggiornamento.Text = "Aggiornamento " + versione + " disponibile";
        _aggiornamento.Foreground = Stile.OroP;
    }

    async Task ClicAggiornamento()
    {
        _aggiornamento.Text = "Controllo…";
        try
        {
            var detto = await Aggiornamenti.Installa();
            Aggiungi("Liquidino", detto, false);
            if (Aggiornamenti.Disponibile is { } v) MostraAggiornamento(v);
            else
            {
                _aggiornamento.Text = "Aggiornamenti";
                _aggiornamento.Foreground = Stile.InchiostroBarraP;
            }
        }
        catch (Exception e)
        {
            _aggiornamento.Text = "Aggiornamenti";
            _aggiornamento.Foreground = Stile.InchiostroBarraP;
            Aggiungi("Liquidino", "Questo fallo tu: non sono riuscito a aggiornare. " + e.Message, false);
        }
    }

    void SistemaPosizione()
    {
        var area = SystemParameters.WorkArea;
        var a = _avatar.Area;
        Left = a.Left - Width - 12;
        if (Left < area.Left) Left = Math.Min(a.Right + 12, area.Right - Width);
        Top = Math.Clamp(a.Top - 80, area.Top, Math.Max(area.Top, area.Bottom - Height));
        if (Left < area.Left) Left = area.Left + 12;
    }
}
