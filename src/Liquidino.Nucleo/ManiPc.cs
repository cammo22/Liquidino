using System.Globalization;
using System.Text;

namespace Liquidino.Nucleo;

/// <summary>I soli gesti che Liquidino sa fare sul PC. Tutto il resto lo dice e lo lascia a te.</summary>
public enum TipoGesto
{
    ApriProgramma,
    ApriCartella,
    Volume,
    GuardaSchermo
}

/// <summary>Un programma del menu Start. Avvio è il collegamento .lnk, l'eseguibile o shell:AppsFolder\ID.</summary>
public sealed record ProgrammaNoto(string Nome, string Avvio);

/// <summary>
/// Un gesto già capito. Bersaglio: per un programma l'avvio, per una cartella il percorso,
/// per il volume una fra su, giu, muto, pausa. Nome è quello che si legge nella domanda e nel registro.
/// </summary>
public sealed record GestoPc(TipoGesto Tipo, string Bersaglio, string Nome)
{
    /// <summary>Aprire un programma chiede sempre il sì. Cartelle, volume e schermo partono col permesso acceso.</summary>
    public bool VuoleConferma => Tipo == TipoGesto.ApriProgramma;
}

public sealed record Comprensione(GestoPc? Gesto, string? Problema);

/// <summary>Le mani vere stanno nella finestra (Windows). Qui c'è solo il contratto, così le prove usano mani finte.</summary>
public interface IManiPc
{
    IReadOnlyList<ProgrammaNoto> Programmi();

    /// <summary>Chiavi: documenti, download, immagini, musica, video, desktop.</summary>
    IReadOnlyDictionary<string, string> Cartelle();

    /// <summary>Fa il gesto e dice cosa ha fatto. Se non ci riesce lancia un'eccezione col motivo.</summary>
    Task<string> Esegui(GestoPc gesto, CancellationToken ct);

    /// <summary>Una foto dello schermo principale, solo in memoria. Null se non si può fare.</summary>
    Task<ImmagineSchermo?> FotoSchermo(CancellationToken ct);
}

public sealed record ImmagineSchermo(byte[] Dati, string Tipo);

/// <summary>
/// Capisce se una frase chiede di usare il PC e quale gesto, senza modello: regole semplici e chiuse.
/// Meglio dire «questo fallo tu» che indovinare un clic.
/// </summary>
public static class InterpretePc
{
    public const string CosaSoFare =
        "le mie mani sul PC sanno solo aprire un programma del menu Start, aprire Documenti, Download, Immagini, Musica, Video o il Desktop, alzare o abbassare il volume, mettere in pausa la musica e guardare lo schermo. Non clicco, non scrivo al posto tuo, non chiudo e non installo niente";

    static readonly Dictionary<string, string> SinonimiCartelle = new()
    {
        ["documenti"] = "documenti",
        ["documento"] = "documenti",
        ["download"] = "download",
        ["downloads"] = "download",
        ["scaricati"] = "download",
        ["immagini"] = "immagini",
        ["foto"] = "immagini",
        ["fotografie"] = "immagini",
        ["musica"] = "musica",
        ["video"] = "video",
        ["filmati"] = "video",
        ["desktop"] = "desktop",
        ["scrivania"] = "desktop"
    };

    static readonly string[] VerbiApri =
    [
        "apri", "aprimi", "aprire", "aprirmi", "apra", "avvia", "avviami", "avviare", "avvii",
        "lancia", "lanciare", "esegui", "eseguire"
    ];

    /// <summary>Con questi verbi la frase è sempre per le mani. «lancia una moneta» invece no, se non c'è un programma così.</summary>
    static readonly string[] VerbiSicuri = ["apri", "aprimi", "aprire", "aprirmi", "apra", "avvia", "avviami", "avviare", "avvii"];

    /// <summary>Frasi da segnare o ricordare: anche se dentro c'è «clicca» o «apri», non sono gesti.</summary>
    static readonly string[] InizioNotaOMemoria =
    [
        "prendi nota", "segnati", "segna", "appunta", "appuntati", "nota", "scrivi una nota", "scrivimi una nota",
        "ricordati", "ricorda", "ricordami", "dimentica", "dimenticati"
    ];

    static readonly HashSet<string> Vuote =
    [
        "il", "lo", "la", "l", "i", "gli", "le", "un", "una", "uno", "un'", "di", "del", "dello", "della", "dei", "degli", "delle",
        "per", "favore", "piacere", "mi", "me", "ti", "prego", "puoi", "potresti", "riesci", "a", "subito", "adesso", "ora",
        "programma", "app", "applicazione", "cartella", "grazie", "dai", "liquidino", "e", "pure", "anche", "sul", "pc", "computer"
    ];

    /// <summary>Cose del suo mondo: le apre la finestra di Liquidino, non le mani.</summary>
    static readonly string[] DelMondo = ["chat", "creazioni", "creazione", "mondo", "nota", "note", "memoria", "diario", "cestino", "registro", "tuo", "tua", "tue", "tuoi"];

    static readonly string[] NonSupportati =
    [
        "clicca", "cliccare", "doppio clic", "muovi il mouse", "sposta il mouse", "usa il mouse", "premi il tasto", "premi invio",
        "scrivi al posto mio", "digita", "usa il pc", "usa il computer", "controlla il desktop", "chiudi il programma",
        "chiudi la finestra", "chiudi tutto", "spegni il pc", "spegni il computer", "riavvia il pc", "riavvia il computer",
        "installa", "disinstalla", "cancella il file", "elimina il file", "formatta", "manda una mail", "invia una mail"
    ];

    /// <summary>Vero se la frase chiede qualcosa sul PC, fattibile o no.</summary>
    public static bool Riguarda(string testo, IReadOnlyList<ProgrammaNoto>? programmi = null)
    {
        var n = Normalizza(testo);
        if (n.Length == 0) return false;
        if (InizioNotaOMemoria.Any(k => n == Normalizza(k) || n.StartsWith(Normalizza(k) + " ", StringComparison.Ordinal)))
            return false;
        if (NonSupportati.Any(k => Contiene(n, Normalizza(k)))) return true;
        if (Volume(n) is not null) return true;
        if (Schermo(n)) return true;
        var (verbo, oggetto) = VerboEOggetto(n);
        if (verbo is null || oggetto.Count == 0) return false;
        // «apri il blocco note» è un programma anche se dentro c'è «note».
        if (programmi is not null && NomeIntero(oggetto, programmi)) return true;
        if (oggetto.Any(o => DelMondo.Contains(o))) return false;
        if (VerbiSicuri.Contains(verbo)) return true;
        return programmi is not null && Migliore(oggetto, programmi).Programma is not null;
    }

    /// <summary>Vero se tutte le parole di un programma ammesso stanno nella richiesta.</summary>
    static bool NomeIntero(IReadOnlyList<string> oggetto, IReadOnlyList<ProgrammaNoto> programmi) =>
        programmi.Any(p => ProgrammaAmmesso(p.Nome) && Parole(Normalizza(p.Nome)) is { Count: > 0 } sue && sue.All(oggetto.Contains));

    public static Comprensione Capisci(string testo, IReadOnlyList<ProgrammaNoto> programmi, IReadOnlyDictionary<string, string> cartelle)
    {
        var n = Normalizza(testo);
        if (NonSupportati.Any(k => Contiene(n, Normalizza(k))))
            return new Comprensione(null, CosaSoFare);

        if (Volume(n) is { } v)
        {
            var nome = v switch
            {
                "su" => "alzo il volume",
                "giu" => "abbasso il volume",
                "muto" => "metto o tolgo il muto",
                _ => "pausa o riprendo la musica"
            };
            return new Comprensione(new GestoPc(TipoGesto.Volume, v, nome), null);
        }

        if (Schermo(n))
            return new Comprensione(new GestoPc(TipoGesto.GuardaSchermo, "schermo", "guardo lo schermo"), null);

        var (verbo, oggetto) = VerboEOggetto(n);
        if (verbo is null || oggetto.Count == 0)
            return new Comprensione(null, CosaSoFare);

        foreach (var parola in oggetto)
        {
            if (!SinonimiCartelle.TryGetValue(parola, out var chiave)) continue;
            // «apri video» col solo nome della cartella, o «apri la cartella video».
            if (oggetto.Count == 1 || Contiene(n, "cartella"))
            {
                if (cartelle.TryGetValue(chiave, out var percorso) && !string.IsNullOrWhiteSpace(percorso))
                    return new Comprensione(new GestoPc(TipoGesto.ApriCartella, percorso, NomeCartella(chiave)), null);
                return new Comprensione(null, "non trovo la cartella " + NomeCartella(chiave) + " su questo PC");
            }
        }
        if (Contiene(n, "cartella"))
            return new Comprensione(null, "non conosco quella cartella. So aprire solo Documenti, Download, Immagini, Musica, Video e il Desktop");

        var (scelto, pari) = Migliore(oggetto, programmi);
        if (scelto is null)
            return new Comprensione(null, "non trovo «" + string.Join(' ', oggetto) + "» fra i programmi del menu Start");
        if (pari is not null)
            return new Comprensione(null, "ho trovato più programmi: «" + scelto.Nome + "» e «" + pari.Nome + "». Dimmi il nome intero");
        return new Comprensione(new GestoPc(TipoGesto.ApriProgramma, scelto.Avvio, scelto.Nome), null);
    }

    public static string Domanda(GestoPc gesto) => gesto.Tipo switch
    {
        TipoGesto.ApriProgramma => "Apro «" + gesto.Nome + "»?",
        _ => "Vuoi che " + gesto.Nome + "?"
    };

    /// <summary>Niente disinstallatori, guide o pagine web: nel menu Start ci sono anche quelli.</summary>
    public static bool ProgrammaAmmesso(string nome)
    {
        var n = Normalizza(nome);
        if (n.Length == 0) return false;
        string[] no = ["uninstall", "disinstalla", "rimuovi", "remove", "leggimi", "readme", "guida", "help", "manuale", "sito web", "website", "license", "licenza"];
        return !no.Any(k => Contiene(n, k));
    }

    public static string NomeCartella(string chiave) => chiave switch
    {
        "documenti" => "Documenti",
        "download" => "Download",
        "immagini" => "Immagini",
        "musica" => "Musica",
        "video" => "Video",
        "desktop" => "Desktop",
        _ => chiave
    };

    static string? Volume(string n)
    {
        var parole = Parole(n);
        var volume = parole.Contains("volume") || parole.Contains("audio");
        // Il verbo vince su «su» e «giù»: «abbassa il volume su Spotify» abbassa.
        var su = parole.Any(p => p is "alza" or "alzami" or "alzalo" or "aumenta" or "aumentalo");
        var giu = parole.Any(p => p is "abbassa" or "abbassami" or "abbassalo" or "diminuisci" or "diminuiscilo");
        if (!su && !giu)
        {
            su = Contiene(n, "volume su");
            giu = Contiene(n, "volume giu");
        }
        if (volume && su && !giu) return "su";
        if (volume && giu && !su) return "giu";
        if (Contiene(n, "metti il muto") || Contiene(n, "metti muto") || Contiene(n, "togli il muto") || Contiene(n, "attiva il muto")
            || (volume && parole.Any(p => p is "togli" or "togliere" or "disattiva" or "spegni" or "silenzia" or "azzera")))
            return "muto";
        var media = parole.Any(p => p is "musica" or "canzone" or "brano" or "video" or "film" or "spotify");
        if (media && parole.Any(p => p is "pausa" or "riprendi" or "play" or "ferma" or "fermare"))
            return "pausa";
        return null;
    }

    static bool Schermo(string n)
    {
        var parole = Parole(n);
        if (!parole.Contains("schermo") && !parole.Contains("schermata")) return false;
        if (parole.Any(p => p is "guarda" or "guardami" or "vedi" or "leggi" or "leggimi" or "descrivi" or "descrivimi"))
            return true;
        // «cosa c'è sullo schermo» diventa «cosa c e sullo schermo».
        return Contiene(n, "c e sullo schermo") || Contiene(n, "c e sul mio schermo") || Contiene(n, "c e nello schermo");
    }

    static (string? Verbo, List<string> Oggetto) VerboEOggetto(string n)
    {
        var parole = Parole(n);
        for (var i = 0; i < parole.Count; i++)
        {
            string? verbo = null;
            var salta = 1;
            if (VerbiApri.Contains(parole[i])) verbo = parole[i];
            else if (parole[i] is "fai" or "fammi" && i + 1 < parole.Count && parole[i + 1] is "partire") { verbo = "lancia"; salta = 2; }
            if (verbo is null) continue;
            var oggetto = parole.Skip(i + salta).Where(p => !Vuote.Contains(p)).ToList();
            return (verbo, oggetto);
        }
        return (null, []);
    }

    /// <summary>
    /// Il programma col nome più vicino. Una parola uguale vale 2, l'inizio di una parola (almeno 4 lettere) vale 1.
    /// A pari punti vince il nome con meno parole, poi il più corto. Se resta un pari vero, lo dice.
    /// </summary>
    static (ProgrammaNoto? Programma, ProgrammaNoto? Pari) Migliore(IReadOnlyList<string> oggetto, IReadOnlyList<ProgrammaNoto> programmi)
    {
        var classifica = new List<(ProgrammaNoto P, int Punti, int Parole, int Lunghezza)>();
        var visti = new HashSet<string>();
        foreach (var p in programmi)
        {
            if (!ProgrammaAmmesso(p.Nome)) continue;
            var nome = Normalizza(p.Nome);
            if (!visti.Add(nome)) continue;
            var sue = Parole(nome);
            if (sue.Count == 0) continue;
            var punti = 0;
            foreach (var o in oggetto)
            {
                if (sue.Contains(o)) punti += 2;
                else if (o.Length >= 4 && sue.Any(s => s.StartsWith(o, StringComparison.Ordinal))) punti += 1;
            }
            if (punti > 0) classifica.Add((p, punti, sue.Count, nome.Length));
        }
        if (classifica.Count == 0) return (null, null);
        var ordinati = classifica
            .OrderByDescending(c => c.Punti)
            .ThenBy(c => c.Parole)
            .ThenBy(c => c.Lunghezza)
            .ToList();
        var primo = ordinati[0];
        if (ordinati.Count > 1)
        {
            var secondo = ordinati[1];
            if (secondo.Punti == primo.Punti && secondo.Parole == primo.Parole && secondo.Lunghezza == primo.Lunghezza)
                return (primo.P, secondo.P);
        }
        return (primo.P, null);
    }

    /// <summary>Minuscole, senza accenti, apostrofi uniformi, solo lettere, cifre e spazi.</summary>
    public static string Normalizza(string? testo)
    {
        var s = (testo ?? "").Normalize(NormalizationForm.FormD);
        var b = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) b.Append(char.ToLowerInvariant(c));
            else b.Append(' ');
        }
        return string.Join(' ', b.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    static List<string> Parole(string normalizzato) =>
        normalizzato.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>Contiene la frase come parole intere: «audio» non scatta dentro «audiolibro».</summary>
    static bool Contiene(string normalizzato, string frase) =>
        (" " + normalizzato + " ").Contains(" " + frase + " ", StringComparison.Ordinal);
}
