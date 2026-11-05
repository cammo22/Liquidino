using System.Windows;
using System.Windows.Controls;
using Liquidino.Nucleo;

namespace Liquidino;

/// <summary>
/// Registra la parola per svegliarlo qualche volta con la tua voce, guarda come la scrive Parakeet e tara la soglia.
/// Le registrazioni restano in orecchio/registrazioni, sul tuo PC; si cancellano da qui.
/// </summary>
public sealed class FinestraParola : Window
{
    static readonly TimeSpan Durata = TimeSpan.FromSeconds(2.5);
    readonly Ponte _ponte;
    readonly StackPanel _elenco = new();
    readonly TextBlock _esito;
    readonly Button _registra;
    readonly List<(string File, string Sentito)> _registrazioni = [];
    bool _occupata;

    internal int Registrazioni => _registrazioni.Count;

    public FinestraParola(Ponte ponte)
    {
        _ponte = ponte;
        var parola = ponte.Impostazioni.ParolaSveglia;
        Title = "Parola";
        Width = 640;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;

        _esito = Mainframe.Nota("");
        _esito.Foreground = Mainframe.CianoP;
        var spiega = Mainframe.Nota(
            "Premi Registra e di' «" + parola + "» come lo dici di solito, una volta sola. Registro " + Durata.TotalSeconds.ToString("0.#") + " secondi. " +
            "Ripeti 5 volte, meglio 10, anche con voci un po' diverse (stanco, allegro, da lontano). " +
            "Poi premi Tara: imparo come Parakeet scrive la tua voce e metto la soglia sopra le parole italiane che somigliano. " +
            "Le registrazioni restano sul tuo PC, nella cartella orecchio/registrazioni dei dati.");
        spiega.Foreground = Stile.Inchiostro;
        spiega.FontSize = 15;

        _registra = Mainframe.Tasto("● Registra");
        _registra.Click += async (_, _) => await Registra();
        var tara = Mainframe.Tasto("Tara");
        tara.Click += (_, _) => Tara();
        var cancella = Mainframe.Tasto("Cancella tutte", pericolo: true);
        cancella.Click += (_, _) => CancellaTutte();
        var tasti = new WrapPanel();
        foreach (var t in new[] { _registra, tara, cancella }) tasti.Children.Add(t);

        var sezione = Mainframe.Sezione("«" + parola + "»", out var righe);
        righe.Children.Add(spiega);
        righe.Children.Add(tasti);
        var registrate = Mainframe.Sezione("Le tue registrazioni", out var dentro);
        dentro.Children.Add(_elenco);

        var corpo = new StackPanel { Margin = new Thickness(20, 0, 20, 0) };
        corpo.Children.Add(sezione);
        corpo.Children.Add(registrate);

        var chiudi = Mainframe.Tasto("Chiudi", pericolo: true);
        chiudi.Click += (_, _) => Close();
        var testata = Mainframe.Testata("PAROLA", chiudi);
        var piede = new Border { Margin = new Thickness(20, 4, 20, 12), Child = _esito };
        var tutto = new DockPanel();
        DockPanel.SetDock(testata, Dock.Top);
        DockPanel.SetDock(piede, Dock.Bottom);
        tutto.Children.Add(testata);
        tutto.Children.Add(piede);
        tutto.Children.Add(new ScrollViewer { Content = corpo, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = Mainframe.Cornice(tutto);

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is Button) return;
            try { DragMove(); } catch { /* già rilasciato */ }
        };
        Loaded += async (_, _) => await Carica();
    }

    /// <summary>All'apertura riascolta le registrazioni già fatte, così la taratura usa la versione di Parakeet che c'è adesso.</summary>
    async Task Carica()
    {
        var cartella = ParolaSveglia.CartellaRegistrazioni();
        var file = Directory.Exists(cartella) ? Directory.GetFiles(cartella, "*.wav").Order().ToList() : [];
        if (!_ponte.Udito.ParakeetPresente)
        {
            _registra.IsEnabled = false;
            _esito.Text = Testo.FalloTu("per registrare la parola mi serve Parakeet. Scaricalo da Mainframe → Modelli, poi riapri questa finestra");
            foreach (var f in file) _registrazioni.Add((f, "(da riascoltare quando c'è Parakeet)"));
            Riempi();
            return;
        }
        if (file.Count > 0) _esito.Text = "Riascolto le " + file.Count + " registrazioni…";
        _occupata = true;
        try
        {
            var parakeet = await _ponte.Udito.Trascrittore();
            foreach (var f in file)
            {
                string sentito;
                try
                {
                    var audio = Wav.Leggi(await File.ReadAllBytesAsync(f));
                    var campioni = Campioni.A16kMono(audio.Campioni, audio.Frequenza, audio.Canali);
                    sentito = await Task.Run(() => parakeet.Trascrivi(campioni));
                }
                catch (Exception e) { sentito = "(non si legge: " + e.Message + ")"; }
                _registrazioni.Add((f, sentito));
            }
            _esito.Text = _ponte.Udito.Parola.Tarata
                ? "Tarata su " + _ponte.Udito.Parola.Registrazioni + " registrazioni, soglia " + Testo.FormatoVirgola(_ponte.Udito.Parola.Soglia) + "."
                : file.Count == 0 ? "Ancora nessuna registrazione." : "Registrazioni pronte: premi Tara.";
        }
        catch (Exception e) { _esito.Text = Testo.FalloTu("Parakeet non parte: " + e.Message); }
        finally { _occupata = false; }
        Riempi();
    }

    async Task Registra()
    {
        if (_occupata) return;
        _occupata = true;
        _registra.IsEnabled = false;
        try
        {
            var dispositivo = Dispositivi.Microfono(_ponte.Impostazioni.Microfono);
            if (dispositivo is null)
            {
                _esito.Text = Testo.FalloTu("non trovo un microfono. Collegalo, o sceglilo nel Mainframe → Orecchie");
                return;
            }
            _esito.Text = "Parla adesso: «" + _ponte.Impostazioni.ParolaSveglia + "»…";
            // Mentre registri, le Orecchie non devono prendere la parola per un comando.
            _ponte.Udito.SilenziaFino(DateTime.UtcNow + Durata + TimeSpan.FromSeconds(2));
            var campioni = await CatturaMicrofono.Registra(dispositivo, Durata, CancellationToken.None);
            if (Campioni.Rms(campioni) < 0.003)
            {
                _esito.Text = Testo.FalloTu("non ho sentito niente. Controlla il microfono (" + dispositivo.FriendlyName + ") e riprova");
                return;
            }
            var cartella = ParolaSveglia.CartellaRegistrazioni();
            Directory.CreateDirectory(cartella);
            var file = Path.Combine(cartella, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".wav");
            await File.WriteAllBytesAsync(file, Wav.Scrivi(campioni, Campioni.Frequenza));
            var parakeet = await _ponte.Udito.Trascrittore();
            var sentito = await Task.Run(() => parakeet.Trascrivi(campioni));
            _registrazioni.Add((file, sentito));
            _esito.Text = sentito.Length == 0
                ? "Registrata, ma Parakeet non ha capito parole: avvicinati al microfono e riprova."
                : "Ho sentito «" + sentito + "». " + (_registrazioni.Count < 5 ? "Ancora " + (5 - _registrazioni.Count) + ", poi Tara." : "Puoi premere Tara.");
            Riempi();
        }
        catch (Exception e)
        {
            _esito.Text = Testo.FalloTu("non sono riuscito a registrare: " + e.Message);
            _ponte.Registro.SegnaErrore("registrazione della parola: " + e.Message);
        }
        finally
        {
            _occupata = false;
            _registra.IsEnabled = _ponte.Udito.ParakeetPresente;
        }
    }

    void Tara()
    {
        if (_occupata) return;
        var esito = ParolaSveglia.Tara(_ponte.Impostazioni.ParolaSveglia, _registrazioni.Select(r => r.Sentito).ToList());
        try
        {
            ParolaSveglia.Salva(esito.Parola);
            _ponte.Udito.RicaricaParola();
            _ponte.Registro.Scrivi("orecchie", "parola tarata su " + esito.Parola.Registrazioni + " registrazioni, soglia " + Testo.FormatoVirgola(esito.Parola.Soglia), esito.Buona ? "ok" : "da rifare", "orecchie");
            _esito.Text = esito.Frase;
        }
        catch (Exception e) { _esito.Text = Testo.FalloTu("non riesco a salvare la taratura: " + e.Message); }
    }

    void CancellaTutte()
    {
        if (_occupata || _registrazioni.Count == 0) return;
        if (MessageBox.Show(this, "Cancello le " + _registrazioni.Count + " registrazioni della tua voce e la taratura?\n\nNon si possono recuperare.",
                "Liquidino · Parola", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        foreach (var (f, _) in _registrazioni)
        {
            try { if (Percorsi.StaDentro(ParolaSveglia.CartellaRegistrazioni(), f)) File.Delete(f); }
            catch (Exception e) { _ponte.Registro.SegnaErrore("cancella registrazione: " + e.Message); }
        }
        try { if (File.Exists(ParolaSveglia.FileParola())) File.Delete(ParolaSveglia.FileParola()); } catch { /* resta la taratura vecchia */ }
        _registrazioni.Clear();
        _ponte.Udito.RicaricaParola();
        _ponte.Registro.Scrivi("orecchie", "registrazioni della parola cancellate", "ok", "orecchie");
        _esito.Text = "Cancellate. Uso la parola così com'è scritta, finché non registri di nuovo.";
        Riempi();
    }

    void Togli(int indice)
    {
        if (_occupata || indice < 0 || indice >= _registrazioni.Count) return;
        var (f, _) = _registrazioni[indice];
        try { if (Percorsi.StaDentro(ParolaSveglia.CartellaRegistrazioni(), f)) File.Delete(f); }
        catch (Exception e) { _esito.Text = Testo.FalloTu("non riesco a cancellarla: " + e.Message); return; }
        _registrazioni.RemoveAt(indice);
        _esito.Text = "Tolta. Premi Tara per aggiornare la taratura.";
        Riempi();
    }

    void Riempi()
    {
        _elenco.Children.Clear();
        if (_registrazioni.Count == 0)
        {
            _elenco.Children.Add(Mainframe.Nota("Nessuna registrazione."));
            return;
        }
        var parola = ParolaPersonale.DiSerie(_ponte.Impostazioni.ParolaSveglia);
        for (var i = 0; i < _registrazioni.Count; i++)
        {
            var indice = i;
            var (_, sentito) = _registrazioni[i];
            var somiglia = sentito.StartsWith('(') ? "" : " · somiglia " + (int)Math.Round(ParolaSveglia.Somiglianza(sentito, parola.Parola) * 100) + "%";
            var togli = Mainframe.Tasto("✕", pericolo: true);
            togli.Padding = new Thickness(8, 0, 8, 0);
            togli.Click += (_, _) => Togli(indice);
            var testo = new TextBlock
            {
                Text = (i + 1) + ". «" + sentito + "»" + somiglia,
                FontFamily = Stile.Testo,
                FontSize = 15,
                Foreground = Mainframe.CianoP,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            var riga = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            DockPanel.SetDock(togli, Dock.Right);
            riga.Children.Add(togli);
            riga.Children.Add(testo);
            _elenco.Children.Add(riga);
        }
    }
}
