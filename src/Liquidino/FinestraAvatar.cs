using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Ellipse = System.Windows.Shapes.Ellipse;
using Liquidino.Nucleo;

namespace Liquidino;

/// <summary>
/// L'avatar sul desktop: finestra trasparente, sempre davanti, trascinabile.
/// Gli stati sono dorme, ascolta, pensa, parla. Fra uno e l'altro resta sveglio.
/// Clic: chat e creazioni. Vicino: permesso del PC e STOP.
/// </summary>
public sealed class FinestraAvatar : Window
{
    const int HotkeyId = 1;
    const int WmHotkey = 0x0312;

    readonly Ponte _ponte;
    readonly Image _figura = new() { Stretch = Stretch.Uniform, IsHitTestVisible = false };
    readonly Ellipse _led = new() { Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 10, 8), IsHitTestVisible = false };
    readonly Button _permesso;
    readonly Button _stop;
    readonly DispatcherTimer _orologio;
    readonly DispatcherTimer _trasforma;
    readonly Random _caso = new();
    FoglioAvatar? _foglio;
    string _animazione = "idle";
    int _frame;
    bool _trascinato;
    Point _inizio;
    FinestraBolla? _bolla;
    HwndSource? _sorgente;

    public FinestraAvatar(Ponte ponte)
    {
        _ponte = ponte;
        Title = "Liquidino";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        AllowDrop = true;

        _permesso = Stile.Bottone("Permesso", ponte.Impostazioni.PermessoPc);
        _stop = Stile.Bottone("STOP", pericolo: true);
        _permesso.Click += (_, _) => _ponte.AlternaPermesso();
        _stop.Click += (_, _) => _ponte.Stop();
        AggiornaPermesso();

        var zona = new Grid { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
        zona.Children.Add(_figura);
        zona.Children.Add(_led);
        zona.MouseLeftButtonDown += InizioTrascinamento;

        var comandi = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };
        comandi.Children.Add(_permesso);
        comandi.Children.Add(_stop);

        var radice = new Grid();
        radice.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        radice.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(zona, 0);
        Grid.SetRow(comandi, 1);
        radice.Children.Add(zona);
        radice.Children.Add(comandi);
        Content = radice;

        ApplicaScala();
        var area = SystemParameters.WorkArea;
        var imp = ponte.Impostazioni;
        Left = imp.AvatarX is { } x && x > area.Left - Width && x < area.Right ? x : area.Right - Width - 28;
        Top = imp.AvatarY is { } y && y > area.Top - Height && y < area.Bottom ? y : area.Bottom - Height - 28;

        var oggetti = FoglioAvatar.Disponibili();
        var nome = oggetti.Contains(imp.AvatarOggetto) ? imp.AvatarOggetto : oggetti.FirstOrDefault() ?? "cassetta";
        _foglio = oggetti.Count == 0 ? null : FoglioAvatar.Carica(nome);
        Suona(AnimazioneDi(ponte.Stato));

        _orologio = new DispatcherTimer { Interval = Intervallo() };
        _orologio.Tick += (_, _) => Avanti();
        _trasforma = new DispatcherTimer { Interval = TimeSpan.FromMinutes(4 + _caso.NextDouble() * 4) };
        _trasforma.Tick += (_, _) =>
        {
            if (imp.AvatarCambioAuto && _ponte.Stato is StatoCompagno.Dorme or StatoCompagno.Sveglio) Trasforma();
            _trasforma.Interval = TimeSpan.FromMinutes(4 + _caso.NextDouble() * 4);
        };

        ponte.StatoCambiato += s => Dispatcher.Invoke(() => ApplicaStato(s));
        ponte.PermessoCambiato += () => Dispatcher.Invoke(AggiornaPermesso);
        ponte.Pensiero += testo => Dispatcher.Invoke(() => MostraBolla(testo));

        Loaded += (_, _) => { _orologio.Start(); _trasforma.Start(); };
        Closed += (_, _) =>
        {
            _orologio.Stop();
            _trasforma.Stop();
            _bolla?.Close();
            if (_sorgente is not null) UnregisterHotKey(_sorgente.Handle, HotkeyId);
            System.Windows.Application.Current.Shutdown();
        };
        MouseRightButtonUp += (_, e) => { Menu(); e.Handled = true; };
        MouseWheel += (_, e) => { CambiaScala(_ponte.Impostazioni.AvatarScala + (e.Delta > 0 ? 0.1 : -0.1)); e.Handled = true; };
        DragEnter += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; _ponte.ImpostaStato(StatoCompagno.Ascolta); };
        DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
        DragLeave += (_, _) => { if (_ponte.Stato == StatoCompagno.Ascolta) _ponte.ImpostaStato(StatoCompagno.Sveglio); };
        Drop += async (_, e) =>
        {
            try
            {
                if (e.Data.GetData(DataFormats.FileDrop) is not string[] file || file.Length == 0) return;
                var casa = ApriCasa();
                foreach (var f in file.Take(3))
                {
                    if (!File.Exists(f)) continue;
                    casa.Aggiungi("tu", "Ti ho lasciato «" + Path.GetFileName(f) + "».", true);
                    var esito = await _ponte.Guarda(f);
                    casa.Aggiungi("Liquidino", esito.Testo, false);
                    casa.AggiornaStato();
                }
            }
            catch (Exception ex)
            {
                ApriCasa().Aggiungi("Liquidino", "Questo fallo tu: non sono riuscito a guardare il file. " + ex.Message, false);
            }
        };
        ApplicaStato(ponte.Stato);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _sorgente = (HwndSource?)PresentationSource.FromVisual(this);
        if (_sorgente is null) return;
        _sorgente.AddHook(MessaggioFinestra);
        RegisterHotKey(_sorgente.Handle, HotkeyId, 0x0002, 0x20);
    }

    IntPtr MessaggioFinestra(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            var casa = ApriCasa();
            casa.AlternaParola();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public FinestraCasa ApriCasa()
    {
        if (_ponte.Stato == StatoCompagno.Dorme) _ponte.ImpostaStato(StatoCompagno.Sveglio);
        if (_ponte.Casa is { IsLoaded: true } gia)
        {
            gia.Show();
            gia.Activate();
            return gia;
        }
        var casa = new FinestraCasa(_ponte, this);
        _ponte.Casa = casa;
        casa.Closed += (_, _) => { if (_ponte.Casa == casa) _ponte.Casa = null; };
        casa.Show();
        return casa;
    }

    void InizioTrascinamento(object sender, MouseButtonEventArgs e)
    {
        _inizio = PointToScreen(e.GetPosition(this));
        _trascinato = false;
        MouseEventHandler? muovi = null;
        muovi = (_, _) =>
        {
            var ora = PointToScreen(Mouse.GetPosition(this));
            if (Math.Abs(ora.X - _inizio.X) + Math.Abs(ora.Y - _inizio.Y) > 8) _trascinato = true;
        };
        MouseMove += muovi;
        try { DragMove(); } catch { /* il mouse è già stato rilasciato */ }
        MouseMove -= muovi;
        _ponte.Impostazioni.AvatarX = Left;
        _ponte.Impostazioni.AvatarY = Top;
        _ponte.Impostazioni.Salva();
        if (!_trascinato && e.ClickCount < 3) ApriCasa();
    }

    void ApplicaStato(StatoCompagno stato)
    {
        _figura.Opacity = stato == StatoCompagno.Dorme ? 0.62 : 1;
        _led.Fill = stato switch
        {
            StatoCompagno.Dorme => Stile.Pennello("#3a3644"),
            StatoCompagno.Ascolta => Stile.VerdeP,
            StatoCompagno.Pensa => Stile.CianoP,
            StatoCompagno.Parla => Stile.MagentaP,
            _ => Stile.AmbraP
        };
        _orologio.Interval = Intervallo();
        Suona(AnimazioneDi(stato));
    }

    static string AnimazioneDi(StatoCompagno stato) => stato switch
    {
        StatoCompagno.Ascolta => "attento",
        StatoCompagno.Pensa => "gira",
        StatoCompagno.Parla => "saluta",
        _ => "idle"
    };

    TimeSpan Intervallo()
    {
        var fps = _foglio?.Fps ?? 24;
        if (_ponte.Stato == StatoCompagno.Dorme) fps = 8;
        return TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, fps));
    }

    void Suona(string nome)
    {
        if (_foglio is null || !_foglio.Ha(nome)) nome = "idle";
        _animazione = nome;
        _frame = 0;
        _figura.Source = _foglio?.Fotogramma(nome, 0);
    }

    void Avanti()
    {
        if (_foglio is null || !_foglio.Ha(_animazione)) return;
        var n = _foglio.Quanti(_animazione);
        if (n <= 1) return;
        _frame = (_frame + 1) % n;
        _figura.Source = _foglio.Fotogramma(_animazione, _frame);
    }

    void Trasforma()
    {
        var altri = FoglioAvatar.Disponibili().Where(n => n != _foglio?.Nome).ToList();
        if (altri.Count == 0) return;
        var nuovo = FoglioAvatar.Carica(altri[_caso.Next(altri.Count)]);
        if (nuovo is null) return;
        _foglio = nuovo;
        _ponte.Impostazioni.AvatarOggetto = nuovo.Nome;
        _ponte.Impostazioni.Salva();
        Suona(AnimazioneDi(_ponte.Stato));
    }

    void CambiaScala(double nuova)
    {
        nuova = Math.Round(Math.Clamp(nuova, 0.7, 2.0), 2);
        _ponte.Impostazioni.AvatarScala = nuova;
        var cx = Left + Width / 2;
        var cy = Top + Height / 2;
        ApplicaScala();
        Left = cx - Width / 2;
        Top = cy - Height / 2;
        _ponte.Impostazioni.AvatarX = Left;
        _ponte.Impostazioni.AvatarY = Top;
        _ponte.Impostazioni.Salva();
    }

    void ApplicaScala()
    {
        var lato = 150 * _ponte.Impostazioni.AvatarScala;
        Width = lato + 8;
        Height = lato + 46;
    }

    void AggiornaPermesso()
    {
        var acceso = _ponte.Impostazioni.PermessoPc;
        _permesso.Content = acceso ? "Permesso sì" : "Permesso";
        _permesso.BorderBrush = acceso ? Stile.VerdeP : Stile.Pennello("#3a3648");
        _permesso.Foreground = acceso ? Stile.VerdeP : Stile.Inchiostro;
    }

    void MostraBolla(string testo)
    {
        _bolla?.Close();
        _bolla = new FinestraBolla(testo, this);
        _bolla.Closed += (_, _) => { if (_bolla is not null && !_bolla.IsVisible) _bolla = null; };
        _bolla.Show();
    }

    void Menu()
    {
        var m = new ContextMenu { PlacementTarget = this, FontFamily = Stile.Testo };
        Voce("Apri la chat", () => ApriCasa());
        Voce("Dormi", () => _ponte.Stop());
        Voce("Sveglia", () => { if (_ponte.Stato == StatoCompagno.Dorme) _ponte.ImpostaStato(StatoCompagno.Sveglio); });
        Voce("Trasformati", Trasforma);
        Voce("Impostazioni", () => new FinestraImpostazioni(_ponte).ShowDialog());
        m.IsOpen = true;

        void Voce(string testo, Action fai)
        {
            var v = new MenuItem { Header = testo, FontFamily = Stile.Testo, FontSize = 15 };
            v.Click += (_, _) => fai();
            m.Items.Add(v);
        }
    }

    public Rect Area => new(Left, Top, Width, Height);

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

/// <summary>Fumetto di compagnia, vetro scuro, si chiude da solo.</summary>
public sealed class FinestraBolla : Window
{
    readonly DispatcherTimer _chiusura;

    public FinestraBolla(string testo, FinestraAvatar avatar)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;

        var corpo = Stile.Riga(testo.Length > 220 ? testo[..220] + "…" : testo);
        corpo.MaxWidth = 280;
        var bordo = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(230, 20, 16, 28)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(12),
            Child = corpo
        };
        Content = bordo;
        MouseLeftButtonUp += (_, _) => Close();
        Loaded += (_, _) =>
        {
            var a = avatar.Area;
            var area = SystemParameters.WorkArea;
            Left = Math.Clamp(a.Left + a.Width / 2 - 140, area.Left, Math.Max(area.Left, area.Right - 280));
            Top = Math.Max(area.Top, a.Top - 90);
        };
        _chiusura = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _chiusura.Tick += (_, _) => Close();
        MouseEnter += (_, _) => _chiusura.Stop();
        MouseLeave += (_, _) => _chiusura.Start();
        Loaded += (_, _) => _chiusura.Start();
        Closed += (_, _) => _chiusura.Stop();
    }
}
