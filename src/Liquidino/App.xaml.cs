using System.Windows;
using Liquidino.Nucleo;
using Velopack;

namespace Liquidino;

public partial class App : System.Windows.Application
{
    Mutex? _mutex;
    Ponte? _ponte;
    FinestraAvatar? _avatar;
    Vassoio? _vassoio;
    public bool InUscita { get; private set; }

    public App()
    {
        // Prima di tutto: Velopack sistema l'avvio dopo un aggiornamento. Non va in rete da solo.
        VelopackApp.Build()
            .OnBeforeUpdateFastCallback(_ => MigrazioneDati.Esegui())
            .OnBeforeUninstallFastCallback(_ => MigrazioneDati.Esegui())
            .Run();
        InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var prova = Array.IndexOf(e.Args, ProvaAvvio.Argomento);
        if (prova >= 0)
        {
            var cartella = prova + 1 < e.Args.Length ? e.Args[prova + 1] : Path.Combine(Path.GetTempPath(), "liquidino-prova-avvio");
            ProvaAvvio.Avvia(this, cartella);
            return;
        }
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

        try { MigrazioneDati.Esegui(); }
        catch { /* se la copia non riesce, i dati nuovi nascono vuoti e i vecchi restano dov'erano */ }
        Semi.Prepara();
        var imp = ImpostazioniUtente.Carica();
        if (imp.CopiaRovinata is { } rovinata)
            MessageBox.Show("Le impostazioni non si leggevano più. Le ho messe da parte in\n" + rovinata +
                "\ne riparto dalle scelte di serie. Le note e la memoria non sono state toccate.", "Liquidino");
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
        _avatar = new FinestraAvatar(_ponte);
        _vassoio = new Vassoio(_ponte, _avatar, Esci);
        MainWindow = _avatar;
        _avatar.Show();
        _ponte.AvviaOrologi();
        _ponte.SegnaAvvio();

        Aggiornamenti.Avvia();

        if (imp.VersioneNovitaViste != Ponte.Versione)
        {
            new FinestraNovita().Show();
            imp.VersioneNovitaViste = Ponte.Versione;
            imp.Salva();
        }
    }

    /// <summary>Chiude senza chiedere: per la prova di avvio.</summary>
    public void Termina(int codice)
    {
        InUscita = true;
        Shutdown(codice);
    }

    public void Esci()
    {
        if (InUscita) return;
        InUscita = true;
        try { _vassoio?.Dispose(); } catch { /* l'icona può essere già tolta */ }
        try { _ponte?.Spegni(); } catch { /* il motore viene comunque chiuso sotto */ }
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        InUscita = true;
        try { _vassoio?.Dispose(); } catch { /* l'icona può essere già tolta */ }
        try { _ponte?.Spegni(); } catch { /* in chiusura non si insiste */ }
        try { Aggiornamenti.AllaChiusura(); } catch { /* l'aggiornamento, se c'è, ci riprova al prossimo avvio */ }
        try { _mutex?.ReleaseMutex(); } catch { /* il mutex può essere già lasciato */ }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
