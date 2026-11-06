using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ellipse = System.Windows.Shapes.Ellipse;
using Liquidino.Nucleo;

namespace Liquidino;

/// <summary>
/// L'avatar sul desktop: finestra trasparente, sempre davanti, trascinabile.
/// Gli stati sono dorme, ascolta, pensa, parla. Fra uno e l'altro resta sveglio.
/// Sotto: le luci dei modelli (accesi, spenti, da scaricare) e i tasti, ognuno col suo nome.
/// In pausa l'avatar è in bianco e nero e niente si carica da solo.
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
    readonly Button _orecchie;
    readonly Button _pausa;
    readonly Button _stop;
    readonly Button _dormi;
    readonly Button[] _tasti;
    readonly WrapPanel _luci;
    string? _scaricando;
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

        _parla = Tasto("🎤", "Parla", "Parla una volta: un clic e ti ascolto, un altro clic e mando la frase. Anche Ctrl+Spazio.");
        _orecchie = Tasto("👂", "Orecchie", TestoOrecchie(false));
        _permesso = Tasto("🖥️", "Mani", TestoPermesso(false));
        _pausa = Tasto("⏸", "Pausa", TestoPausa(false));
        _stop = Tasto("⏹", "STOP", "STOP: ferma tutto, spegne le Orecchie e scarica i modelli.", pericolo: true);
        var chat = Tasto("💬", "Chat", "Apri la chat.", principale: true);
        var mondo = Tasto("🗂", "Mondo", "Apri il tuo mondo: note, pensieri, sogni, diario e memoria.");
        _dormi = Tasto("🌙", "Dormi", "");
        var impostazioni = Tasto("⚙", "Impost.", "Impostazioni: tutto quello che si può cambiare, e i modelli.");
        _parla.Click += (_, _) => _ponte.AlternaAscolto();
        _orecchie.Click += async (_, _) => await _ponte.AlternaOrecchie();
        _permesso.Click += (_, _) => _ponte.AlternaPermesso();
        _pausa.Click += async (_, _) => await _ponte.AlternaPausa();
        _stop.Click += (_, _) => _ponte.Stop();
        chat.Click += (_, _) => ApriCasa().ModoChat();
        mondo.Click += (_, _) => ApriCasa().ModoMondo();
        _dormi.Click += (_, _) => _ponte.AlternaSonno();
        _dormi.ToolTipOpening += (_, _) => _dormi.ToolTip = TestoDormi();
        impostazioni.Click += (_, _) => new FinestraImpostazioni(_ponte).ShowDialog();
        _tasti = [chat, mondo, _parla, _orecchie, _dormi, _permesso, _pausa, impostazioni, _stop];

        var zona = new Grid { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
        zona.Children.Add(_figura);
        zona.Children.Add(_led);
        zona.MouseLeftButtonDown += InizioTrascinamento;

        _luci = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 2) };

        var comandi = new WrapPanel
        {
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };
        foreach (var b in _tasti) comandi.Children.Add(b);
        foreach (var b in _tasti) b.Opacity = 0;
        AggiornaPermesso();
        AggiornaParla();
        AggiornaOrecchie();
        AggiornaDormi();
        AggiornaLuci();

        var sotto = new StackPanel();
        sotto.Children.Add(_luci);
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
        ponte.OrecchieCambiate += () => SulFilo(AggiornaOrecchie);
        ponte.AscoltoCambiato += () => SulFilo(AggiornaParla);
        ponte.Pensiero += testo => SulFilo(() => MostraVotabile(testo, "pensiero"));
        ponte.Sognato += sogno => SulFilo(() => MostraVotabile("💤 " + sogno, "sogno"));
        ponte.ModelliCambiati += () => SulFilo(() => { AggiornaLuci(); AggiornaPausa(); });
        ponte.SonnoCambiato += () => SulFilo(AggiornaDormi);
        ponte.ImpostazioniSalvate += () => SulFilo(() =>
        {
            if (_foglio?.Nome != _ponte.Impostazioni.AvatarOggetto && FoglioAvatar.Carica(_ponte.Impostazioni.AvatarOggetto) is { } nuovo)
            {
                _foglio = nuovo;
                Suona(AnimazioneDi(_ponte.Stato));
            }
            CambiaScala(_ponte.Impostazioni.AvatarScala);
            AggiornaOrecchie();
            AggiornaLuci();
        });
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
        if (!_trascinato && e.ClickCount < 3) ApriCasa().ModoChat();
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
        _figura.Source = Colore(_foglio?.Fotogramma(nome, 0));
    }

    /// <summary>In pausa l'avatar è in bianco e nero: si vede subito che non carica niente da solo.</summary>
    ImageSource? Colore(ImageSource? fotogramma)
    {
        if (fotogramma is not BitmapSource bitmap || !_ponte.Regista.InPausa) return fotogramma;
        if (_grigi.TryGetValue(bitmap, out var gia)) return gia;
        var grigio = Grigio(bitmap);
        _grigi.Add(bitmap, grigio);
        return grigio;
    }

    readonly System.Runtime.CompilerServices.ConditionalWeakTable<BitmapSource, BitmapSource> _grigi = new();

    static BitmapSource Grigio(BitmapSource sorgente)
    {
        var bgra = new FormatConvertedBitmap(sorgente, PixelFormats.Bgra32, null, 0);
        var larghezza = bgra.PixelWidth;
        var altezza = bgra.PixelHeight;
        var passo = larghezza * 4;
        var pixel = new byte[passo * altezza];
        bgra.CopyPixels(pixel, passo, 0);
        for (var i = 0; i < pixel.Length; i += 4)
        {
            var l = (byte)Math.Round(pixel[i] * 0.114 + pixel[i + 1] * 0.587 + pixel[i + 2] * 0.299);
            pixel[i] = pixel[i + 1] = pixel[i + 2] = l;
        }
        var uscita = BitmapSource.Create(larghezza, altezza, sorgente.DpiX, sorgente.DpiY, PixelFormats.Bgra32, null, pixel, passo);
        uscita.Freeze();
        return uscita;
    }

    void Avanti()
    {
        if (_foglio is null || !_foglio.Ha(_animazione)) return;
        var n = _foglio.Quanti(_animazione);
        if (n <= 1) return;
        _frame = (_frame + 1) % n;
        _figura.Source = Colore(_foglio.Fotogramma(_animazione, _frame));
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
        var dorme = _ponte.Dormita is not null;
        ImpostaTasto(_dormi, dorme ? "☀" : "🌙", dorme ? "Sveglia" : "Dormi");
        _dormi.ToolTip = TestoDormi();
        AggiornaServizi();
        MostraTasti();
    }

    string TestoDormi()
    {
        var d = _ponte.Dormita;
        var complete = "Dormite complete: " + _ponte.Impostazioni.DormiteComplete + ".";
        if (d is null)
            return "Mettilo a dormire: sceglie lui quanto (al massimo " + Sonno.Leggibile(TimeSpan.FromMinutes(_ponte.Impostazioni.SonnoMassimoMinuti)) + ") e intanto sogna. " + complete;
        return "Dorme ancora " + Sonno.Leggibile(d.Manca(DateTimeOffset.Now)) + " (sveglia alle " + d.Sveglia.ToString("HH:mm") + "), " + d.Sogni.Count + " sogni finora. Un clic per svegliarlo: non conta come dormita completa. " + complete;
    }

    void AggiornaPausa()
    {
        var pausa = _ponte.Regista.InPausa;
        _pausa.BorderBrush = pausa ? Stile.Pennello("#e7e0d2") : Stile.Pennello("#3a3648");
        _pausa.Foreground = pausa ? Stile.Pennello("#e7e0d2") : Stile.Inchiostro;
        ImpostaTasto(_pausa, pausa ? "▶" : "⏸", pausa ? "Riprendi" : "Pausa");
        _pausa.ToolTip = TestoPausa(pausa);
        Suona(_animazione);
    }

    static string TestoPausa(bool pausa) => pausa
        ? "In pausa: niente si carica da solo e l'avatar è in bianco e nero. Accendi un modello dalla sua luce, o un clic qui per riprendere."
        : "Pausa: scarica i modelli e non ne carica più da solo, così non ti riempie la memoria all'improvviso. I modelli li accendi tu dalle luci.";

    static Button Tasto(string simbolo, string etichetta, string tooltip, bool principale = false, bool pericolo = false)
    {
        var b = Stile.Icona(simbolo, tooltip, principale, pericolo);
        b.Width = 56;
        b.Padding = new Thickness(2, 3, 2, 2);
        ImpostaTasto(b, simbolo, etichetta);
        return b;
    }

    static void ImpostaTasto(Button b, string simbolo, string etichetta)
    {
        var pila = new StackPanel();
        pila.Children.Add(new TextBlock { Text = simbolo, FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Segoe UI"), FontSize = 17, HorizontalAlignment = HorizontalAlignment.Center });
        pila.Children.Add(new TextBlock { Text = etichetta, FontFamily = Stile.Testo, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -1, 0, 0) });
        b.Content = pila;
    }

    // ---------- le luci dei modelli ----------

    static string ColoreDi(RuoloModello r) => r switch
    {
        RuoloModello.Mani => Stile.Oro,
        RuoloModello.Omni => Stile.Magenta,
        RuoloModello.D1 => Stile.Ciano,
        RuoloModello.Vl => Stile.Ambra,
        RuoloModello.Audio => Stile.Verde,
        _ => "#b48cff"
    };

    void AggiornaLuci()
    {
        _luci.Children.Clear();
        var accesi = _ponte.Regista.Accesi;
        foreach (var scheda in CatalogoModelli.Tutti)
        {
            var presente = _ponte.Regista.Presente(scheda.Ruolo);
            var acceso = accesi.Contains(scheda.Ruolo);
            var stato = acceso ? "acceso, in memoria" : presente ? "installato, spento" : "non installato";
            var azione = acceso ? "Clic: spegnilo e libera la memoria." : presente ? "Clic: accendilo adesso." : "Clic: scaricalo (ti chiedo prima il sì).";
            var schedaPresa = scheda;
            _luci.Children.Add(Luce(ColoreDi(scheda.Ruolo), acceso, presente,
                scheda.Nome + " · " + NomiModello.Etichetta(scheda.Ruolo) + "\n" + scheda.APerCosa + "\n\nStato: " + stato + ".\n" + azione,
                async () =>
                {
                    if (presente) MostraBolla(await _ponte.AlternaModello(schedaPresa.Ruolo));
                    else await ScaricaDaLuce(schedaPresa, null);
                }));
        }
        var parakeet = CatalogoOrecchio.Parakeet;
        var cePa = _ponte.Udito.ParakeetPresente;
        var orecchie = _ponte.Udito.Accese;
        _luci.Children.Add(Luce("#e7e0d2", orecchie, cePa,
            parakeet.Nome + " · orecchie\n" + parakeet.APerCosa + "\n\nStato: " + (orecchie ? "acceso (Orecchie accese)" : cePa ? "installato, spento" : "non installato") + ".\n"
            + (cePa ? "Clic: " + (orecchie ? "spegni" : "accendi") + " le Orecchie." : "Clic: scaricalo (ti chiedo prima il sì)."),
            async () =>
            {
                if (cePa) await _ponte.AlternaOrecchie();
                else await ScaricaDaLuce(null, parakeet);
            }));
    }

    FrameworkElement Luce(string colore, bool accesa, bool presente, string spiegazione, Func<Task> clic)
    {
        var pennello = Stile.Pennello(colore);
        var punto = new Ellipse
        {
            Width = 13,
            Height = 13,
            Fill = accesa ? pennello : presente ? new SolidColorBrush(Color.FromArgb(60, pennello.Color.R, pennello.Color.G, pennello.Color.B)) : Brushes.Transparent,
            Stroke = presente ? pennello : Stile.Pennello("#6f6b7d"),
            StrokeThickness = 1.5,
            StrokeDashArray = presente ? null : new DoubleCollection { 2, 2 },
            Effect = accesa ? new DropShadowEffect { Color = pennello.Color, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.95 } : null
        };
        var zona = new Border
        {
            Child = punto,
            Padding = new Thickness(5),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            ToolTip = new ToolTip { Content = new TextBlock { Text = spiegazione, MaxWidth = 320, TextWrapping = TextWrapping.Wrap, FontFamily = Stile.Testo, FontSize = 14 } }
        };
        ToolTipService.SetInitialShowDelay(zona, 150);
        ToolTipService.SetShowDuration(zona, 20000);
        zona.MouseLeftButtonUp += async (_, e) =>
        {
            e.Handled = true;
            try { await clic(); }
            catch (Exception ex) { MostraBolla(Testo.FalloTu(ex.Message)); }
        };
        return zona;
    }

    async Task ScaricaDaLuce(SchedaCatalogo? lfm, SchedaFissa? fissa)
    {
        if (Scaricamenti.InCorso is { } gia)
        {
            MostraBolla("Sto scaricando " + gia + ". Vuoi fermarlo?", new[]
            {
                new SceltaBolla("Ferma", _ => Scaricamenti.Annulla()),
                new SceltaBolla("Lascia", _ => { })
            });
            return;
        }
        var fine = await Scaricamenti.Scarica(this, _ponte, lfm, fissa, testo => SulFilo(() =>
        {
            _scaricando = testo;
            if (_luci.Children.Count > 0) _luci.ToolTip = testo;
        }));
        _scaricando = null;
        _luci.ToolTip = null;
        _ponte.Regista.Rileggi();
        AggiornaLuci();
        MostraBolla(fine);
    }

    /// <summary>Un pensiero o un sogno, con 👍 e 👎. Lui non risponde: il voto va nella palestra.</summary>
    void MostraVotabile(string testo, string fonte)
    {
        if (!_ponte.Impostazioni.MostraVoti || _domandaAperta) { MostraBolla(testo); return; }
        var pulito = testo.StartsWith("💤 ", StringComparison.Ordinal) ? testo[3..] : testo;
        MostraBolla(testo, new[]
        {
            new SceltaBolla("👍", _ => _ponte.Vota(fonte, pulito, true)),
            new SceltaBolla("👎", _ => _ponte.Vota(fonte, pulito, false))
        }, resta: false);
    }

    void AggiornaPermesso()
    {
        var acceso = _ponte.Impostazioni.PermessoPc;
        _permesso.BorderBrush = acceso ? Stile.RossoP : Stile.Pennello("#3a3648");
        _permesso.Foreground = acceso ? Stile.RossoP : Stile.Inchiostro;
        _permesso.ToolTip = TestoPermesso(acceso);
        AggiornaServizi();
    }

    void AggiornaOrecchie()
    {
        var accese = _ponte.Udito.Accese;
        var chiamata = _ponte.Udito.InChiamata;
        _orecchie.BorderBrush = accese ? Stile.VerdeP : Stile.Pennello("#3a3648");
        _orecchie.Foreground = accese ? Stile.VerdeP : Stile.Inchiostro;
        _orecchie.ToolTip = TestoOrecchie(accese);
        _orecchie.ToolTip = TestoOrecchie(accese) + (chiamata ? " Adesso sento anche " + _ponte.Udito.NomeChiamata + " a parte." : "");
        AggiornaServizi();
    }

    string TestoOrecchie(bool accese) => accese
        ? "Orecchie accese: sto sempre in ascolto del mio nome, «" + _ponte.Impostazioni.ParolaSveglia + "». Sento anche l'audio del PC (senza la mia voce) e, se Discord è aperto, la chiamata a parte. Un clic per spegnerle."
        : "Orecchie: mi chiami per nome senza cliccare («" + _ponte.Impostazioni.ParolaSveglia + ", …»). Diverso da Parla, che ascolta una frase sola. Serve Parakeet (la luce bianca).";

    static string TestoPermesso(bool acceso) => acceso
        ? "Permesso del PC acceso: apro un programma dopo il tuo sì, apro le cartelle, cambio il volume e guardo lo schermo. Non clicco e non scrivo. Un clic per spegnerlo."
        : "Permesso del PC spento: non tocco niente fuori dal mio mondo. Un clic per accenderlo.";

    void AggiornaServizi() => AggiornaLuci();

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
                new SceltaBolla("Sì", _testo => { _domandaAperta = false; _ = _ponte.Messaggio("sì"); }),
                new SceltaBolla("No", _testo => { _domandaAperta = false; _ = _ponte.Messaggio("no"); })
            }, resta: true);
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
            }, Campo: true),
            new SceltaBolla("Lascia", _testo => { _domandaAperta = false; })
        }, resta: true);
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
        Voce("Chat", () => ApriCasa().ModoChat());
        Voce("Mondo", () => ApriCasa().ModoMondo());
        Voce("Nota veloce", ChiediNota);
        Voce("Un pensiero adesso", () => _ = _ponte.PensieroAdesso());
        Voce(_ponte.Udito.Accese ? "Spegni le Orecchie" : "Accendi le Orecchie", () => _ = _ponte.AlternaOrecchie());
        Voce(_ponte.Impostazioni.PermessoPc ? "Spegni le mani sul PC" : "Accendi le mani sul PC", () => _ponte.AlternaPermesso());
        Voce(_ponte.Regista.InPausa ? "Riprendi (togli la pausa)" : "Pausa", () => _ = _ponte.AlternaPausa());
        Voce(_ponte.Dormita is null ? "Dormi" : "Sveglia", () => _ponte.AlternaSonno());
        Voce("STOP", () => _ponte.Stop());
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
                if (char.IsLetter(scelta.Etichetta[0]))
                {
                    bottone.FontFamily = Stile.Testo;
                    bottone.FontSize = 15;
                }
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
