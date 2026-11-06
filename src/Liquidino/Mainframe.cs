using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Liquidino;

/// <summary>
/// I pezzi delle finestre di impostazioni, nello stile DaProd della chat: barra crema in alto, fondo notte,
/// titoli oro, interruttori a pillola e scelte a gettoni. Compatti: una scheda sta in una schermata, senza scorrere.
/// </summary>
public static class Mainframe
{
    public static readonly SolidColorBrush LineaP = Stile.Pennello("#2a2632");
    public static readonly SolidColorBrush RossoP = Stile.RossoP;
    public static readonly SolidColorBrush RossoScuroP = Stile.Pennello("#3a1822");
    public static readonly SolidColorBrush CianoP = Stile.CianoP;
    public static readonly SolidColorBrush CianoScuroP = Stile.Pennello("#0f3a40");
    public static readonly SolidColorBrush BandaP = Stile.Carta;
    public static readonly SolidColorBrush SpentoP = Stile.Pennello("#26232e");

    /// <summary>Un gruppo: titolo oro piccolo, sottile linea sotto, righe compatte.</summary>
    public static Border Sezione(string titolo, out StackPanel righe, UIElement? aDestra = null)
    {
        righe = new StackPanel { Margin = new Thickness(12, 2, 12, 8) };
        var testa = new DockPanel { Margin = new Thickness(12, 8, 10, 4), LastChildFill = true };
        if (aDestra is not null)
        {
            DockPanel.SetDock(aDestra, Dock.Right);
            testa.Children.Add(aDestra);
        }
        testa.Children.Add(new TextBlock
        {
            Text = titolo.ToUpperInvariant(),
            FontFamily = Stile.Marchio,
            FontSize = 11,
            Foreground = Stile.OroP,
            VerticalAlignment = VerticalAlignment.Center
        });
        var pila = new StackPanel();
        pila.Children.Add(testa);
        pila.Children.Add(new Border { Height = 1, Background = LineaP, Margin = new Thickness(12, 0, 12, 4) });
        pila.Children.Add(righe);
        return new Border
        {
            Background = BandaP,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 8),
            Child = pila
        };
    }

    /// <summary>Una riga: il nome a sinistra, il valore a destra, e sotto, se c'è, una spiegazione corta.</summary>
    public static FrameworkElement Riga(string nome, UIElement valore, string? spiegazione = null)
    {
        var griglia = new Grid { Margin = new Thickness(0, 2, 0, 2), MinHeight = 26 };
        griglia.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.75, GridUnitType.Star) });
        griglia.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
        griglia.Children.Add(new TextBlock
        {
            Text = nome,
            FontFamily = Stile.Testo,
            FontSize = 15,
            Foreground = Stile.Inchiostro,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 10, 0)
        });
        if (valore is FrameworkElement fe) fe.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(valore, 1);
        griglia.Children.Add(valore);
        if (spiegazione is null) return griglia;
        var pila = new StackPanel();
        pila.Children.Add(griglia);
        pila.Children.Add(Nota(spiegazione));
        return pila;
    }

    /// <summary>Riga piccola e grigia, per spiegare in parole semplici.</summary>
    public static TextBlock Nota(string testo) => new()
    {
        Text = testo,
        FontFamily = Stile.Testo,
        FontSize = 13,
        Foreground = Stile.Inchiostro2,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 4)
    };

    /// <summary>Interruttore a pillola: oro e pallino a destra quando è acceso. Un clic lo gira.</summary>
    public sealed class Interruttore : Border
    {
        readonly Border _pallino;
        public bool Acceso { get; private set; }
        public event Action<bool>? Cambiato;

        public Interruttore(bool acceso)
        {
            Width = 46;
            Height = 22;
            CornerRadius = new CornerRadius(11);
            BorderThickness = new Thickness(1);
            Cursor = Cursors.Hand;
            HorizontalAlignment = HorizontalAlignment.Left;
            _pallino = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Margin = new Thickness(2, 0, 2, 0) };
            var dentro = new Grid { ClipToBounds = false };
            dentro.Children.Add(_pallino);
            Child = dentro;
            MouseLeftButtonUp += (_, _) => Imposta(!Acceso, true);
            Imposta(acceso, false);
        }

        public void Imposta(bool acceso, bool avvisa)
        {
            var cambia = Acceso != acceso;
            Acceso = acceso;
            Background = acceso ? Stile.Pennello("#3a3010") : SpentoP;
            BorderBrush = acceso ? Stile.OroP : Stile.Pennello("#3a3648");
            _pallino.Background = acceso ? Stile.OroP : Stile.Inchiostro3;
            _pallino.HorizontalAlignment = acceso ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            ToolTip = acceso ? "Acceso. Un clic per spegnere." : "Spento. Un clic per accendere.";
            if (avvisa && cambia) Cambiato?.Invoke(acceso);
        }
    }

    /// <summary>Le scelte come gettoni uno accanto all'altro: quello scelto è oro.</summary>
    public sealed class Selettore : WrapPanel
    {
        readonly string[] _voci;
        readonly List<Border> _gettoni = [];
        public int Indice { get; private set; } = -1;
        public string Valore => _voci[Math.Max(0, Indice)];
        public event Action<int>? Cambiato;

        public Selettore(IReadOnlyList<string> voci, int indice)
        {
            _voci = voci.Count == 0 ? ["—"] : voci.ToArray();
            for (var i = 0; i < _voci.Length; i++)
            {
                var n = i;
                var g = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 1, 8, 2),
                    Margin = new Thickness(0, 2, 4, 2),
                    Cursor = Cursors.Hand,
                    Child = new TextBlock { Text = _voci[i], FontFamily = Stile.Testo, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 240 }
                };
                g.MouseLeftButtonUp += (_, _) => Imposta(n, true);
                _gettoni.Add(g);
                Children.Add(g);
            }
            Imposta(Math.Clamp(indice, 0, _voci.Length - 1), false);
        }

        public void Imposta(int indice, bool avvisa)
        {
            indice = Math.Clamp(indice, 0, _voci.Length - 1);
            var cambia = indice != Indice;
            Indice = indice;
            for (var i = 0; i < _gettoni.Count; i++)
            {
                var scelto = i == indice;
                _gettoni[i].Background = scelto ? Stile.Pennello("#3a3010") : SpentoP;
                _gettoni[i].BorderBrush = scelto ? Stile.OroP : Stile.Pennello("#3a3648");
                ((TextBlock)_gettoni[i].Child).Foreground = scelto ? Stile.OroP : Stile.Inchiostro2;
            }
            if (avvisa && cambia) Cambiato?.Invoke(indice);
        }
    }

    /// <summary>Tasto piccolo, come quelli della chat.</summary>
    public static Button Tasto(string testo, bool pericolo = false, bool principale = false)
    {
        var b = Stile.Bottone(testo, principale: principale, pericolo: pericolo);
        b.FontSize = 14;
        b.Padding = new Thickness(10, 3, 10, 3);
        b.Margin = new Thickness(0, 2, 6, 2);
        return b;
    }

    /// <summary>Campo di testo scuro.</summary>
    public static TextBox Campo(string testo)
    {
        var c = Stile.Campo();
        c.Text = testo;
        c.Foreground = Stile.Inchiostro;
        c.Background = SpentoP;
        c.BorderBrush = Stile.Pennello("#3a3648");
        c.FontSize = 14;
        c.Padding = new Thickness(6, 2, 6, 2);
        return c;
    }

    /// <summary>La barra DaProd in alto, come nella chat: «DaP LIQUIDINO», il nome della finestra, la versione e i tasti.</summary>
    public static DockPanel Testata(string titolo, params Button[] tasti)
    {
        var barra = new DockPanel { Height = 46, Background = Stile.BarraP, LastChildFill = false };
        var nome = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        nome.Children.Add(Stile.Titolo("DaP", chiaro: true));
        var marca = Stile.Titolo("  LIQUIDINO", chiaro: true);
        marca.FontSize = 16;
        nome.Children.Add(marca);
        nome.Children.Add(new TextBlock { Text = "   " + titolo.ToLowerInvariant(), FontFamily = Stile.Testo, FontSize = 16, Foreground = Stile.InchiostroBarraP, VerticalAlignment = VerticalAlignment.Center });
        nome.Children.Add(new TextBlock { Text = "  v" + Ponte.Versione, FontFamily = Stile.Testo, FontSize = 13, Foreground = Stile.Pennello("#7a7268"), VerticalAlignment = VerticalAlignment.Center });
        DockPanel.SetDock(nome, Dock.Left);
        barra.Children.Add(nome);
        var destra = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        foreach (var t in tasti) destra.Children.Add(t);
        DockPanel.SetDock(destra, Dock.Right);
        barra.Children.Add(destra);
        return barra;
    }

    /// <summary>La cornice della finestra: fondo notte, bordo sottile, angoli smussati.</summary>
    public static Border Cornice(UIElement contenuto) => new()
    {
        Background = Stile.Fondo,
        BorderBrush = Stile.Pennello("#3a3648"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        ClipToBounds = true,
        Child = contenuto
    };
}
