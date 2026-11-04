using System.Windows;
using System.Windows.Controls;
using Liquidino.Nucleo;
using Microsoft.Win32;

namespace Liquidino;

/// <summary>Cartella di llama.cpp, ciclo orario, carattere e voce. Tutto resta su questo PC.</summary>
public sealed class FinestraImpostazioni : Window
{
    readonly Ponte _ponte;
    readonly TextBox _cartella;
    readonly TextBox _porta;
    readonly TextBox _minuti;
    readonly CheckBox _ciclo;
    readonly ComboBox _carattere;
    readonly ComboBox _voce;
    readonly WrapPanel _strumenti;
    readonly TextBlock _esito;

    public FinestraImpostazioni(Ponte ponte)
    {
        _ponte = ponte;
        var imp = ponte.Impostazioni;
        Title = "Impostazioni";
        Width = 560;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;

        var pila = new StackPanel { Margin = new Thickness(24) };
        pila.Children.Add(new TextBlock { Text = "IMPOSTAZIONI", FontFamily = Stile.Marchio, FontSize = 16, Foreground = Stile.OroP, Margin = new Thickness(0, 0, 0, 8) });
        pila.Children.Add(Etichetta("Cartella del motore (llama.cpp e i GGUF)"));
        var riga = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var sfoglia = Stile.Bottone("Sfoglia");
        sfoglia.Click += (_, _) => Sfoglia();
        DockPanel.SetDock(sfoglia, Dock.Right);
        _cartella = Stile.Campo();
        _cartella.Text = imp.CartellaMotore;
        _cartella.Foreground = Stile.Inchiostro;
        riga.Children.Add(sfoglia);
        riga.Children.Add(_cartella);
        pila.Children.Add(riga);

        pila.Children.Add(Etichetta("Porta locale (solo 127.0.0.1)"));
        _porta = Stile.Campo();
        _porta.Text = imp.Porta.ToString();
        _porta.Foreground = Stile.Inchiostro;
        _porta.Width = 120;
        _porta.HorizontalAlignment = HorizontalAlignment.Left;
        pila.Children.Add(_porta);

        _ciclo = new CheckBox
        {
            Content = "Un pensiero ogni tanto, nella sua cartella",
            IsChecked = imp.CicloAutonomo,
            Foreground = Stile.Inchiostro,
            FontFamily = Stile.Testo,
            FontSize = 16,
            Margin = new Thickness(0, 12, 0, 4)
        };
        pila.Children.Add(_ciclo);
        pila.Children.Add(Etichetta("Minuti fra un pensiero e l'altro"));
        _minuti = Stile.Campo();
        _minuti.Text = imp.CicloMinuti.ToString();
        _minuti.Foreground = Stile.Inchiostro;
        _minuti.Width = 120;
        _minuti.HorizontalAlignment = HorizontalAlignment.Left;
        pila.Children.Add(_minuti);

        pila.Children.Add(Etichetta("Carattere"));
        _carattere = new ComboBox { FontFamily = Stile.Testo, FontSize = 16, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var c in ImpostazioniUtente.Caratteri) _carattere.Items.Add(c);
        _carattere.SelectedItem = imp.Carattere;
        pila.Children.Add(_carattere);

        pila.Children.Add(Etichetta("Voce"));
        _voce = new ComboBox { FontFamily = Stile.Testo, FontSize = 16 };
        _voce.Items.Add("femminile");
        _voce.Items.Add("maschile");
        _voce.SelectedIndex = imp.Voce == "male" ? 1 : 0;
        pila.Children.Add(_voce);

        var misura = Stile.Bottone("Misura di nuovo il PC");
        misura.HorizontalAlignment = HorizontalAlignment.Left;
        misura.Margin = new Thickness(0, 16, 0, 0);
        misura.Click += (_, _) =>
        {
            imp.ApplicaContesto(SceltaContesto.Scegli(MisuraPc.Misura()));
            System.Windows.MessageBox.Show(imp.FraseContesto, "Liquidino");
        };
        pila.Children.Add(misura);
        pila.Children.Add(Stile.Riga("Contesto attuale: mani " + imp.ContestoMani + ", Omni " + imp.ContestoOmni + ", d1 " + imp.ContestoD1 + ".", secondario: true));

        pila.Children.Add(Etichetta("Strumenti"));
        _strumenti = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        var dettagli = Stile.Bottone("Copia dettagli errore");
        dettagli.Margin = new Thickness(0, 4, 8, 4);
        dettagli.Click += (_, _) => CopiaDettagli();
        _strumenti.Children.Add(dettagli);
        pila.Children.Add(_strumenti);
        _esito = Stile.Riga("", secondario: true);
        pila.Children.Add(_esito);

        var tasti = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
        var salva = Stile.Bottone("Salva", principale: true);
        salva.Click += async (_, _) =>
        {
            try { await Salva(); }
            catch (Exception e) { System.Windows.MessageBox.Show("Questo fallo tu: non sono riuscito a salvare. " + e.Message, "Liquidino"); }
        };
        var chiudi = Stile.Bottone("Chiudi");
        chiudi.Click += (_, _) => Close();
        tasti.Children.Add(salva);
        tasti.Children.Add(chiudi);
        pila.Children.Add(tasti);

        Content = new Border
        {
            Background = Stile.Fondo,
            BorderBrush = Stile.Pennello("#33ffffff"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Child = new ScrollViewer { Content = pila, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is Button or TextBox or ComboBox or CheckBox) return;
            try { DragMove(); } catch { /* già rilasciato */ }
        };
    }

    static TextBlock Etichetta(string testo) => new()
    {
        Text = testo,
        FontFamily = Stile.Marchio,
        FontSize = 12,
        Foreground = Stile.CianoP,
        Margin = new Thickness(0, 10, 0, 4)
    };

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
        imp.CartellaMotore = _cartella.Text.Trim();
        if (int.TryParse(_porta.Text.Trim(), out var porta) && porta is > 0 and < 65536) imp.Porta = porta;
        imp.CicloAutonomo = _ciclo.IsChecked == true;
        if (int.TryParse(_minuti.Text.Trim(), out var minuti)) imp.CicloMinuti = Math.Clamp(minuti, 5, 24 * 60);
        if (_carattere.SelectedItem is string carattere) imp.Carattere = carattere;
        imp.Voce = _voce.SelectedIndex == 1 ? "male" : "female";
        await _ponte.RicaricaMotore();
        Close();
    }
}
