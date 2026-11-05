using System.Windows;
using System.Windows.Controls;
using Liquidino.Nucleo;

namespace Liquidino;

/// <summary>Primo avvio: il tuo nome, il carattere, la voce. Poi misura il PC.</summary>
public sealed class FinestraSetup : Window
{
    readonly ImpostazioniUtente _imp;
    readonly TextBox _nome;
    string _carattere = "calmo";
    string _voce = "female";
    readonly List<Button> _caratteri = [];
    readonly Button _lei;
    readonly Button _lui;

    public FinestraSetup(ImpostazioniUtente imp)
    {
        _imp = imp;
        Title = "Liquidino · primo avvio";
        Width = 560;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;

        var pila = new StackPanel { Margin = new Thickness(28) };
        pila.Children.Add(new TextBlock { Text = "LIQUIDINO", FontFamily = Stile.Marchio, FontSize = 22, Foreground = Stile.OroP });
        pila.Children.Add(Stile.Riga("Prima di tutto, due cose tue. Il resto lo scelgo io dal PC, senza andare in rete."));
        pila.Children.Add(Etichetta("Come ti chiami?"));
        _nome = Stile.Campo("il tuo nome");
        pila.Children.Add(_nome);
        pila.Children.Add(Etichetta("Che carattere vuoi?"));
        var caratteri = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) };
        foreach (var c in ImpostazioniUtente.Caratteri)
        {
            var b = Stile.Bottone(c);
            var nome = c;
            b.Click += (_, _) => ScegliCarattere(nome);
            _caratteri.Add(b);
            caratteri.Children.Add(b);
        }
        pila.Children.Add(caratteri);
        pila.Children.Add(Etichetta("Che voce, quando ti legge le risposte?"));
        var voci = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 12) };
        _lei = Stile.Bottone("femminile", principale: true);
        _lui = Stile.Bottone("maschile");
        _lei.Click += (_, _) => ScegliVoce("female");
        _lui.Click += (_, _) => ScegliVoce("male");
        voci.Children.Add(_lei);
        voci.Children.Add(_lui);
        pila.Children.Add(voci);
        pila.Children.Add(Stile.Riga(OrecchioLeggero.Stato(_imp.ParolaSveglia, RilevatoreModelli.Cerca(_imp.CartellaMotore).CartellaModelli).Messaggio, secondario: true));
        var parti = Stile.Bottone("Si parte", principale: true);
        parti.HorizontalAlignment = HorizontalAlignment.Left;
        parti.Margin = new Thickness(0, 16, 0, 0);
        parti.Click += (_, _) => Conferma();
        pila.Children.Add(parti);

        Content = new Border
        {
            Background = Stile.Fondo,
            BorderBrush = Stile.OroP,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = pila
        };
        ScegliCarattere("calmo");
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is Button or TextBox) return;
            try { DragMove(); } catch { /* già rilasciato */ }
        };
    }

    static TextBlock Etichetta(string testo) => new()
    {
        Text = testo,
        FontFamily = Stile.Marchio,
        FontSize = 12,
        Foreground = Stile.CianoP,
        Margin = new Thickness(0, 14, 0, 4)
    };

    void ScegliCarattere(string nome)
    {
        _carattere = nome;
        foreach (var b in _caratteri)
        {
            var acceso = (b.Content as string) == nome;
            b.BorderBrush = acceso ? Stile.OroP : Stile.Pennello("#3a3648");
            b.Foreground = acceso ? Stile.OroP : Stile.Inchiostro;
        }
    }

    void ScegliVoce(string voce)
    {
        _voce = voce;
        _lei.BorderBrush = voce == "female" ? Stile.MagentaP : Stile.Pennello("#3a3648");
        _lui.BorderBrush = voce == "male" ? Stile.CianoP : Stile.Pennello("#3a3648");
    }

    void Conferma()
    {
        var nome = Stile.TestoCampo(_nome);
        if (nome.Length == 0) nome = "amico";
        _imp.NomeUtente = nome;
        _imp.Carattere = _carattere;
        _imp.Voce = _voce;
        _imp.PrimoAvvioCompletato = true;
        DialogResult = true;
    }
}
