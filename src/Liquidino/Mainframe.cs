using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Liquidino;

/// <summary>
/// I pezzi del Mainframe di Liquidino: bande scure sotto una linea teal, nomi rossi a sinistra, valori ciano a destra,
/// interruttori OFF/ON e selettori con le frecce. Scritti per Liquidino: l'idea dell'aspetto viene dalle schermate
/// di gioco cyberpunk, il codice no.
/// </summary>
public static class Mainframe
{
    public const string Linea = "#1fd6c6";
    public const string Rosso = "#ff4b5c";
    public const string RossoScuro = "#7a1f2a";
    public const string Ciano = "#5ff4ff";
    public const string CianoScuro = "#0f3a40";
    public const string Banda = "#110f17";
    public const string Spento = "#2a2632";

    public static readonly SolidColorBrush LineaP = Stile.Pennello(Linea);
    public static readonly SolidColorBrush RossoP = Stile.Pennello(Rosso);
    public static readonly SolidColorBrush RossoScuroP = Stile.Pennello(RossoScuro);
    public static readonly SolidColorBrush CianoP = Stile.Pennello(Ciano);
    public static readonly SolidColorBrush CianoScuroP = Stile.Pennello(CianoScuro);
    public static readonly SolidColorBrush BandaP = Stile.Pennello(Banda);
    public static readonly SolidColorBrush SpentoP = Stile.Pennello(Spento);

    /// <summary>Una sezione: titolo con la barretta rossa, linea teal sopra, righe sotto.</summary>
    public static Border Sezione(string titolo, out StackPanel righe, UIElement? aDestra = null)
    {
        righe = new StackPanel { Margin = new Thickness(14, 4, 14, 10) };
        var testa = new DockPanel { Margin = new Thickness(0, 8, 14, 6), LastChildFill = true };
        var barra = new Border { Width = 4, Height = 16, Background = RossoP, Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(barra, Dock.Left);
        testa.Children.Add(barra);
        if (aDestra is not null)
        {
            DockPanel.SetDock(aDestra, Dock.Right);
            testa.Children.Add(aDestra);
        }
        testa.Children.Add(new TextBlock
        {
            Text = titolo.ToUpperInvariant(),
            FontFamily = Stile.Marchio,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = Stile.Inchiostro,
            VerticalAlignment = VerticalAlignment.Center
        });
        var pila = new StackPanel();
        pila.Children.Add(testa);
        pila.Children.Add(righe);
        return new Border
        {
            Background = BandaP,
            BorderBrush = LineaP,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(0, 0, 0, 12),
            Child = pila
        };
    }

    /// <summary>Una riga: il nome in rosso a sinistra, il valore a destra, e sotto, se c'è, la spiegazione in grigio.</summary>
    public static FrameworkElement Riga(string nome, UIElement valore, string? spiegazione = null)
    {
        var griglia = new Grid { Margin = new Thickness(0, 3, 0, 3), MinHeight = 30 };
        griglia.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        griglia.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
        var etichetta = new TextBlock
        {
            Text = nome,
            FontFamily = Stile.Testo,
            FontSize = 16,
            Foreground = RossoP,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 10, 0)
        };
        griglia.Children.Add(etichetta);
        if (valore is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(valore, 1);
        griglia.Children.Add(valore);
        if (spiegazione is null) return griglia;
        var pila = new StackPanel();
        pila.Children.Add(griglia);
        pila.Children.Add(Nota(spiegazione));
        return pila;
    }

    /// <summary>Riga di testo piccola e grigia, per spiegare in parole semplici.</summary>
    public static TextBlock Nota(string testo) => new()
    {
        Text = testo,
        FontFamily = Stile.Testo,
        FontSize = 14,
        Foreground = Stile.Inchiostro2,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 6)
    };

    /// <summary>OFF e ON uno accanto all'altro: quello scelto si accende (OFF rosso, ON ciano).</summary>
    public sealed class Interruttore : StackPanel
    {
        readonly Button _off;
        readonly Button _on;
        public bool Acceso { get; private set; }
        public event Action<bool>? Cambiato;

        public Interruttore(bool acceso)
        {
            Orientation = Orientation.Horizontal;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            _off = Pezzo("OFF");
            _on = Pezzo("ON");
            _off.Click += (_, _) => Imposta(false, true);
            _on.Click += (_, _) => Imposta(true, true);
            Children.Add(_off);
            Children.Add(_on);
            Imposta(acceso, false);
        }

        public void Imposta(bool acceso, bool avvisa)
        {
            var cambia = Acceso != acceso;
            Acceso = acceso;
            _off.Background = acceso ? SpentoP : RossoScuroP;
            _off.Foreground = acceso ? Stile.Inchiostro3 : RossoP;
            _off.BorderBrush = acceso ? SpentoP : RossoP;
            _on.Background = acceso ? CianoP : SpentoP;
            _on.Foreground = acceso ? Stile.Fondo : Stile.Inchiostro3;
            _on.BorderBrush = acceso ? CianoP : SpentoP;
            if (avvisa && cambia) Cambiato?.Invoke(acceso);
        }

        static Button Pezzo(string testo)
        {
            var b = Stile.Bottone(testo);
            b.Width = 92;
            b.Margin = new Thickness(0, 0, 4, 0);
            b.FontFamily = Stile.Marchio;
            b.FontSize = 12;
            return b;
        }
    }

    /// <summary>◁ valore ▷, con sotto le tacche: quella rossa dice dove sei fra le scelte.</summary>
    public sealed class Selettore : Grid
    {
        readonly string[] _voci;
        readonly TextBlock _valore;
        readonly StackPanel _tacche = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
        public int Indice { get; private set; }
        public string Valore => _voci[Indice];
        public event Action<int>? Cambiato;

        public Selettore(IReadOnlyList<string> voci, int indice)
        {
            _voci = voci.Count == 0 ? ["—"] : voci.ToArray();
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var indietro = Freccia("◁");
            var avanti = Freccia("▷");
            indietro.Click += (_, _) => Imposta(Indice - 1, true);
            avanti.Click += (_, _) => Imposta(Indice + 1, true);
            _valore = new TextBlock
            {
                FontFamily = Stile.Testo,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = CianoP,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var centro = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            centro.Children.Add(_valore);
            centro.Children.Add(_tacche);
            SetColumn(centro, 1);
            SetColumn(avanti, 2);
            Children.Add(indietro);
            Children.Add(centro);
            Children.Add(avanti);
            Background = SpentoP;
            Imposta(indice, false);
        }

        public void Imposta(int indice, bool avvisa)
        {
            indice = Math.Clamp(indice, 0, _voci.Length - 1);
            var cambia = indice != Indice;
            Indice = indice;
            _valore.Text = _voci[indice];
            _tacche.Children.Clear();
            for (var i = 0; i < _voci.Length && _voci.Length <= 12; i++)
                _tacche.Children.Add(new Border { Width = 10, Height = 2, Margin = new Thickness(2, 0, 2, 0), Background = i == indice ? RossoP : Stile.Inchiostro3 });
            if (avvisa && cambia) Cambiato?.Invoke(indice);
        }

        static Button Freccia(string segno)
        {
            var b = Stile.Bottone(segno);
            b.Background = Brushes.Transparent;
            b.BorderThickness = new Thickness(0);
            b.Foreground = CianoP;
            b.Padding = new Thickness(10, 2, 10, 2);
            b.FontFamily = new FontFamily("Segoe UI Symbol, Segoe UI");
            return b;
        }
    }

    /// <summary>Tasto piatto del Mainframe, bordo teal.</summary>
    public static Button Tasto(string testo, bool pericolo = false)
    {
        var b = Stile.Bottone(testo, pericolo: pericolo);
        b.Background = Stile.Pennello(pericolo ? "#2a1218" : "#14202a");
        b.BorderBrush = pericolo ? RossoP : LineaP;
        b.Foreground = pericolo ? RossoP : CianoP;
        b.FontSize = 15;
        b.Margin = new Thickness(0, 2, 8, 2);
        return b;
    }

    /// <summary>Campo di testo del Mainframe: scuro, testo ciano.</summary>
    public static TextBox Campo(string testo)
    {
        var c = Stile.Campo();
        c.Text = testo;
        c.Foreground = CianoP;
        c.Background = SpentoP;
        c.BorderBrush = SpentoP;
        c.FontSize = 15;
        c.Padding = new Thickness(6, 3, 6, 3);
        return c;
    }

    /// <summary>La barra in alto delle finestre del Mainframe: titolo, versione e tasti.</summary>
    public static DockPanel Testata(string titolo, params Button[] tasti)
    {
        var barra = new DockPanel { Margin = new Thickness(20, 14, 16, 10), LastChildFill = false };
        var nome = new StackPanel { Orientation = Orientation.Horizontal };
        nome.Children.Add(new TextBlock { Text = titolo, FontFamily = Stile.Marchio, FontSize = 22, FontWeight = FontWeights.Bold, Foreground = RossoP, VerticalAlignment = VerticalAlignment.Center });
        nome.Children.Add(new TextBlock { Text = "  v" + Ponte.Versione, FontFamily = Stile.Testo, FontSize = 14, Foreground = Stile.Inchiostro3, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 3) });
        DockPanel.SetDock(nome, Dock.Left);
        barra.Children.Add(nome);
        var destra = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var t in tasti) destra.Children.Add(t);
        DockPanel.SetDock(destra, Dock.Right);
        barra.Children.Add(destra);
        return barra;
    }

    /// <summary>La cornice di una finestra del Mainframe: fondo notte, bordo sottile, angoli appena smussati.</summary>
    public static Border Cornice(UIElement contenuto) => new()
    {
        Background = Stile.Fondo,
        BorderBrush = RossoScuroP,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Child = contenuto
    };
}
