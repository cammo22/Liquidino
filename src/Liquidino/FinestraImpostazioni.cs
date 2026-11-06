using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Liquidino.Nucleo;
using Microsoft.Win32;

namespace Liquidino;

/// <summary>
/// Le impostazioni: barra DaProd in alto (come la chat), schede a sinistra, una scheda per volta a destra.
/// Ogni scheda sta in una schermata. Salva in alto applica tutto; Esc chiude senza salvare.
/// </summary>
public sealed class FinestraImpostazioni : Window
{
    static readonly int[] Volumi = [50, 75, 100, 125, 150, 175, 200];
    static readonly int[] SonnoMinuti = [10, 20, 30, 45, 60, 90, 120];
    static readonly double[] Scale = [0.7, 0.85, 1.0, 1.25, 1.5, 1.75, 2.0];

    readonly Ponte _ponte;
    readonly ContentControl _pagina = new();
    readonly StackPanel _schede = new() { Margin = new Thickness(10, 10, 6, 10) };
    readonly Dictionary<string, Func<FrameworkElement>> _costruttori = new();
    readonly Dictionary<string, FrameworkElement> _costruite = new();
    readonly Dictionary<string, Border> _linguette = new();
    readonly TextBlock _esito;
    string _aperta = "";

    // I valori che Salva legge: ci sono solo se la loro scheda è stata aperta.
    TextBox? _nome;
    Mainframe.Selettore? _carattere;
    Mainframe.Selettore? _voce;
    Mainframe.Interruttore? _voceWindows;
    Mainframe.Interruttore? _leggePensieri;
    TextBox? _parola;
    Mainframe.Selettore? _microfono;
    List<string> _microfoni = [];
    Mainframe.Interruttore? _ascoltaPc;
    Mainframe.Interruttore? _voceMic;
    Mainframe.Selettore? _volumeMic;
    Mainframe.Interruttore? _ciclo;
    Mainframe.Selettore? _ogni;
    Mainframe.Selettore? _sonnoMassimo;
    Mainframe.Selettore? _sogniOgni;
    Mainframe.Interruttore? _voti;
    Mainframe.Selettore? _oggetto;
    IReadOnlyList<string> _oggetti = [];
    Mainframe.Interruttore? _cambiaDaSolo;
    Mainframe.Selettore? _scala;
    Mainframe.Interruttore? _pausa;
    TextBox? _cartella;
    TextBox? _porta;
    TextBlock? _contesto;
    TextBlock? _statoFirewall;
    TextBlock? _statoOrecchie;

    internal int Schede => _costruttori.Count;

    public FinestraImpostazioni(Ponte ponte, string? scheda = null)
    {
        _ponte = ponte;
        Title = "Impostazioni";
        Width = 940;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;

        _esito = new TextBlock { FontFamily = Stile.Testo, FontSize = 14, Foreground = Stile.CianoP, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };

        Aggiungi("Tu", "👤", PaginaTu);
        Aggiungi("Voce", "🔊", PaginaVoce);
        Aggiungi("Orecchie", "👂", PaginaOrecchie);
        Aggiungi("Pensieri e sonno", "🌙", PaginaSonno);
        Aggiungi("Avatar", "💿", PaginaAvatar);
        Aggiungi("Modelli", "💡", () => new PannelloModelli(_ponte));
        Aggiungi("Motore", "⚙", PaginaMotore);
        Aggiungi("Sicurezza", "🛡", PaginaSicurezza);
        Aggiungi("Strumenti", "🧰", PaginaStrumenti);

        var salva = Stile.Bottone("Salva", principale: true);
        salva.Click += async (_, _) =>
        {
            try { await Salva(); }
            catch (Exception e) { _esito.Text = Testo.FalloTu("non sono riuscito a salvare. " + e.Message); }
        };
        var chiudi = Stile.Bottone("Chiudi");
        chiudi.Click += (_, _) => Close();
        var testata = Mainframe.Testata("impostazioni", salva, chiudi);
        testata.MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is Button) return;
            try { DragMove(); } catch { /* già rilasciato */ }
        };

        var piede = new Border { Padding = new Thickness(14, 6, 14, 8), Child = _esito, BorderBrush = Mainframe.LineaP, BorderThickness = new Thickness(0, 1, 0, 0), MinHeight = 34 };
        var lato = new Border { Background = Stile.Carta, Width = 190, Child = _schede };
        var corpo = new DockPanel();
        DockPanel.SetDock(lato, Dock.Left);
        corpo.Children.Add(lato);
        corpo.Children.Add(new Border { Padding = new Thickness(14, 10, 14, 6), Child = _pagina });

        var tutto = new DockPanel();
        DockPanel.SetDock(testata, Dock.Top);
        DockPanel.SetDock(piede, Dock.Bottom);
        tutto.Children.Add(testata);
        tutto.Children.Add(piede);
        tutto.Children.Add(corpo);
        Content = Mainframe.Cornice(tutto);

        Apri(scheda is not null && _costruttori.ContainsKey(scheda) ? scheda : "Tu");
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    void Aggiungi(string nome, string simbolo, Func<FrameworkElement> costruisci)
    {
        _costruttori[nome] = costruisci;
        var testo = new StackPanel { Orientation = Orientation.Horizontal };
        testo.Children.Add(new TextBlock { Text = simbolo, Width = 26, FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol"), FontSize = 15, VerticalAlignment = VerticalAlignment.Center });
        testo.Children.Add(new TextBlock { Text = nome, FontFamily = Stile.Testo, FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
        var linguetta = new Border { Child = testo, Padding = new Thickness(10, 6, 8, 6), Margin = new Thickness(0, 1, 0, 1), CornerRadius = new CornerRadius(6), Cursor = Cursors.Hand, Background = Brushes.Transparent };
        linguetta.MouseLeftButtonUp += (_, _) => Apri(nome);
        _linguette[nome] = linguetta;
        _schede.Children.Add(linguetta);
    }

    /// <summary>Apre una scheda. Si costruisce la prima volta che la apri e resta: Salva le legge tutte.</summary>
    public void Apri(string nome)
    {
        if (!_costruite.TryGetValue(nome, out var pagina))
        {
            pagina = _costruttori[nome]();
            _costruite[nome] = pagina;
        }
        _aperta = nome;
        _pagina.Content = pagina;
        foreach (var (n, l) in _linguette)
        {
            var scelta = n == _aperta;
            l.Background = scelta ? Stile.Pennello("#3a3010") : Brushes.Transparent;
            foreach (var t in ((StackPanel)l.Child).Children.OfType<TextBlock>()) t.Foreground = scelta ? Stile.OroP : Stile.Inchiostro2;
        }
    }

    /// <summary>Apre tutte le schede una volta: la usa la prova di avvio.</summary>
    internal void ApriTutte()
    {
        foreach (var n in _costruttori.Keys.ToList()) Apri(n);
        Apri("Tu");
    }

    // ---------- schede ----------

    FrameworkElement PaginaTu()
    {
        var imp = _ponte.Impostazioni;
        var pila = new StackPanel();
        pila.Children.Add(Mainframe.Sezione("Tu e lui", out var r));
        _nome = Mainframe.Campo(imp.NomeUtente);
        r.Children.Add(Mainframe.Riga("Come ti chiama", _nome));
        _carattere = new Mainframe.Selettore(ImpostazioniUtente.Caratteri, Array.IndexOf(ImpostazioniUtente.Caratteri, imp.Carattere));
        var descrizione = Mainframe.Nota(ImpostazioniUtente.DescrizioneCarattere(imp.Carattere));
        _carattere.Cambiato += i => descrizione.Text = ImpostazioniUtente.DescrizioneCarattere(ImpostazioniUtente.Caratteri[i]);
        r.Children.Add(Mainframe.Riga("Carattere", _carattere));
        r.Children.Add(descrizione);

        pila.Children.Add(Mainframe.Sezione("Personalizza a mano", out var p));
        p.Children.Add(Mainframe.Nota("Due file di testo che puoi aprire e cambiare. Le regole di sicurezza restano nel programma e valgono sempre."));
        var tasti = new WrapPanel();
        var prompt = Mainframe.Tasto("Il suo modo di parlare (sistema.md)");
        prompt.Click += (_, _) => ApriFile(Percorsi.Prompt(_ponte.Mondo.RadiceDati));
        var memoria = Mainframe.Tasto("Cosa sa di te (memoria)");
        memoria.Click += (_, _) => ApriFile(_ponte.Mondo.PercorsoMemoria());
        tasti.Children.Add(prompt);
        tasti.Children.Add(memoria);
        p.Children.Add(tasti);
        return pila;
    }

    FrameworkElement PaginaVoce()
    {
        var imp = _ponte.Impostazioni;
        var pila = new StackPanel();
        var prova = Mainframe.Tasto("Prova la voce");
        prova.Click += async (_, _) =>
        {
            ApplicaVoce();
            _esito.Text = "Provo a leggere una frase…";
            try { await _ponte.Parla("Ciao, sono Liquidino. Mi senti bene?", _ponte.Token()); }
            catch (OperationCanceledException) { /* STOP */ }
            _esito.Text = "Se non hai sentito niente, guarda il volume e l'uscita audio.";
        };
        pila.Children.Add(Mainframe.Sezione("Voce", out var r, prova));
        _voce = new Mainframe.Selettore(["femminile", "maschile"], imp.Voce == "male" ? 1 : 0);
        r.Children.Add(Mainframe.Riga("Voce", _voce));
        _voceWindows = new Mainframe.Interruttore(imp.VoceWindows);
        r.Children.Add(Mainframe.Riga("Voci di Windows", _voceWindows, "Se Omni non c'è o non parla, legge con le voci italiane di Windows."));
        _leggePensieri = new Mainframe.Interruttore(imp.CompagniaVocale);
        r.Children.Add(Mainframe.Riga("Legge ad alta voce i pensieri", _leggePensieri, "Solo con pensieri ogni 5 minuti o più: con pensieri fitti resta il fumetto."));
        return pila;
    }

    FrameworkElement PaginaOrecchie()
    {
        var imp = _ponte.Impostazioni;
        var pila = new StackPanel();
        var registra = Mainframe.Tasto("Registra la parola");
        registra.Click += (_, _) =>
        {
            new FinestraParola(_ponte) { Owner = this }.ShowDialog();
            if (_statoOrecchie is not null) _statoOrecchie.Text = StatoOrecchie();
        };
        pila.Children.Add(Mainframe.Sezione("Orecchie", out var r, registra));
        _parola = Mainframe.Campo(imp.ParolaSveglia);
        r.Children.Add(Mainframe.Riga("Parola per chiamarlo", _parola));
        _microfoni = Dispositivi.Ingressi().Where(n => !CavoVirtuale.ECavo(n)).ToList();
        var voci = new List<string> { "Predefinito di Windows" };
        voci.AddRange(_microfoni);
        _microfono = new Mainframe.Selettore(voci, Math.Max(0, _microfoni.IndexOf(imp.Microfono) + 1));
        r.Children.Add(Mainframe.Riga("Microfono", _microfono));
        _ascoltaPc = new Mainframe.Interruttore(imp.AscoltaPc);
        r.Children.Add(Mainframe.Riga("Sente l'audio del PC", _ascoltaPc, "Video, musica, giochi, app, tranne la sua voce. Lo tiene in mente 3 minuti, senza salvarlo. Non sono ordini."));
        _statoOrecchie = Mainframe.Nota(StatoOrecchie());
        r.Children.Add(_statoOrecchie);

        pila.Children.Add(Mainframe.Sezione("Discord", out var d));
        d.Children.Add(Mainframe.Nota("Con le Orecchie accese, se Discord è aperto lo sente da solo, a parte. Chi lo chiama per nome in chiamata riceve una risposta a voce; le mani le comandi solo tu."));
        _voceMic = new Mainframe.Interruttore(imp.VoceNelMicrofono);
        d.Children.Add(Mainframe.Riga("La sua voce nel tuo microfono", _voceMic, "In Discord scegli «CABLE Output» come microfono: gli altri sentono te e lui."));
        var volume = Array.IndexOf(Volumi, imp.VolumeVoceMicrofono);
        _volumeMic = new Mainframe.Selettore(Volumi.Select(v => v + "%").ToList(), volume < 0 ? 2 : volume);
        d.Children.Add(Mainframe.Riga("Quanto forte", _volumeMic));
        var cavo = CavoVirtuale.Ingresso(Dispositivi.Uscite());
        var scarica = Mainframe.Tasto("Scarica VB-CABLE");
        scarica.ToolTip = "Apre " + CavoVirtuale.PaginaUfficiale + " nel tuo browser. Lo installi tu, poi riavvii il PC.";
        scarica.Click += (_, _) => ApriSito(CavoVirtuale.PaginaUfficiale);
        d.Children.Add(Mainframe.Riga("VB-CABLE", cavo is null ? scarica : new TextBlock { Text = "installato", FontFamily = Stile.Testo, FontSize = 14, Foreground = Stile.CianoP }));
        return pila;
    }

    FrameworkElement PaginaSonno()
    {
        var imp = _ponte.Impostazioni;
        var pila = new StackPanel();
        pila.Children.Add(Mainframe.Sezione("Pensieri da sveglio", out var r));
        _ciclo = new Mainframe.Interruttore(imp.CicloAutonomo);
        r.Children.Add(Mainframe.Riga("Un pensiero ogni tanto", _ciclo, "Lo scrive in mondo/idee e te lo mostra in un fumetto, con 👍 e 👎."));
        var ogni = Array.IndexOf(Sonno.SecondiPensieri, imp.CicloSecondi);
        _ogni = new Mainframe.Selettore(Sonno.SecondiPensieri.Select(Sonno.Intervallo).ToList(), ogni < 0 ? 8 : ogni);
        r.Children.Add(Mainframe.Riga("Ogni", _ogni));

        pila.Children.Add(Mainframe.Sezione("Sonno", out var s));
        s.Children.Add(Mainframe.Nota("Niente orari: lo metti a dormire tu (🌙). Sceglie lui quanto, in base a quanto è stata piena la giornata, e intanto sogna. Si sveglia da solo; se lo svegli prima, la dormita non conta."));
        var massimo = Array.IndexOf(SonnoMinuti, imp.SonnoMassimoMinuti);
        _sonnoMassimo = new Mainframe.Selettore(SonnoMinuti.Select(m => Sonno.Leggibile(TimeSpan.FromMinutes(m))).ToList(), massimo < 0 ? SonnoMinuti.Length - 1 : massimo);
        s.Children.Add(Mainframe.Riga("Dorme al massimo", _sonnoMassimo));
        var sogni = Array.IndexOf(Sonno.SecondiSogni, imp.SogniSecondi);
        _sogniOgni = new Mainframe.Selettore(Sonno.SecondiSogni.Select(Sonno.Intervallo).ToList(), sogni < 0 ? 2 : sogni);
        s.Children.Add(Mainframe.Riga("Sogna ogni", _sogniOgni));
        _voti = new Mainframe.Interruttore(imp.MostraVoti);
        var (si, no) = Palestra.Conta(_ponte.Mondo.RadiceDati);
        s.Children.Add(Mainframe.Riga("👍 e 👎 su pensieri, sogni e chat", _voti, "Lui non risponde ai voti: li tiene per allenarsi. Finora " + si + " 👍 e " + no + " 👎."));
        s.Children.Add(Mainframe.Riga("Dormite complete", new TextBlock { Text = imp.DormiteComplete.ToString(), FontFamily = Stile.Display, FontSize = 18, Foreground = Stile.OroP }));
        return pila;
    }

    FrameworkElement PaginaAvatar()
    {
        var imp = _ponte.Impostazioni;
        var pila = new StackPanel();
        pila.Children.Add(Mainframe.Sezione("Avatar", out var r));
        _oggetti = FoglioAvatar.Disponibili();
        _oggetto = new Mainframe.Selettore(_oggetti.Count == 0 ? ["—"] : _oggetti.ToList(), Math.Max(0, _oggetti.ToList().IndexOf(imp.AvatarOggetto)));
        r.Children.Add(Mainframe.Riga("Oggetto", _oggetto, "Gli oggetti già approvati del Convertitore DaProd."));
        _cambiaDaSolo = new Mainframe.Interruttore(imp.AvatarCambioAuto);
        r.Children.Add(Mainframe.Riga("Cambia oggetto da solo", _cambiaDaSolo));
        var scala = Array.FindIndex(Scale, x => Math.Abs(x - imp.AvatarScala) < 0.06);
        _scala = new Mainframe.Selettore(Scale.Select(x => (int)Math.Round(x * 100) + "%").ToList(), scala < 0 ? 2 : scala);
        r.Children.Add(Mainframe.Riga("Grandezza", _scala, "Anche con la rotella del mouse sopra l'avatar."));
        _pausa = new Mainframe.Interruttore(_ponte.Regista.InPausa);
        r.Children.Add(Mainframe.Riga("Pausa", _pausa, "In pausa niente si carica da solo e l'avatar è in bianco e nero. I modelli li accendi dalle luci."));
        return pila;
    }

    FrameworkElement PaginaMotore()
    {
        var imp = _ponte.Impostazioni;
        var pila = new StackPanel();
        pila.Children.Add(Mainframe.Sezione("Motore", out var r));
        var rigaCartella = new DockPanel();
        var sfoglia = Mainframe.Tasto("Sfoglia");
        sfoglia.Margin = new Thickness(6, 0, 0, 0);
        sfoglia.Click += (_, _) => Sfoglia();
        DockPanel.SetDock(sfoglia, Dock.Right);
        _cartella = Mainframe.Campo(imp.CartellaMotore);
        rigaCartella.Children.Add(sfoglia);
        rigaCartella.Children.Add(_cartella);
        r.Children.Add(Mainframe.Riga("Cartella del motore", rigaCartella, "Dove stanno llama-server e la cartella «modelli»."));
        _porta = Mainframe.Campo(imp.Porta.ToString());
        _porta.Width = 100;
        _porta.HorizontalAlignment = HorizontalAlignment.Left;
        r.Children.Add(Mainframe.Riga("Porta", _porta, "Solo su 127.0.0.1: il motore ascolta solo questo PC."));
        r.Children.Add(Mainframe.Nota(StatoMotore()));

        pila.Children.Add(Mainframe.Sezione("Contesto", out var c));
        _contesto = Mainframe.Nota(TestoContesto());
        c.Children.Add(_contesto);
        var misura = Mainframe.Tasto("Misura il PC");
        misura.Click += (_, _) =>
        {
            imp.ApplicaContesto(SceltaContesto.Scegli(MisuraPc.Misura()));
            _contesto.Text = TestoContesto();
            _esito.Text = imp.FraseContesto;
        };
        var prova = Mainframe.Tasto("Prova il contesto sul serio");
        prova.ToolTip = "Riempie davvero la memoria del motore, da 128K in giù finché regge. Qualche minuto. Lo stesso tasto la ferma.";
        var inCorso = false;
        prova.Click += async (_, _) =>
        {
            if (inCorso) { _ponte.Annulla(); _esito.Text = "Fermo la prova…"; return; }
            inCorso = true;
            prova.Content = "Ferma la prova";
            _esito.Text = "Comincio la prova di carico. Il motore si riaccende a ogni gradino.";
            try
            {
                var esito = await _ponte.ProvaContestoVera(passo => _esito.Text = passo);
                _esito.Text = esito.Frase();
                _contesto.Text = TestoContesto();
            }
            finally
            {
                inCorso = false;
                prova.Content = "Prova il contesto sul serio";
            }
        };
        var tasti = new WrapPanel();
        tasti.Children.Add(misura);
        tasti.Children.Add(prova);
        c.Children.Add(tasti);
        return pila;
    }

    FrameworkElement PaginaSicurezza()
    {
        var imp = _ponte.Impostazioni;
        var pila = new StackPanel();
        pila.Children.Add(Mainframe.Sezione("Sicurezza", out var r));
        r.Children.Add(Mainframe.Riga("Mani sul PC", new TextBlock
        {
            Text = imp.PermessoPc ? "accese" : "spente",
            FontFamily = Stile.Testo,
            FontSize = 15,
            Foreground = imp.PermessoPc ? Stile.CianoP : Stile.RossoP
        }, "Si cambiano col tasto 🖥️ Mani vicino all'avatar. Accese: apre programmi dopo il tuo sì, cartelle, volume, guarda lo schermo. Non clicca e non scrive."));
        _statoFirewall = Mainframe.Nota("Regola del firewall: controllo…");
        r.Children.Add(_statoFirewall);
        var firewall = Mainframe.Tasto("Togli internet a llama-server");
        firewall.ToolTip = "Crea una regola di Windows Firewall. Windows chiede il permesso dell'amministratore una volta.";
        firewall.Click += async (_, _) =>
        {
            if (_ponte.Regista.Eseguibile is not { } exe) { _esito.Text = Testo.FalloTu("non trovo llama-server nella cartella del motore"); return; }
            firewall.IsEnabled = false;
            _esito.Text = "Windows ti chiederà il permesso…";
            try
            {
                var detto = await Firewall.Crea(exe);
                _esito.Text = detto;
                _ponte.Registro.Scrivi("firewall", detto, detto.StartsWith("Fatto", StringComparison.Ordinal) ? "ok" : "rifiutato");
                await AggiornaFirewall();
            }
            finally { firewall.IsEnabled = true; }
        };
        r.Children.Add(firewall);
        _ = AggiornaFirewall();
        return pila;
    }

    FrameworkElement PaginaStrumenti()
    {
        var pila = new StackPanel();
        pila.Children.Add(Mainframe.Sezione("Strumenti", out var r));
        var tasti = new WrapPanel();
        var dettagli = Mainframe.Tasto("Copia dettagli errore");
        dettagli.Click += (_, _) => CopiaDettagli();
        var dati = Mainframe.Tasto("Cartella dei dati");
        dati.ToolTip = Percorsi.RadiceDati();
        dati.Click += (_, _) => ApriCartella(Percorsi.RadiceDati());
        var sogni = Mainframe.Tasto("Cartella dei sogni");
        sogni.Click += (_, _) => ApriCartella(Path.Combine(_ponte.Mondo.Radice, "sogni"));
        tasti.Children.Add(dettagli);
        tasti.Children.Add(dati);
        tasti.Children.Add(sogni);
        r.Children.Add(tasti);
        r.Children.Add(Mainframe.Nota("«Copia dettagli errore» mette negli appunti versione, sistema, motore e le ultime righe del diario tecnico. Niente chat né memoria."));
        return pila;
    }

    // ---------- aiuti ----------

    string StatoMotore()
    {
        var r = _ponte.Regista.Rilevazione;
        var n = r.Modelli.Count;
        return (r.Eseguibile is null ? "llama-server non trovato" : "llama-server trovato") + " · " + (n == 0 ? "nessun modello" : n == 1 ? "1 modello" : n + " modelli") + " in " + r.CartellaModelli;
    }

    string StatoOrecchie()
    {
        var imp = _ponte.Impostazioni;
        var stato = OrecchioLeggero.Stato(imp.ParolaSveglia, _ponte.Regista.Rilevazione.CartellaModelli).Messaggio;
        var adesso = _ponte.Udito.Accese
            ? "Adesso: accese" + (_ponte.Udito.NomeMicrofono is { } m ? ", microfono " + m : ", senza microfono") + (_ponte.Udito.SenteIlPc ? ", audio del PC" : "")
              + (_ponte.Udito.NomeChiamata is { } c ? ", " + c + " a parte" : "") + "."
            : "Adesso: spente.";
        return adesso + " " + stato;
    }

    string TestoContesto()
    {
        var imp = _ponte.Impostazioni;
        return "Mani " + imp.ContestoMani / 1024 + "K, Omni " + imp.ContestoOmni / 1024 + "K, d1 " + imp.ContestoD1 / 1024 + "K."
               + (string.IsNullOrWhiteSpace(imp.FraseContesto) ? "" : " " + imp.FraseContesto);
    }

    async Task AggiornaFirewall()
    {
        if (_statoFirewall is null) return;
        if (_ponte.Regista.Eseguibile is not { } exe)
        {
            _statoFirewall.Text = "Regola del firewall: llama-server non è stato trovato.";
            return;
        }
        try
        {
            var c = await Firewall.Controlla(exe);
            _statoFirewall.Text = c switch
            {
                true => "Regola del firewall: c'è, llama-server non va in internet.",
                false => "Regola del firewall: non c'è. llama-server ascolta solo su 127.0.0.1, ma la regola lo chiude anche verso fuori.",
                _ => "Regola del firewall: non riesco a leggerla."
            };
        }
        catch { _statoFirewall.Text = "Regola del firewall: non riesco a leggerla."; }
    }

    void ApplicaVoce()
    {
        var imp = _ponte.Impostazioni;
        if (_voce is not null) imp.Voce = _voce.Indice == 1 ? "male" : "female";
        if (_voceWindows is not null) imp.VoceWindows = _voceWindows.Acceso;
    }

    void CopiaDettagli()
    {
        var sistema = Environment.OSVersion.VersionString + (Environment.Is64BitOperatingSystem ? ", 64 bit" : "");
        var testo = DettagliErrore.Componi(Ponte.Versione, sistema, _ponte.Regista.Riassunto(), _ponte.Registro);
        try
        {
            Clipboard.SetText(testo);
            _esito.Text = "Dettagli copiati. Prima di incollarli guarda che non ci sia niente di tuo.";
        }
        catch (Exception e) { _esito.Text = Testo.FalloTu("gli appunti sono occupati da un altro programma. Riprova fra un attimo. (" + e.Message + ")"); }
    }

    void Sfoglia()
    {
        if (_cartella is null) return;
        var dialogo = new OpenFolderDialog { Title = "Cartella di llama.cpp", InitialDirectory = _cartella.Text };
        if (dialogo.ShowDialog(this) == true) _cartella.Text = dialogo.FolderName;
    }

    void ApriCartella(string cartella)
    {
        try
        {
            Directory.CreateDirectory(cartella);
            using (Process.Start(new ProcessStartInfo("explorer.exe", "\"" + cartella + "\"") { UseShellExecute = true })) { }
        }
        catch (Exception e) { _esito.Text = Testo.FalloTu("non riesco ad aprire " + cartella + ". " + e.Message); }
    }

    void ApriFile(string file)
    {
        try
        {
            if (!File.Exists(file)) { _esito.Text = Testo.FalloTu("il file " + file + " non c'è ancora"); return; }
            using (Process.Start(new ProcessStartInfo(file) { UseShellExecute = true })) { }
            _esito.Text = "Aperto. Quando lo cambi, riavvia Liquidino perché lo rilegga.";
        }
        catch (Exception e) { _esito.Text = Testo.FalloTu("non riesco ad aprire " + file + ". " + e.Message); }
    }

    void ApriSito(string indirizzo)
    {
        try { using (Process.Start(new ProcessStartInfo(indirizzo) { UseShellExecute = true })) { } }
        catch (Exception e) { _esito.Text = Testo.FalloTu("apri " + indirizzo + " nel browser. " + e.Message); }
    }

    /// <summary>Salva tutte le schede aperte almeno una volta. Quelle mai aperte restano com'erano.</summary>
    async Task Salva()
    {
        var imp = _ponte.Impostazioni;
        if (_nome is not null)
        {
            var nome = _nome.Text.Trim();
            if (nome.Length > 0) imp.NomeUtente = nome.Length > 40 ? nome[..40] : nome;
        }
        if (_carattere is not null) imp.Carattere = _carattere.Valore;
        ApplicaVoce();
        if (_leggePensieri is not null) imp.CompagniaVocale = _leggePensieri.Acceso;

        var riaccendiOrecchie = false;
        if (_parola is not null)
        {
            var parola = _parola.Text.Trim();
            if (parola.Length > 0) imp.ParolaSveglia = parola.Length > 40 ? parola[..40] : parola;
        }
        if (_microfono is not null)
        {
            var microfono = _microfono.Indice == 0 ? "" : _microfoni[_microfono.Indice - 1];
            riaccendiOrecchie |= microfono != imp.Microfono;
            imp.Microfono = microfono;
        }
        if (_ascoltaPc is not null)
        {
            riaccendiOrecchie |= _ascoltaPc.Acceso != imp.AscoltaPc;
            imp.AscoltaPc = _ascoltaPc.Acceso;
        }
        if (_voceMic is not null)
        {
            riaccendiOrecchie |= _voceMic.Acceso != imp.VoceNelMicrofono;
            imp.VoceNelMicrofono = _voceMic.Acceso;
        }
        if (_volumeMic is not null) imp.VolumeVoceMicrofono = Volumi[_volumeMic.Indice];
        _ponte.Udito.RicaricaParola();
        if (_ponte.Udito.Cavo is { } cavo) cavo.Guadagno = imp.VolumeVoceMicrofono / 100f;

        if (_ciclo is not null) imp.CicloAutonomo = _ciclo.Acceso;
        if (_ogni is not null) imp.CicloSecondi = Sonno.SecondiPensieri[_ogni.Indice];
        if (_sonnoMassimo is not null) imp.SonnoMassimoMinuti = SonnoMinuti[_sonnoMassimo.Indice];
        if (_sogniOgni is not null) imp.SogniSecondi = Sonno.SecondiSogni[_sogniOgni.Indice];
        if (_voti is not null) imp.MostraVoti = _voti.Acceso;

        if (_oggetto is not null && _oggetti.Count > 0) imp.AvatarOggetto = _oggetti[_oggetto.Indice];
        if (_cambiaDaSolo is not null) imp.AvatarCambioAuto = _cambiaDaSolo.Acceso;
        if (_scala is not null) imp.AvatarScala = Scale[_scala.Indice];
        var pausaCambiata = _pausa is not null && _pausa.Acceso != _ponte.Regista.InPausa;

        var motoreCambiato = false;
        if (_cartella is not null && _cartella.Text.Trim() != imp.CartellaMotore)
        {
            imp.CartellaMotore = _cartella.Text.Trim();
            motoreCambiato = true;
        }
        if (_porta is not null && int.TryParse(_porta.Text.Trim(), out var porta) && porta is > 0 and < 65536 && porta != imp.Porta)
        {
            imp.Porta = porta;
            motoreCambiato = true;
        }

        if (riaccendiOrecchie && _ponte.Udito.Accese)
        {
            _esito.Text = "Riaccendo le Orecchie…";
            _ponte.Udito.Spegni();
            _ponte.Annuncia(await _ponte.Udito.Accendi());
        }
        if (pausaCambiata) await _ponte.AlternaPausa();
        _esito.Text = motoreCambiato ? "Salvo e riaccendo il motore…" : "Salvo…";
        if (motoreCambiato) await _ponte.RicaricaMotore();
        else
        {
            await Task.Run(() => imp.Salva());
            _ponte.AggiornaOrologi();
        }
        _ponte.AvvisaImpostazioni();
        Close();
    }
}
