using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    readonly Button _parla;
    readonly Button _permesso;
    readonly Button _stop;
    readonly Button _dormi;
    readonly Button[] _tasti;
    readonly WrapPanel _servizi;
    readonly Border _chipAscolto;
    readonly Border _chipD1;
    readonly Border _chipVoce;
    readonly Border _chipMani;
    readonly Border _chipVl;
    readonly Border _chipPc;
    bool _sopra;
    bool _domandaAperta;
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

        _parla = Stile.Icona("🎤", "Parla: un clic per ascoltare, un altro per mandare.");
        _permesso = Stile.Icona("🖥️", "Permesso del PC: si segna, non clicca da solo.");
        _stop = Stile.Icona("⏹", "STOP: ferma il turno e scarica i modelli.", pericolo: true);
        var chat = Stile.Icona("💬", "Apri la chat.", principale: true);
        var creazioni = Stile.Icona("✨", "Apri le creazioni.");
        _dormi = Stile.Icona("🌙", "Dormi, o svegliati se sta già dormendo.");
        var impostazioni = Stile.Icona("⚙", "Impostazioni.");
        var nota = Stile.Icona("📝", "Nota veloce, senza aprire la chat.");
        var ore = Stile.Icona("🕐", "Che ore sono.");
        var pensiero = Stile.Icona("💭", "Un pensiero adesso, nella sua cartella.");
        _parla.Click += (_, _) => _ponte.AlternaAscolto();
        _permesso.Click += (_, _) => _ponte.AlternaPermesso();
        _stop.Click += (_, _) => _ponte.Stop();
        chat.Click += (_, _) => ApriCasa();
        creazioni.Click += (_, _) => ApriCasa().MostraCreazioni();
        _dormi.Click += (_, _) =>
        {
            if (_ponte.Stato == StatoCompagno.Dorme) _ponte.ImpostaStato(StatoCompagno.Sveglio);
            else _ponte.Stop();
        };
        impostazioni.Click += (_, _) => new FinestraImpostazioni(_ponte).ShowDialog();
        nota.Click += (_, _) => ChiediNota();
        ore.Click += async (_, _) => await _ponte.Messaggio("che ore sono");
        pensiero.Click += async (_, _) => await _ponte.PensieroAdesso();
        _tasti = [_parla, chat, creazioni, _permesso, _stop, _dormi, impostazioni, nota, ore, pensiero];

        var zona = new Grid { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
        zona.Children.Add(_figura);
        zona.Children.Add(_led);
        zona.MouseLeftButtonDown += InizioTrascinamento;

        _servizi = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        _chipAscolto = Chip("orecchio", Stile.Verde);
        _chipD1 = Chip("d1", Stile.Ciano);
        _chipVoce = Chip("voce", Stile.Magenta);
        _chipMani = Chip("mani", Stile.Oro);
        _chipVl = Chip("occhi", Stile.Ambra);
        _chipPc = Chip("pc", Stile.Rosso);
        foreach (var c in new[] { _chipAscolto, _chipD1, _chipVoce, _chipMani, _chipVl, _chipPc })
            _servizi.Children.Add(c);

        var comandi = new WrapPanel
        {
            Width = 280,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };
        foreach (var b in _tasti) comandi.Children.Add(b);
        foreach (var b in _tasti) b.Opacity = 0;
        AggiornaPermesso();
        AggiornaParla();

        var sotto = new StackPanel();
        sotto.Children.Add(_servizi);
        sotto.Children.Add(comandi);

        var radice = new Grid { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
        radice.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        radice.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(zona, 0);
        Grid.SetRow(sotto, 1);
        radice.Children.Add(zona);
        radice.Children.Add(sotto);
        Content = radice;
        MouseEnter += (_, _) => { _sopra = true; MostraTasti(); };
        MouseLeave += (_, _) => { _sopra = false; MostraTasti(); };

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

        ponte.StatoCambiato += s => SulFilo(() => { ApplicaStato(s); AggiornaDormi(); });
        ponte.PermessoCambiato += () => SulFilo(AggiornaPermesso);
        ponte.AscoltoCambiato += () => SulFilo(AggiornaParla);
        ponte.Pensiero += testo => SulFilo(() => MostraBolla(testo));
        ponte.Annuncio += testo => SulFilo(() => MostraBolla(testo));
        ponte.Risposto += esito => SulFilo(() => MostraEsito(esito));
        ponte.VocePronta += wav => SulFilo(async () => await _ponte.DaAudio(wav));

        Loaded += (_, _) => { _orologio.Start(); _trasforma.Start(); };
        Closing += (_, e) =>
        {
            if (System.Windows.Application.Current is App { InUscita: true }) return;
            e.Cancel = true;
            Nascondi();
        };
        Closed += (_, _) =>
        {
            _orologio.Stop();
            _trasforma.Stop();
            _bolla?.Close();
            if (_sorgente is not null) UnregisterHotKey(_sorgente.Handle, HotkeyId);
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
                    await _ponte.Guarda(f);
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
            ApriCasa();
            _ponte.AlternaAscolto();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Nascondi() => Hide();

    public void MostraDiNuovo()
    {
        Show();
        Activate();
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
        _ponte.Registro.Scrivi("chat", "finestra aperta", "ok");
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
        _ = Task.Run(() => _ponte.Impostazioni.Salva());
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
        AggiornaServizi();
        MostraTasti();
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
        _ = Task.Run(() => _ponte.Impostazioni.Salva());
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
        _ = Task.Run(() => _ponte.Impostazioni.Salva());
    }

    void ApplicaScala()
    {
        var lato = 150 * _ponte.Impostazioni.AvatarScala;
        Width = Math.Max(lato + 16, 300);
        Height = lato + 168;
    }

    void SulFilo(Action azione)
    {
        if (Dispatcher.CheckAccess()) azione();
        else Dispatcher.BeginInvoke(azione);
    }

    void AggiornaParla()
    {
        var acceso = _ponte.StaAscoltando;
        _parla.BorderBrush = acceso ? Stile.VerdeP : Stile.Pennello("#3a3648");
        _parla.Foreground = acceso ? Stile.VerdeP : Stile.Inchiostro;
        _parla.ToolTip = acceso ? "Ti sto ascoltando. Un clic e mando la frase." : "Parla: un clic per ascoltare, un altro per mandare.";
        AggiornaServizi();
        MostraTasti();
    }

    void AggiornaDormi()
    {
        var dorme = _ponte.Stato == StatoCompagno.Dorme;
        _dormi.Content = dorme ? "☀" : "🌙";
        _dormi.ToolTip = dorme ? "Sveglia." : "Dormi, o svegliati se sta già dormendo.";
        AggiornaServizi();
        MostraTasti();
    }

    void AggiornaPermesso()
    {
        var acceso = _ponte.Impostazioni.PermessoPc;
        _permesso.BorderBrush = acceso ? Stile.RossoP : Stile.Pennello("#3a3648");
        _permesso.Foreground = acceso ? Stile.RossoP : Stile.Inchiostro;
        _permesso.ToolTip = acceso ? "Permesso del PC acceso. Per ora non clicca da solo." : "Permesso del PC: si segna, non clicca da solo.";
        AggiornaServizi();
    }

    void AggiornaServizi()
    {
        var accesi = _ponte.Regista.Accesi;
        Accendi(_chipAscolto, _ponte.StaAscoltando || _ponte.Stato == StatoCompagno.Ascolta);
        Accendi(_chipD1, accesi.Contains(RuoloModello.D1));
        Accendi(_chipVoce, accesi.Contains(RuoloModello.Omni) || _ponte.Stato == StatoCompagno.Parla);
        Accendi(_chipMani, accesi.Contains(RuoloModello.Mani));
        Accendi(_chipVl, accesi.Contains(RuoloModello.Vl));
        Accendi(_chipPc, _ponte.Impostazioni.PermessoPc);
    }

    static void Accendi(Border chip, bool acceso)
    {
        chip.Opacity = acceso ? 1 : 0.28;
        chip.Background = acceso ? Stile.Pennello("#221f2a") : Brushes.Transparent;
    }

    void MostraTasti()
    {
        var lavorando = _ponte.StaLavorando;
        foreach (var b in _tasti)
        {
            var resta = (b == _stop && lavorando) || _domandaAperta;
            var verso = _sopra || resta ? 1.0 : 0.0;
            b.BeginAnimation(OpacityProperty, new DoubleAnimation(verso, TimeSpan.FromMilliseconds(180)));
            b.IsHitTestVisible = verso > 0.4 || _sopra;
        }
    }

    void MostraEsito(EsitoDialogo esito)
    {
        if (string.IsNullOrWhiteSpace(esito.Testo)) return;
        if (esito.ChiestaConferma)
        {
            _domandaAperta = true;
            MostraTasti();
            MostraBolla(esito.Testo, new[]
            {
                new SceltaBolla("Sì", () => { _domandaAperta = false; _ = _ponte.Messaggio("sì"); }),
                new SceltaBolla("No", () => { _domandaAperta = false; _ = _ponte.Messaggio("no"); })
            }, resta = true);
            return;
        }
        _domandaAperta = false;
        MostraBolla(esito.Testo);
        MostraTasti();
    }

    void ChiediNota()
    {
        _domandaAperta = true;
        MostraTasti();
        MostraBolla("Cosa segno?", new[]
        {
            new SceltaBolla("Segna", testo =>
            {
                _domandaAperta = false;
                var detto = (testo ?? "").Trim();
                if (detto.Length == 0) return;
                _ = _ponte.Messaggio("prendi nota: " + detto);
            }, campo: true),
            new SceltaBolla("Lascia", _ => _domandaAperta = false)
        }, resta = true);
    }

    void MostraBolla(string testo, IReadOnlyList<SceltaBolla>? scelte = null, bool resta = false)
    {
        _bolla?.Close();
        _bolla = new FinestraBolla(testo, this, scelte, resta);
        _bolla.Closed += (_, _) =>
        {
            if (_bolla is not null && !_bolla.IsVisible) _bolla = null;
            if (scelte is not null) { _domandaAperta = false; MostraTasti(); }
        };
        _bolla.Show();
    }

    static Border Chip(string nome, string colore)
    {
        var pennello = Stile.Pennello(colore);
        return new Border
        {
            Child = new TextBlock { Text = nome, FontFamily = Stile.Testo, FontSize = 12, Foreground = pennello },
            BorderBrush = pennello,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 0, 6, 0),
            Margin = new Thickness(2, 0, 2, 0),
            Opacity = 0.28,
            ToolTip = nome switch
            {
                "orecchio" => "Ascolto, microfono acceso.",
                "d1" => "Decisione di d1, il modello che sceglie l'azione.",
                "voce" => "Voce di Omni.",
                "mani" => "Mani, LFM3-1.7B, per scrivere e compilare.",
                "occhi" => "VL, per guardare le immagini.",
                _ => "Permesso del PC."
            }
        };
    }

    void Menu()
    {
        var m = new ContextMenu { PlacementTarget = this, FontFamily = Stile.Testo };
        Voce(IsVisible ? "Nascondi l'avatar" : "Mostra l'avatar", () => { if (IsVisible) Nascondi(); else MostraDiNuovo(); });
        Voce("Aggiornamenti: " + Aggiornamenti.Stato, async () =>
        {
            try
            {
                if (Aggiornamenti.Disponibile is null)
                    await Aggiornamenti.Controlla(scarica: true, CancellationToken.None);
                var detto = Aggiornamenti.Disponibile is null ? Aggiornamenti.Stato : await Aggiornamenti.Installa();
                MostraBolla(detto);
            }
            catch (Exception e) { MostraBolla("Questo fallo tu: non sono riuscito a aggiornare. " + e.Message); }
        });
        m.Items.Add(new Separator());
        Voce("Parla", () => _ponte.AlternaAscolto());
        Voce("Chat", () => ApriCasa());
        Voce("Creazioni", () => ApriCasa().MostraCreazioni());
        Voce(_ponte.Impostazioni.PermessoPc ? "Permesso sì" : "Permesso", () => _ponte.AlternaPermesso());
        Voce("STOP", () => _ponte.Stop());
        Voce(_ponte.Stato == StatoCompagno.Dorme ? "Sveglia" : "Dormi", () =>
        {
            if (_ponte.Stato == StatoCompagno.Dorme) _ponte.ImpostaStato(StatoCompagno.Sveglio);
            else _ponte.Stop();
        });
        Voce("Impostazioni", () => new FinestraImpostazioni(_ponte).ShowDialog());
        Voce("Trasformati", Trasforma);
        m.Items.Add(new Separator());
        Voce("Esci", () => { if (System.Windows.Application.Current is App app) app.Esci(); });
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

public sealed record SceltaBolla(string Etichetta, Action<string?> Azione, bool Campo = false);

/// <summary>Fumetto sull'avatar. Si chiude da solo, con un clic, o con un pulsante.</summary>
public sealed class FinestraBolla : Window
{
    readonly DispatcherTimer? _chiusura;

    public FinestraBolla(string testo, FinestraAvatar avatar, IReadOnlyList<SceltaBolla>? scelte = null, bool resta = false)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = scelte?.Any(s => s.Campo) == true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;

        var pila = new StackPanel();
        var corpo = Stile.Riga(testo.Length > 280 ? testo[..280] + "…" : testo);
        corpo.MaxWidth = 280;
        pila.Children.Add(corpo);

        TextBox? campo = null;
        if (scelte?.Any(s => s.Campo) == true)
        {
            campo = Stile.Campo("scrivi qui");
            campo.Margin = new Thickness(0, 8, 0, 0);
            campo.MaxWidth = 280;
            pila.Children.Add(campo);
        }
        if (scelte is { Count: > 0 })
        {
            var riga = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (var scelta in scelte)
            {
                var bottone = Stile.Icona(scelta.Etichetta, scelta.Etichetta, principale: scelta.Etichetta is "Sì" or "Segna");
                bottone.FontFamily = Stile.Testo;
                bottone.FontSize = 15;
                var presa = scelta;
                bottone.Click += (_, _) =>
                {
                    Close();
                    presa.Azione(campo is null ? null : Stile.TestoCampo(campo));
                };
                riga.Children.Add(bottone);
            }
            pila.Children.Add(riga);
        }

        var bordo = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(230, 20, 16, 28)),
            BorderBrush = Stile.OroP,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(12),
            Child = pila
        };
        Content = bordo;
        if (scelte is null)
            MouseLeftButtonUp += (_, _) => Close();
        Loaded += (_, _) =>
        {
            var a = avatar.Area;
            var area = SystemParameters.WorkArea;
            Left = Math.Clamp(a.Left + a.Width / 2 - 150, area.Left, Math.Max(area.Left, area.Right - 320));
            Top = Math.Max(area.Top, a.Top - ActualHeight - 8);
        };
        if (!resta)
        {
            _chiusura = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _chiusura.Tick += (_, _) => Close();
            MouseEnter += (_, _) => _chiusura.Stop();
            MouseLeave += (_, _) => _chiusura.Start();
            Loaded += (_, _) => _chiusura.Start();
            Closed += (_, _) => _chiusura.Stop();
        }
    }
}
