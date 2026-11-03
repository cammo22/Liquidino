using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Liquidino;

/// <summary>Le novità, dal CHANGELOG che viaggia con il programma. Si chiude da sola.</summary>
public sealed class FinestraNovita : Window
{
    readonly DispatcherTimer _chiusura;

    public FinestraNovita()
    {
        Title = "Novità";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        ShowActivated = true;
        ResizeMode = ResizeMode.NoResize;

        var testo = "Liquidino è qui.";
        var percorso = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
        if (File.Exists(percorso)) testo = File.ReadAllText(percorso);

        var corpo = new TextBlock
        {
            Text = testo,
            FontFamily = Stile.Testo,
            FontSize = 16,
            Foreground = Stile.Inchiostro,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(18)
        };
        Content = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(235, 16, 14, 24)),
            BorderBrush = Stile.OroP,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Child = corpo
        };
        _chiusura = new DispatcherTimer { Interval = TimeSpan.FromSeconds(9) };
        _chiusura.Tick += (_, _) => Close();
        MouseEnter += (_, _) => _chiusura.Stop();
        MouseLeave += (_, _) => _chiusura.Start();
        MouseLeftButtonUp += (_, _) => Close();
        Loaded += (_, _) => _chiusura.Start();
        Closed += (_, _) => _chiusura.Stop();
    }
}

