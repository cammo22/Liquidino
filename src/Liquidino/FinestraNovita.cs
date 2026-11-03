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

/// <summary>L'unico contatto con la rete: lo chiedi tu, dal tasto in alto, e non porta via nessun dato tuo.</summary>
public static class Aggiornamenti
{
    public static async Task<string?> Cerca(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Liquidino");
        var json = await http.GetStringAsync("https://api.github.com/repos/cammo22/Liquidino/releases/latest", ct);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null;
    }

    public static bool ENuova(string? tag, string nostra)
    {
        if (string.IsNullOrWhiteSpace(tag)) return false;
        var loro = tag.Trim().TrimStart('v').Split('-', '+')[0].Split('.');
        var nostri = nostra.Split('.', '-');
        for (var i = 0; i < 3; i++)
        {
            var a = loro.Length > i && int.TryParse(loro[i], out var x) ? x : 0;
            var b = nostri.Length > i && int.TryParse(nostri[i], out var y) ? y : 0;
            if (a != b) return a > b;
        }
        return false;
    }
}
