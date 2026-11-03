using System.Drawing;
using System.Windows.Forms;

namespace Liquidino;

/// <summary>Icona in basso a destra. Il tasto destro apre il menu. Liquidino resta acceso anche se l'avatar è nascosto.</summary>
public sealed class Vassoio : IDisposable
{
    readonly NotifyIcon _icona;
    readonly Ponte _ponte;
    readonly FinestraAvatar _avatar;
    readonly Action _esci;
    readonly ToolStripMenuItem _aggiornamenti;
    bool _chiuso;

    public Vassoio(Ponte ponte, FinestraAvatar avatar, Action esci)
    {
        _ponte = ponte;
        _avatar = avatar;
        _esci = esci;
        _aggiornamenti = new ToolStripMenuItem(Aggiornamenti.Stato) { Font = new Font("Segoe UI", 10) };
        _aggiornamenti.Click += async (_, _) => await ClicAggiornamenti();

        var menu = new ContextMenuStrip();
        Voce(menu, "Mostra o nascondi l'avatar", AlternaAvatar);
        menu.Items.Add(_aggiornamenti);
        menu.Items.Add(new ToolStripSeparator());
        Voce(menu, "Esci", _esci);
        menu.Opening += (_, _) => _aggiornamenti.Text = Aggiornamenti.Stato;

        _icona = new NotifyIcon
        {
            Icon = Icona(),
            Visible = true,
            Text = "Liquidino",
            ContextMenuStrip = menu
        };
        _icona.DoubleClick += (_, _) => MostraAvatar();
        Aggiornamenti.StatoCambiato += AggiornaVoce;
    }

    static void Voce(ContextMenuStrip menu, string testo, Action fai)
    {
        var voce = new ToolStripMenuItem(testo) { Font = new Font("Segoe UI", 10) };
        voce.Click += (_, _) => fai();
        menu.Items.Add(voce);
    }

    static Icon Icona()
    {
        var percorso = Path.Combine(AppContext.BaseDirectory, "icona.ico");
        if (File.Exists(percorso)) return new Icon(percorso);
        return System.Drawing.SystemIcons.Application;
    }

    void AlternaAvatar()
    {
        if (_avatar.IsVisible) _avatar.Nascondi();
        else MostraAvatar();
    }

    void MostraAvatar()
    {
        _avatar.MostraDiNuovo();
    }

    async Task ClicAggiornamenti()
    {
        try
        {
            if (Aggiornamenti.Disponibile is null)
                await Aggiornamenti.Controlla(scarica: true, CancellationToken.None);
            if (Aggiornamenti.Disponibile is not null)
                await Aggiornamenti.Installa();
        }
        catch (Exception e)
        {
            _ponte.Annuncia("Questo fallo tu: non sono riuscito a aggiornare. " + e.Message);
        }
    }

    void AggiornaVoce()
    {
        try { _aggiornamenti.Text = Aggiornamenti.Stato; }
        catch { /* il menu è stato chiuso */ }
    }

    public void Dispose()
    {
        if (_chiuso) return;
        _chiuso = true;
        Aggiornamenti.StatoCambiato -= AggiornaVoce;
        _icona.Visible = false;
        _icona.Dispose();
    }
}
