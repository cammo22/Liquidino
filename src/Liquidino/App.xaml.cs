using System.Windows;
using Liquidino.Nucleo;

namespace Liquidino;

public partial class App : System.Windows.Application
{
    Mutex? _mutex;
    Ponte? _ponte;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, ev) =>
        {
            try
            {
                var percorso = Percorsi.DiarioTecnico();
                Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
                File.AppendAllText(percorso, DateTimeOffset.Now.ToString("o") + "\n" + ev.Exception + "\n\n");
            }
            catch { /* se non si può scrivere, almeno la finestra lo dice */ }
            MessageBox.Show("Qualcosa è andato storto. Questo fallo tu: guarda il diario tecnico nella cartella di Liquidino.\n\n" + ev.Exception.Message, "Liquidino");
            ev.Handled = true;
        };

        var primo = false;
        try { _mutex = new Mutex(true, @"Local\Liquidino", out primo); }
        catch (AbandonedMutexException) { primo = true; }
        if (!primo)
        {
            MessageBox.Show("Liquidino è già aperto.", "Liquidino");
            Shutdown();
            return;
        }

        Semi.Prepara();
        var imp = ImpostazioniUtente.Carica();
        if (!imp.PrimoAvvioCompletato)
        {
            var setup = new FinestraSetup(imp);
            var ok = setup.ShowDialog();
            if (ok != true)
            {
                Shutdown();
                return;
            }
            imp.PrimoAvvio = DateTimeOffset.Now;
            imp.PrimoAvvioCompletato = true;
            imp.ApplicaContesto(SceltaContesto.Scegli(MisuraPc.Misura()));
            imp.Salva();
            MessageBox.Show(imp.FraseContesto, "Liquidino");
        }
        else if (!imp.ContestoMisurato)
        {
            imp.ApplicaContesto(SceltaContesto.Scegli(MisuraPc.Misura()));
            imp.Salva();
        }

        _ponte = new Ponte(imp);
        var avatar = new FinestraAvatar(_ponte);
        MainWindow = avatar;
        avatar.Show();
        _ponte.AvviaOrologi();

        if (imp.VersioneNovitaViste != Ponte.Versione)
        {
            new FinestraNovita().Show();
            imp.VersioneNovitaViste = Ponte.Versione;
            imp.Salva();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _ponte?.Chiudi(); } catch { /* in chiusura non si insiste */ }
        try { _mutex?.ReleaseMutex(); } catch { /* il mutex può essere già lasciato */ }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
