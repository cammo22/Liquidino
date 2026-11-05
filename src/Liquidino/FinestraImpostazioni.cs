using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Liquidino.Nucleo;
using Microsoft.Win32;

namespace Liquidino;

/// <summary>
/// Il Mainframe: tutte le impostazioni di Liquidino in una finestra, a due colonne.
/// Le scelte valgono quando premi Salva; prova della voce, prove e firewall partono subito.
/// </summary>
public sealed class FinestraImpostazioni : Window
{
    static readonly int[] Minuti = [15, 30, 45, 60, 90, 120, 180, 240];

    readonly Ponte _ponte;
    readonly TextBox _nome;
    readonly Mainframe.Selettore _carattere;
    readonly Mainframe.Selettore _voce;
    readonly Mainframe.Interruttore _voceWindows;
    readonly Mainframe.Interruttore _ciclo;
    readonly Mainframe.Selettore _minuti;
    readonly Mainframe.Selettore _notteInizio;
    readonly Mainframe.Selettore _notteFine;
    readonly TextBox _cartella;
    readonly TextBox _porta;
    readonly TextBlock _esito;
    readonly TextBlock _contesto;
    readonly TextBlock _statoFirewall;
    readonly TextBlock _motore;
    readonly int[] _sceltaMinuti;
    static readonly int[] Volumi = [50, 75, 100, 125, 150, 175, 200];
    readonly TextBox _parola;
    readonly Mainframe.Selettore _microfono;
    readonly List<string> _microfoni;
    readonly Mainframe.Interruttore _ascoltaPc;
    readonly Mainframe.Interruttore _voceMic;
    readonly Mainframe.Selettore _volumeMic;
    readonly TextBlock _statoOrecchie;

    public FinestraImpostazioni(Ponte ponte)
    {
        _ponte = ponte;
        var imp = ponte.Impostazioni;
        Title = "Mainframe";
        Width = 1000;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;

        _esito = Mainframe.Nota("");
        _esito.Foreground = Mainframe.CianoP;

        // ---------- colonna sinistra: tu, voce, pensieri e notte ----------
        var sinistra = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };

        sinistra.Children.Add(Mainframe.Sezione("Tu", out var tu));
        _nome = Mainframe.Campo(imp.NomeUtente);
        tu.Children.Add(Mainframe.Riga("Come ti chiama", _nome));
        _carattere = new Mainframe.Selettore(ImpostazioniUtente.Caratteri, Array.IndexOf(ImpostazioniUtente.Caratteri, imp.Carattere));
        var descrizioneCarattere = Mainframe.Nota(ImpostazioniUtente.DescrizioneCarattere(imp.Carattere));
        _carattere.Cambiato += i => descrizioneCarattere.Text = ImpostazioniUtente.DescrizioneCarattere(ImpostazioniUtente.Caratteri[i]);
        tu.Children.Add(Mainframe.Riga("Carattere", _carattere));
        tu.Children.Add(descrizioneCarattere);

        var provaVoce = Mainframe.Tasto("Prova la voce");
        provaVoce.Margin = new Thickness(0);
        provaVoce.Click += async (_, _) =>
        {
            ApplicaVoce();
            _esito.Text = "Provo a leggere una frase…";
            try { await _ponte.Parla("Ciao, sono Liquidino. Mi senti bene?", _ponte.Token()); }
            catch (OperationCanceledException) { /* STOP: va bene così */ }
            _esito.Text = "Se non hai sentito niente, guarda il volume e l'uscita audio. Il testo resta comunque in chat.";
        };
        sinistra.Children.Add(Mainframe.Sezione("Voce", out var voce, provaVoce));
        _voce = new Mainframe.Selettore(["femminile", "maschile"], imp.Voce == "male" ? 1 : 0);
        voce.Children.Add(Mainframe.Riga("Voce", _voce));
        _voceWindows = new Mainframe.Interruttore(imp.VoceWindows);
        voce.Children.Add(Mainframe.Riga("Voci di Windows", _voceWindows, "Se Omni non c'è o non parla, legge con le voci italiane già installate in Windows."));
        var descrizioneVoci = Mainframe.Nota("Cerco le voci…");
        voce.Children.Add(descrizioneVoci);
        _ = Task.Run(VoceWindows.Descrizione).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully) descrizioneVoci.Text = t.Result;
        }, TaskScheduler.FromCurrentSynchronizationContext());

        sinistra.Children.Add(Mainframe.Sezione("Pensieri e notte", out var pensieri));
        _ciclo = new Mainframe.Interruttore(imp.CicloAutonomo);
        pensieri.Children.Add(Mainframe.Riga("Un pensiero ogni tanto", _ciclo, "Lascia un pensiero breve in mondo/idee. Nelle prime due settimane è un saluto."));
        _sceltaMinuti = Minuti.Contains(imp.CicloMinuti) ? Minuti : Minuti.Append(imp.CicloMinuti).Order().ToArray();
        _minuti = new Mainframe.Selettore(_sceltaMinuti.Select(TestoMinuti).ToList(), Array.IndexOf(_sceltaMinuti, imp.CicloMinuti));
        pensieri.Children.Add(Mainframe.Riga("Ogni", _minuti));
        var ore = Enumerable.Range(0, 24).Select(h => h.ToString("00") + ":00").ToList();
        _notteInizio = new Mainframe.Selettore(ore, Math.Clamp(imp.OraNotteInizio, 0, 23));
        _notteFine = new Mainframe.Selettore(ore, Math.Clamp(imp.OraNotteFine, 0, 23));
        pensieri.Children.Add(Mainframe.Riga("Dorme dalle", _notteInizio));
        pensieri.Children.Add(Mainframe.Riga("Fino alle", _notteFine, "Di notte scarica i modelli e scrive il diario della giornata. Se hai usato la chat da poco, aspetta."));

        _statoOrecchie = Mainframe.Nota("");
        var registraParola = Mainframe.Tasto("Registra la parola");
        registraParola.Click += (_, _) =>
        {
            new FinestraParola(_ponte) { Owner = this }.ShowDialog();
            _statoOrecchie.Text = StatoOrecchie();
        };
        sinistra.Children.Add(Mainframe.Sezione("Orecchie", out var orecchie, registraParola));
        _parola = Mainframe.Campo(imp.ParolaSveglia);
        orecchie.Children.Add(Mainframe.Riga("Parola per svegliarmi", _parola,
            "Con le Orecchie accese (👂) mi chiami così: «" + imp.ParolaSveglia + ", che ore sono?». Se la dici da sola, ti ascolto per 8 secondi. Registrala qualche volta con la tua voce: ti sento meglio."));
        _microfoni = Dispositivi.Ingressi().Where(n => !CavoVirtuale.ECavo(n)).ToList();
        var vociMic = new List<string> { "Predefinito di Windows" };
        vociMic.AddRange(_microfoni);
        _microfono = new Mainframe.Selettore(vociMic, Math.Max(0, _microfoni.IndexOf(imp.Microfono) + 1));
        orecchie.Children.Add(Mainframe.Riga("Microfono", _microfono, "L'uscita di VB-CABLE non compare: non è un microfono vero."));
        _ascoltaPc = new Mainframe.Interruttore(imp.AscoltaPc);
        orecchie.Children.Add(Mainframe.Riga("Sento l'audio del PC", _ascoltaPc,
            "Video, musica, giochi, app: tutto quello che suona il PC tranne la mia voce. Lo trascrivo e lo tengo in mente qualche minuto, senza salvarlo. Non sono ordini: li comandi solo tu."));
        _voceMic = new Mainframe.Interruttore(imp.VoceNelMicrofono);
        orecchie.Children.Add(Mainframe.Riga("In chiamata, la mia voce nel microfono", _voceMic,
            "Con 📞 la mia voce va anche in «CABLE Input» insieme alla tua. In Discord: Impostazioni → Voce e video → Dispositivo di ingresso → «CABLE Output». Così gli altri sentono te e me."));
        var volumeOra = Array.IndexOf(Volumi, imp.VolumeVoceMicrofono);
        _volumeMic = new Mainframe.Selettore(Volumi.Select(v => v + "%").ToList(), volumeOra < 0 ? 2 : volumeOra);
        orecchie.Children.Add(Mainframe.Riga("Quanto forte mi sentono", _volumeMic));
        var cavo = CavoVirtuale.Ingresso(Dispositivi.Uscite());
        var scaricaCavo = Mainframe.Tasto("Scarica VB-CABLE");
        scaricaCavo.ToolTip = "Apre " + CavoVirtuale.PaginaUfficiale + " nel tuo browser. Lo installi tu, poi riavvii il PC.";
        scaricaCavo.Click += (_, _) => ApriSito(CavoVirtuale.PaginaUfficiale);
        orecchie.Children.Add(Mainframe.Riga("VB-CABLE", cavo is null ? scaricaCavo : (UIElement)new TextBlock
        {
            Text = "installato (" + cavo + ")",
            FontFamily = Stile.Testo,
            FontSize = 15,
            Foreground = Mainframe.CianoP,
            TextWrapping = TextWrapping.Wrap
        }, cavo is null
            ? "Il microfono finto di VB-Audio (donationware): non posso metterlo io nel programma. Dal sito: scarica, estrai, «VBCABLE_Setup_x64» come amministratore, Install Driver, riavvia."
            : "Serve solo per la mia voce nel microfono. Le orecchie in chiamata funzionano anche senza."));
        _statoOrecchie.Text = StatoOrecchie();
        orecchie.Children.Add(_statoOrecchie);

        // ---------- colonna destra: motore, modelli, contesto, sicurezza, strumenti ----------
        var destra = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };

        var apriModelli = Mainframe.Tasto("Modelli");
        apriModelli.Margin = new Thickness(0);
        apriModelli.Click += (_, _) => ApriModelli();
        destra.Children.Add(Mainframe.Sezione("Motore", out var motore, apriModelli));
        var rigaCartella = new DockPanel();
        var sfoglia = Mainframe.Tasto("Sfoglia");
        sfoglia.Margin = new Thickness(6, 0, 0, 0);
        sfoglia.Click += (_, _) => Sfoglia();
        DockPanel.SetDock(sfoglia, Dock.Right);
        _cartella = Mainframe.Campo(imp.CartellaMotore);
        rigaCartella.Children.Add(sfoglia);
        rigaCartella.Children.Add(_cartella);
        motore.Children.Add(Mainframe.Riga("Cartella del motore", rigaCartella, "Dove stanno llama-server e la cartella «modelli». La pagina Modelli scarica lì i GGUF."));
        _porta = Mainframe.Campo(imp.Porta.ToString());
        _porta.Width = 110;
        _porta.HorizontalAlignment = HorizontalAlignment.Left;
        motore.Children.Add(Mainframe.Riga("Porta", _porta, "Solo su 127.0.0.1: il motore ascolta solo questo PC."));
        _motore = Mainframe.Nota(StatoMotore());
        motore.Children.Add(_motore);

        _contesto = Mainframe.Nota(TestoContesto());
        var misura = Mainframe.Tasto("Misura il PC");
        misura.Click += (_, _) =>
        {
            imp.ApplicaContesto(SceltaContesto.Scegli(MisuraPc.Misura()));
            _contesto.Text = TestoContesto();
            _esito.Text = imp.FraseContesto;
        };
        var provaContesto = Mainframe.Tasto("Prova il contesto sul serio");
        provaContesto.ToolTip = "Riempie davvero la memoria del motore, partendo da 128K e scendendo finché regge. Può volerci qualche minuto. Lo stesso tasto la ferma.";
        var provaInCorso = false;
        provaContesto.Click += async (_, _) =>
        {
            // Questa finestra blocca le altre: lo STOP della prova sta qui, sullo stesso tasto.
            if (provaInCorso)
            {
                _ponte.Annulla();
                _esito.Text = "Fermo la prova…";
                return;
            }
            provaInCorso = true;
            provaContesto.Content = "Ferma la prova";
            _esito.Text = "Comincio la prova di carico. Il motore si riaccende a ogni gradino.";
            try
            {
                var esito = await _ponte.ProvaContestoVera(passo => _esito.Text = passo);
                _esito.Text = esito.Frase();
                _contesto.Text = TestoContesto();
            }
            finally
            {
                provaInCorso = false;
                provaContesto.Content = "Prova il contesto sul serio";
            }
        };
        destra.Children.Add(Mainframe.Sezione("Contesto", out var contesto));
        // _contesto è già creato sopra, prima dei tasti che lo aggiornano.
        contesto.Children.Add(_contesto);
        var tastiContesto = new WrapPanel();
        tastiContesto.Children.Add(misura);
        tastiContesto.Children.Add(provaContesto);
        contesto.Children.Add(tastiContesto);

        destra.Children.Add(Mainframe.Sezione("Sicurezza", out var sicurezza));
        _statoFirewall = Mainframe.Nota("Regola del firewall: controllo…");
        var firewall = Mainframe.Tasto("Togli internet a llama-server");
        firewall.ToolTip = "Crea una regola di Windows Firewall. Windows chiede il permesso dell'amministratore una volta.";
        firewall.Click += async (_, _) =>
        {
            if (_ponte.Regista.Eseguibile is not { } exe)
            {
                _esito.Text = "Questo fallo tu: non trovo llama-server nella cartella del motore.";
                return;
            }
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
        sicurezza.Children.Add(Mainframe.Riga("Permesso del PC", new TextBlock
        {
            Text = imp.PermessoPc ? "acceso" : "spento",
            FontFamily = Stile.Testo,
            FontSize = 16,
            Foreground = imp.PermessoPc ? Mainframe.CianoP : Mainframe.RossoP
        }, "Si cambia col tasto 🖥️ vicino all'avatar. Acceso: apre programmi dopo il tuo sì, cartelle, volume e guarda lo schermo."));
        sicurezza.Children.Add(_statoFirewall);
        sicurezza.Children.Add(firewall);

        destra.Children.Add(Mainframe.Sezione("Strumenti", out var strumenti));
        var tastiStrumenti = new WrapPanel();
        var dettagli = Mainframe.Tasto("Copia dettagli errore");
        dettagli.Click += (_, _) => CopiaDettagli();
        var cartellaDati = Mainframe.Tasto("Cartella dei dati");
        cartellaDati.ToolTip = Percorsi.RadiceDati();
        cartellaDati.Click += (_, _) => Apri(Percorsi.RadiceDati());
        tastiStrumenti.Children.Add(dettagli);
        tastiStrumenti.Children.Add(cartellaDati);
        strumenti.Children.Add(tastiStrumenti);

        // ---------- insieme ----------
        var colonne = new Grid { Margin = new Thickness(20, 0, 20, 0) };
        colonne.ColumnDefinitions.Add(new ColumnDefinition());
        colonne.ColumnDefinitions.Add(new ColumnDefinition());
        colonne.Children.Add(sinistra);
        Grid.SetColumn(destra, 1);
        colonne.Children.Add(destra);

        var salva = Mainframe.Tasto("Salva");
        salva.Click += async (_, _) =>
        {
            try { await Salva(); }
            catch (Exception e) { _esito.Text = "Questo fallo tu: non sono riuscito a salvare. " + e.Message; }
        };
        var chiudi = Mainframe.Tasto("Chiudi", pericolo: true);
        chiudi.Click += (_, _) => Close();

        var piede = new DockPanel { Margin = new Thickness(20, 6, 20, 14) };
        var tasti = new StackPanel { Orientation = Orientation.Horizontal };
        tasti.Children.Add(salva);
        tasti.Children.Add(chiudi);
        DockPanel.SetDock(tasti, Dock.Right);
        piede.Children.Add(tasti);
        piede.Children.Add(_esito);

        var tutto = new DockPanel();
        var modelliAlto = Mainframe.Tasto("Modelli");
        modelliAlto.Click += (_, _) => ApriModelli();
        var testata = Mainframe.Testata("MAINFRAME", modelliAlto);
        DockPanel.SetDock(testata, Dock.Top);
        DockPanel.SetDock(piede, Dock.Bottom);
        tutto.Children.Add(testata);
        tutto.Children.Add(piede);
        tutto.Children.Add(new ScrollViewer { Content = colonne, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = Mainframe.Cornice(tutto);

        Loaded += async (_, _) =>
        {
            try { await AggiornaFirewall(); }
            catch { _statoFirewall.Text = "Regola del firewall: non riesco a leggerla."; }
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is Button or TextBox) return;
            try { DragMove(); } catch { /* già rilasciato */ }
        };
    }

    static string TestoMinuti(int minuti) => minuti < 60 ? minuti + " min" : minuti % 60 == 0 ? minuti / 60 + " h" : minuti / 60 + " h " + minuti % 60 + " min";

    string StatoMotore()
    {
        var r = _ponte.Regista.Rilevazione;
        var exe = r.Eseguibile is null ? "llama-server non trovato" : "llama-server trovato";
        var n = r.Modelli.Count;
        return exe + " · " + (n == 0 ? "nessun modello" : n == 1 ? "1 modello" : n + " modelli") + " in " + r.CartellaModelli;
    }

    void ApriModelli()
    {
        var finestra = new FinestraModelli(_ponte) { Owner = this };
        finestra.ShowDialog();
        _motore.Text = StatoMotore();
        _statoOrecchie.Text = StatoOrecchie();
    }

    string StatoOrecchie()
    {
        var imp = _ponte.Impostazioni;
        var stato = OrecchioLeggero.Stato(imp.ParolaSveglia, _ponte.Regista.Rilevazione.CartellaModelli).Messaggio;
        var adesso = _ponte.Udito.Accese
            ? "Adesso: accese" + (_ponte.Udito.NomeMicrofono is { } m ? ", microfono " + m : ", senza microfono") + (_ponte.Udito.SenteIlPc ? ", audio del PC" : "")
              + (_ponte.Udito.InChiamata ? ", in chiamata su " + _ponte.Udito.NomeChiamata + (_ponte.Udito.Cavo is null ? "" : " con la voce nel microfono") : "") + "."
            : "Adesso: spente.";
        return adesso + " " + stato;
    }

    void ApriSito(string indirizzo)
    {
        try { using (Process.Start(new ProcessStartInfo(indirizzo) { UseShellExecute = true })) { } }
        catch (Exception e) { _esito.Text = "Questo fallo tu: apri " + indirizzo + " nel browser. " + e.Message; }
    }

    void Apri(string cartella)
    {
        try
        {
            Directory.CreateDirectory(cartella);
            using (Process.Start(new ProcessStartInfo("explorer.exe", "\"" + cartella + "\"") { UseShellExecute = true })) { }
        }
        catch (Exception e) { _esito.Text = "Questo fallo tu: non riesco ad aprire " + cartella + ". " + e.Message; }
    }

    string TestoContesto()
    {
        var imp = _ponte.Impostazioni;
        return "Mani " + imp.ContestoMani / 1024 + "K, Omni " + imp.ContestoOmni / 1024 + "K, d1 " + imp.ContestoD1 / 1024 + "K."
            + (string.IsNullOrWhiteSpace(imp.FraseContesto) ? "" : " " + imp.FraseContesto);
    }

    async Task AggiornaFirewall()
    {
        if (_ponte.Regista.Eseguibile is not { } exe)
        {
            _statoFirewall.Text = "Regola del firewall: llama-server non è stato trovato.";
            return;
        }
        var c = await Firewall.Controlla(exe);
        _statoFirewall.Text = c switch
        {
            true => "Regola del firewall: c'è, llama-server non va in internet.",
            false => "Regola del firewall: non c'è. llama-server ascolta solo su 127.0.0.1, ma la regola lo chiude anche verso fuori.",
            _ => "Regola del firewall: non riesco a leggerla."
        };
    }

    void ApplicaVoce()
    {
        var imp = _ponte.Impostazioni;
        imp.Voce = _voce.Indice == 1 ? "male" : "female";
        imp.VoceWindows = _voceWindows.Acceso;
    }

    void CopiaDettagli()
    {
        var sistema = Environment.OSVersion.VersionString + (Environment.Is64BitOperatingSystem ? ", 64 bit" : "");
        var testo = DettagliErrore.Componi(Ponte.Versione, sistema, _ponte.Regista.Riassunto(), _ponte.Registro);
        try
        {
            System.Windows.Clipboard.SetText(testo);
            _esito.Text = "Dettagli copiati. Prima di incollarli guarda che non ci sia niente di tuo: la chat e la memoria non ci sono.";
        }
        catch (Exception e)
        {
            _esito.Text = "Questo fallo tu: gli appunti sono occupati da un altro programma. Riprova fra un attimo. (" + e.Message + ")";
        }
    }

    void Sfoglia()
    {
        var dialogo = new OpenFolderDialog { Title = "Cartella di llama.cpp", InitialDirectory = _cartella.Text };
        if (dialogo.ShowDialog(this) == true) _cartella.Text = dialogo.FolderName;
    }

    async Task Salva()
    {
        var imp = _ponte.Impostazioni;
        var nome = _nome.Text.Trim();
        if (nome.Length > 0) imp.NomeUtente = nome.Length > 40 ? nome[..40] : nome;
        imp.Carattere = _carattere.Valore;
        imp.CartellaMotore = _cartella.Text.Trim();
        if (int.TryParse(_porta.Text.Trim(), out var porta) && porta is > 0 and < 65536) imp.Porta = porta;
        imp.CicloAutonomo = _ciclo.Acceso;
        imp.CicloMinuti = Math.Clamp(_sceltaMinuti[_minuti.Indice], 5, 24 * 60);
        imp.OraNotteInizio = _notteInizio.Indice;
        imp.OraNotteFine = _notteFine.Indice;
        ApplicaVoce();
        var parola = _parola.Text.Trim();
        if (parola.Length > 0) imp.ParolaSveglia = parola.Length > 40 ? parola[..40] : parola;
        var microfono = _microfono.Indice == 0 ? "" : _microfoni[_microfono.Indice - 1];
        var riaccendi = _ponte.Udito.Accese && (microfono != imp.Microfono || _ascoltaPc.Acceso != imp.AscoltaPc);
        imp.Microfono = microfono;
        imp.AscoltaPc = _ascoltaPc.Acceso;
        imp.VoceNelMicrofono = _voceMic.Acceso;
        imp.VolumeVoceMicrofono = Volumi[_volumeMic.Indice];
        _ponte.Udito.RicaricaParola();
        if (_ponte.Udito.Cavo is { } cavo) cavo.Guadagno = imp.VolumeVoceMicrofono / 100f;
        if (riaccendi)
        {
            _esito.Text = "Riaccendo le Orecchie col microfono nuovo…";
            _ponte.Udito.Spegni();
            _ponte.Annuncia(await _ponte.Udito.Accendi());
        }
        _esito.Text = "Salvo e riaccendo il motore…";
        await _ponte.RicaricaMotore();
        Close();
    }
}
