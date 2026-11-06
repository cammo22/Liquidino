using System.Windows;
using System.Windows.Threading;
using Liquidino.Nucleo;

namespace Liquidino;

/// <summary>
/// «Liquidino.exe --prova-avvio cartella»: apre tutte le finestre su dati finti, fa qualche turno senza motore,
/// legge i programmi del menu Start senza aprirne nessuno, ed esce. Ogni passo scrive PASS o FAIL in esito-avvio.txt.
/// Serve alla CI su Windows: i dati veri non si toccano, niente rete, niente volume, niente clic.
/// </summary>
public static class ProvaAvvio
{
    public const string Argomento = "--prova-avvio";
    static readonly TimeSpan Massimo = TimeSpan.FromSeconds(150);

    public static void Avvia(App app, string cartella)
    {
        cartella = Path.GetFullPath(cartella);
        Directory.CreateDirectory(cartella);
        var esito = Path.Combine(cartella, "esito-avvio.txt");
        File.WriteAllText(esito, "Liquidino " + Ponte.Versione + ", prova di avvio" + Environment.NewLine);
        void Riga(string r)
        {
            try { File.AppendAllText(esito, r + Environment.NewLine); }
            catch { /* la riga si perde, l'esito resta nel codice di uscita */ }
        }

        var finito = false;
        var guardia = new DispatcherTimer { Interval = Massimo };
        void Fine(int codice)
        {
            if (finito) return;
            finito = true;
            guardia.Stop();
            Riga(codice == 0 ? "ESITO: OK" : "ESITO: FALLITA");
            app.Termina(codice);
        }

        app.DispatcherUnhandledException += (_, ev) =>
        {
            ev.Handled = true;
            Riga("FAIL eccezione nella finestra: " + ev.Exception);
            Fine(1);
        };
        guardia.Tick += (_, _) =>
        {
            Riga("FAIL la prova supera " + Massimo.TotalSeconds + " secondi");
            Fine(1);
        };
        guardia.Start();
        app.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await Passi(cartella, Riga);
                Fine(0);
            }
            catch (Exception e)
            {
                Riga("FAIL " + e);
                Fine(1);
            }
        });
    }

    static async Task Passi(string cartella, Action<string> riga)
    {
        Percorsi.UsaRadice(Path.Combine(cartella, "dati"));
        Semi.Prepara();
        riga("PASS cartelle e file di serie in " + Percorsi.RadiceDati());

        var imp = ImpostazioniUtente.Carica();
        imp.PrimoAvvioCompletato = true;
        imp.NomeUtente = "Prova";
        imp.CartellaMotore = Path.Combine(cartella, "motore-vuoto");
        Directory.CreateDirectory(imp.CartellaMotore);
        imp.ApplicaContesto(SceltaContesto.Scegli(MisuraPc.Misura()));
        imp.Salva();
        riga("PASS misura del PC: " + imp.FraseContesto);

        var setup = new FinestraSetup(new ImpostazioniUtente());
        setup.Show();
        await Attesa();
        setup.Close();
        riga("PASS finestra del primo avvio");

        var ponte = new Ponte(imp);
        var avatar = new FinestraAvatar(ponte);
        var vassoio = new Vassoio(ponte, avatar, () => { });
        try
        {
            avatar.Show();
            ponte.AvviaOrologi();
            ponte.SegnaAvvio();
            await Attesa();
            riga("PASS avatar e icona nel vassoio");

            var casa = avatar.ApriCasa();
            await Attesa();
            riga("PASS chat e mondo");

            var ciao = await ponte.Messaggio("ciao");
            Verifica(ciao.Testo.Contains("Questo fallo tu", StringComparison.Ordinal), "senza motore dice di farlo tu: " + ciao.Testo);
            var nota = await ponte.Messaggio("prendi nota: prova di avvio");
            Verifica(nota.FileCreato is { } f && File.Exists(f), "la nota si scrive: " + nota.Testo);
            var pc = await ponte.Messaggio("apri la calcolatrice");
            Verifica(pc.FalloTu && pc.Testo.Contains("permesso", StringComparison.Ordinal), "senza permesso le mani non partono: " + pc.Testo);
            riga("PASS dialogo: chiacchiera, nota, permesso");

            var programmi = await Task.Run(() => ponte.Mani.Programmi());
            Verifica(programmi.Count > 0, "le mani vedono i programmi del menu Start");
            var app = programmi.Count(p => p.Avvio.StartsWith(ManiWindows.PrefissoApp, StringComparison.OrdinalIgnoreCase));
            riga("PASS mani: " + programmi.Count + " programmi (" + app + " dalla shell), cartelle " + string.Join(", ", ponte.Mani.Cartelle().Keys));
            try
            {
                var foto = await ponte.Mani.FotoSchermo(CancellationToken.None);
                riga(foto is { Dati.Length: > 0 } ? "PASS foto dello schermo, " + foto.Dati.Length + " byte, resta in memoria" : "NOTA foto dello schermo non disponibile su questa macchina");
            }
            catch (Exception e) { riga("NOTA foto dello schermo: " + e.Message); }
            riga("PASS voci di Windows: " + VoceWindows.Descrizione());

            casa.EsercitaPannello();
            await Attesa();
            riga("PASS pannello del mondo: icone, ricerca, anteprima, diario");

            await ponte.PensieroAdesso();
            Verifica(Directory.EnumerateFiles(Path.Combine(ponte.Mondo.Radice, "idee")).Any(), "il pensiero chiesto si scrive");
            riga("PASS un pensiero adesso");

            var impostazioni = new FinestraImpostazioni(ponte);
            impostazioni.Show();
            impostazioni.ApriTutte();
            Verifica(impostazioni.Schede == 9, "le impostazioni hanno le nove schede");
            await Attesa(2500);
            impostazioni.Close();
            riga("PASS impostazioni: tutte le " + impostazioni.Schede + " schede si aprono");

            await ponte.AlternaPausa();
            await Attesa();
            Verifica(ponte.Regista.InPausa && ponte.Impostazioni.Pausa, "la pausa si accende");
            await ponte.AlternaPausa();
            Verifica(!ponte.Regista.InPausa, "la pausa si spegne");
            riga("PASS pausa avanti e indietro");

            ponte.AlternaSonno();
            await Attesa();
            Verifica(ponte.Dormita is not null && ponte.Stato == StatoCompagno.Dorme, "si addormenta");
            var durata = ponte.Dormita!.Durata;
            ponte.AlternaSonno();
            await Attesa();
            Verifica(ponte.Dormita is null && ponte.Stato == StatoCompagno.Sveglio, "si sveglia col tasto");
            riga("PASS sonno: dormiva " + Sonno.Leggibile(durata) + ", svegliato a mano");

            var modelli = new FinestraModelli(ponte);
            modelli.Show();
            await Attesa();
            Verifica(modelli.Schede == CatalogoModelli.Tutti.Count + 1, "la pagina dei modelli mostra tutto il catalogo, Parakeet compreso");
            modelli.Close();
            riga("PASS pagina dei modelli (senza scaricare niente)");

            var parola = new FinestraParola(ponte);
            parola.Show();
            await Attesa(1500);
            parola.Close();
            riga("PASS finestra della parola");

            // Senza Parakeet le Orecchie non si accendono e lo dicono, senza rompersi.
            string? detto = null;
            ponte.Annuncio += a => detto = a;
            await ponte.AlternaOrecchie();
            await Attesa();
            if (ponte.Udito.ParakeetPresente) riga("INFO Parakeet c'è: orecchie " + (ponte.Udito.Accese ? "accese" : "spente") + ". " + detto);
            else
            {
                Verifica(!ponte.Udito.Accese && detto is not null && detto.Contains("Parakeet"), "senza Parakeet le Orecchie restano spente e lo dicono");
                riga("PASS orecchie senza Parakeet: " + detto);
            }
            if (ponte.Udito.Accese) await ponte.AlternaOrecchie();

            // La cattura dell'audio del PC dipende dalla macchina (le macchine della CI possono non avere l'audio): solo informazione.
            try
            {
                using var audioPc = await CatturaProcesso.Apri(Environment.ProcessId, includi: false, CancellationToken.None);
                riga("INFO cattura dell'audio del PC senza Liquidino: si apre");
            }
            catch (Exception e) { riga("INFO cattura dell'audio del PC: non disponibile qui (" + e.GetType().Name + ": " + e.Message + ")"); }
            riga("INFO microfoni: " + Dispositivi.Ingressi().Count + ", uscite: " + Dispositivi.Uscite().Count + ", VB-CABLE: " + (Dispositivi.Cavo() is null ? "no" : "sì"));

            var novita = new FinestraNovita();
            novita.Show();
            await Attesa();
            novita.Close();
            riga("PASS novità");

            ponte.Stop();
            ponte.Stop();
            await Attesa();
            Verifica(ponte.Stato == StatoCompagno.Dorme, "dopo STOP dorme");
            riga("PASS due STOP di fila, la finestra risponde");
            casa.Close();
        }
        finally
        {
            vassoio.Dispose();
            ponte.Spegni();
        }
    }

    static Task Attesa(int millisecondi = 400) => Task.Delay(millisecondi);

    static void Verifica(bool vero, string cosa)
    {
        if (!vero) throw new InvalidOperationException(cosa);
    }
}
