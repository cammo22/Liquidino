using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Liquidino;

/// <summary>Colori e caratteri DaProd: viola notte, oro, magenta. Barra chiara, resto scuro.</summary>
public static class Stile
{
    public const string Notte = "#09080d";
    public const string Pannello = "#14131a";
    public const string Pannello2 = "#1b1a22";
    public const string Oro = "#ffd54a";
    public const string OroPremuto = "#ffab00";
    public const string Magenta = "#ff3df2";
    public const string Ciano = "#35e8ff";
    public const string Verde = "#5dffb4";
    public const string Rosso = "#ff4d6d";
    public const string Ambra = "#ffb23d";
    public const string TestoChiaro = "#ebe7f4";
    public const string Secondario = "#a19db0";
    public const string Terziario = "#6f6b7d";
    public const string Barra = "#e7e0d2";
    public const string InchiostroBarra = "#1a1520";

    public static FontFamily Marchio => _marchio ??= Carica("font/Orbitron-Regular.ttf#Orbitron");
    public static FontFamily Testo => _testo ??= Carica("font/Rajdhani-Regular.ttf#Rajdhani");
    public static FontFamily Display => _display ??= Carica("font/DSEG7Classic-Bold.ttf#DSEG7 Classic");

    static FontFamily? _marchio, _testo, _display;

    static FontFamily Carica(string relativo)
    {
        try { return new FontFamily(new Uri("pack://application:,,,/"), "./" + relativo); }
        catch { return new FontFamily("Segoe UI"); }
    }

    public static SolidColorBrush Pennello(string esadecimale)
    {
        var colore = (Color)ColorConverter.ConvertFromString(esadecimale);
        var pennello = new SolidColorBrush(colore);
        if (pennello.CanFreeze) pennello.Freeze();
        return pennello;
    }

    public static readonly SolidColorBrush Fondo = Pennello(Notte);
    public static readonly SolidColorBrush Carta = Pennello(Pannello);
    public static readonly SolidColorBrush Carta2 = Pennello(Pannello2);
    public static readonly SolidColorBrush OroP = Pennello(Oro);
    public static readonly SolidColorBrush MagentaP = Pennello(Magenta);
    public static readonly SolidColorBrush CianoP = Pennello(Ciano);
    public static readonly SolidColorBrush VerdeP = Pennello(Verde);
    public static readonly SolidColorBrush RossoP = Pennello(Rosso);
    public static readonly SolidColorBrush AmbraP = Pennello(Ambra);
    public static readonly SolidColorBrush Inchiostro = Pennello(TestoChiaro);
    public static readonly SolidColorBrush Inchiostro2 = Pennello(Secondario);
    public static readonly SolidColorBrush Inchiostro3 = Pennello(Terziario);
    public static readonly SolidColorBrush BarraP = Pennello(Barra);
    public static readonly SolidColorBrush InchiostroBarraP = Pennello(InchiostroBarra);

    public static Button Bottone(string testo, bool principale = false, bool pericolo = false)
    {
        var sfondo = pericolo ? "#3a1822" : principale ? "#3a3010" : "#1b1a22";
        var bordo = pericolo ? Rosso : principale ? Oro : "#3a3648";
        var inchiostro = pericolo ? Rosso : principale ? Oro : TestoChiaro;
        var b = new Button
        {
            Content = testo,
            FontFamily = Testo,
            FontSize = 16,
            FontWeight = principale ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = Pennello(inchiostro),
            Background = Pennello(sfondo),
            BorderBrush = Pennello(bordo),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 5, 12, 5),
            Margin = new Thickness(4, 0, 4, 0),
            Cursor = Cursors.Hand,
            Template = Piatto()
        };
        return b;
    }

    /// <summary>Simbolo al posto della parola. Il tooltip dice che cosa fa.</summary>
    public static Button Icona(string simbolo, string tooltip, bool principale = false, bool pericolo = false)
    {
        var b = Bottone(simbolo, principale, pericolo);
        b.FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Segoe UI");
        b.FontSize = 18;
        b.Padding = new Thickness(6, 2, 6, 2);
        b.MinWidth = 42;
        b.Margin = new Thickness(3, 2, 3, 2);
        b.ToolTip = tooltip;
        return b;
    }

    public static TextBlock Titolo(string testo, bool chiaro = false) => new()
    {
        Text = testo,
        FontFamily = Marchio,
        FontSize = 14,
        Foreground = chiaro ? InchiostroBarraP : OroP,
        VerticalAlignment = VerticalAlignment.Center
    };

    public static TextBlock Riga(string testo, bool secondario = false) => new()
    {
        Text = testo,
        FontFamily = Testo,
        FontSize = 16,
        Foreground = secondario ? Inchiostro2 : Inchiostro,
        TextWrapping = TextWrapping.Wrap
    };

    public static TextBox Campo(string suggerimento = "")
    {
        var box = new TextBox
        {
            FontFamily = Testo,
            FontSize = 18,
            Background = Carta,
            Foreground = Inchiostro,
            BorderBrush = Pennello("#3a3648"),
            CaretBrush = OroP,
            Padding = new Thickness(8, 6, 8, 6),
            BorderThickness = new Thickness(1)
        };
        if (suggerimento.Length > 0)
        {
            box.Tag = suggerimento;
            box.Text = suggerimento;
            box.Foreground = Inchiostro3;
            box.GotFocus += (_, _) =>
            {
                if (Equals(box.Text, box.Tag as string)) { box.Text = ""; box.Foreground = Inchiostro; }
            };
            box.LostFocus += (_, _) =>
            {
                if (box.Text.Length == 0) { box.Text = suggerimento; box.Foreground = Inchiostro3; }
            };
        }
        return box;
    }

    public static string TestoCampo(TextBox box)
    {
        var suggerimento = box.Tag as string;
        if (suggerimento is not null && box.Text == suggerimento) return "";
        return box.Text.Trim();
    }

    static ControlTemplate Piatto()
    {
        var t = new ControlTemplate(typeof(Button));
        var bordo = new FrameworkElementFactory(typeof(Border));
        bordo.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        bordo.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        bordo.SetBinding(Border.BorderBrushProperty, new Binding(nameof(Control.BorderBrush)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        bordo.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(Control.BorderThickness)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        bordo.SetBinding(Border.PaddingProperty, new Binding(nameof(Control.Padding)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        var contenuto = new FrameworkElementFactory(typeof(ContentPresenter));
        contenuto.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contenuto.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        bordo.AppendChild(contenuto);
        t.VisualTree = bordo;
        return t;
    }
}
