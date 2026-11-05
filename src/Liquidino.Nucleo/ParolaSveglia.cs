using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Liquidino.Nucleo;

/// <summary>Il nome trovato in una frase: quanto somiglia, cosa si è sentito e cosa resta da fare.</summary>
public sealed record Risveglio(double Punteggio, string Sentito, string Resto);

/// <summary>
/// La parola di attivazione tarata su di te: le forme in cui Parakeet trascrive la tua voce e la soglia.
/// Si salva in orecchio/parola.json.
/// </summary>
public sealed record ParolaPersonale(string Parola, IReadOnlyList<string> Varianti, double Soglia, int Registrazioni, int Riconosciute, DateTimeOffset? Data)
{
    public static ParolaPersonale DiSerie(string parola) =>
        new(string.IsNullOrWhiteSpace(parola) ? ParolaSveglia.DiSerie : parola.Trim(), [], ParolaSveglia.SogliaDiSerie, 0, 0, null);

    public bool Tarata => Registrazioni > 0;
}

/// <summary>Come è andata la taratura, con la frase da mostrare.</summary>
public sealed record EsitoTaratura(ParolaPersonale Parola, double MinimoTuo, double MassimoAltrui, bool Buona, string Frase);

/// <summary>
/// La parola per svegliare Liquidino («babbasone»). Parakeet trascrive la frase, qui si cerca il nome con un confronto
/// tollerante: le lettere che si confondono facilmente (b/p, t/d, s/z, le vocali) costano meno di un errore pieno.
/// Le tue registrazioni insegnano come Parakeet scrive la tua voce, e la soglia si mette fra te e le parole italiane simili.
/// </summary>
public static class ParolaSveglia
{
    public const string DiSerie = "babbasone";

    /// <summary>Senza taratura si tiene alta: «papà sono» non deve svegliarlo.</summary>
    public const double SogliaDiSerie = 0.84;

    /// <summary>Unire due o tre parole costa un po': «baba sone» va bene, frasi qualunque meno.</summary>
    const double CostoUnione = 0.03;

    /// <summary>Parole e pezzi di frase italiani che somigliano al nome. La soglia sta sopra di loro.</summary>
    public static readonly string[] Altrui =
    [
        "basta", "bastone", "bastoni", "sapone", "babbo", "babbo natale", "babbuino", "passione", "abbassa", "abbasso",
        "bambino", "bambine", "baciami", "bastoncino", "basso", "bassa", "bassotto", "bacio", "babà", "barbone",
        "borbone", "buonasera", "bastano", "sabbia", "stagione", "pastone", "cassone", "maglione", "pallone", "bottone",
        "basilico", "baccano", "balcone", "mattone", "cannone", "carbone", "cartone", "persone", "persona", "ragione",
        "canzone", "stazione", "pensione", "occasione", "abbastanza", "ebbasta", "papà sono", "babbo sono", "ma basta",
        "va bene", "pasta sono", "bas sono", "bassone", "la bassa", "passa sono", "abbiamo", "bambola", "babysitter",
        "sapore", "salone", "padrone", "bastava", "basato", "passato", "pazzo sono", "papa sono", "mamma sono"
    ];

    static readonly HashSet<string> Riempitivi = ["ehi", "hey", "ei", "oh", "ehy", "eh", "ciao", "senti", "allora", "dai", "ok", "okay", "o"];

    /// <summary>Minuscolo, senza accenti e senza punteggiatura, spazi singoli.</summary>
    public static string Normalizza(string testo)
    {
        var sb = new StringBuilder();
        foreach (var c in (testo ?? "").Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (char.IsWhiteSpace(c) || c is '-' or '\'' or '’') sb.Append(' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// La chiave fonetica: senza spazi né h, le doppie diventano singole, c/q/k sono uno stesso suono, y e j come i, w come v.
    /// </summary>
    public static string Chiave(string testo)
    {
        var n = Normalizza(testo).Replace(" ", "");
        var sb = new StringBuilder();
        foreach (var c0 in n)
        {
            var c = c0 switch
            {
                'q' or 'k' => 'c',
                'y' or 'j' => 'i',
                'w' => 'v',
                'x' => 's',
                _ => c0
            };
            if (c == 'h') continue;
            if (sb.Length > 0 && sb[^1] == c) continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    static bool Vocale(char c) => c is 'a' or 'e' or 'i' or 'o' or 'u';

    static double Costo(char a, char b)
    {
        if (a == b) return 0;
        var coppia = a < b ? (a, b) : (b, a);
        return coppia switch
        {
            ('b', 'p') or ('d', 't') or ('c', 'g') or ('f', 'v') or ('s', 'z') or ('m', 'n') or ('e', 'i') or ('o', 'u') => 0.4,
            ('l', 'r') => 0.6,
            _ when Vocale(a) && Vocale(b) => 0.6,
            _ => 1
        };
    }

    /// <summary>Somiglianza fra 0 e 1 di due testi, sulle chiavi fonetiche.</summary>
    public static double Somiglianza(string a, string b) => SomiglianzaChiavi(Chiave(a), Chiave(b));

    static double SomiglianzaChiavi(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        var riga = new double[b.Length + 1];
        var nuova = new double[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) riga[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            nuova[0] = i;
            for (var j = 1; j <= b.Length; j++)
                nuova[j] = Math.Min(Math.Min(riga[j] + 1, nuova[j - 1] + 1), riga[j - 1] + Costo(a[i - 1], b[j - 1]));
            (riga, nuova) = (nuova, riga);
        }
        return Math.Max(0, 1 - riga[b.Length] / Math.Max(a.Length, b.Length));
    }

    /// <summary>
    /// Quanto il primo testo sta dentro il secondo, fra 0 e 1, con lo stesso confronto tollerante.
    /// Serve a riconoscere l'eco: il microfono che risente quello che è appena uscito dalle casse.
    /// </summary>
    public static double Contenuto(string ago, string pagliaio)
    {
        var a = Chiave(ago);
        var b = Chiave(pagliaio);
        if (a.Length == 0 || b.Length == 0) return 0;
        // Distanza «semi-globale»: l'ago può cominciare e finire in qualunque punto del pagliaio.
        var riga = new double[b.Length + 1];
        var nuova = new double[b.Length + 1];
        for (var i = 1; i <= a.Length; i++)
        {
            nuova[0] = i;
            for (var j = 1; j <= b.Length; j++)
                nuova[j] = Math.Min(Math.Min(riga[j] + 1, nuova[j - 1] + 1), riga[j - 1] + Costo(a[i - 1], b[j - 1]));
            (riga, nuova) = (nuova, riga);
        }
        return Math.Max(0, 1 - riga.Min() / a.Length);
    }

    static IEnumerable<string> Bersagli(ParolaPersonale parola) =>
        new[] { Chiave(parola.Parola) }.Concat(parola.Varianti.Select(Chiave)).Where(k => k.Length > 0).Distinct();

    /// <summary>Il pezzo della frase (una, due o tre parole di fila) che somiglia di più al nome, con il suo punteggio.</summary>
    static (double Punteggio, int Da, int Quante) Migliore(IReadOnlyList<string> chiaviParole, IReadOnlyList<string> bersagli)
    {
        var migliore = (Punteggio: 0.0, Da: -1, Quante: 0);
        var lunghezzaMax = bersagli.Count == 0 ? 0 : bersagli.Max(b => b.Length);
        for (var i = 0; i < chiaviParole.Count; i++)
        {
            var unite = "";
            for (var q = 1; q <= 3 && i + q <= chiaviParole.Count; q++)
            {
                unite += chiaviParole[i + q - 1];
                if (unite.Length < 3) continue;
                if (unite.Length > lunghezzaMax * 1.6 + 2) break;
                foreach (var b in bersagli)
                {
                    var p = SomiglianzaChiavi(unite, b) - CostoUnione * (q - 1);
                    if (p > migliore.Punteggio + 1e-9) migliore = (p, i, q);
                }
            }
        }
        return migliore;
    }

    /// <summary>Cerca il nome nella frase trascritta. Null se non c'è o non somiglia abbastanza.</summary>
    public static Risveglio? Trova(string trascrizione, ParolaPersonale parola)
    {
        var parole = (trascrizione ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parole.Length == 0) return null;
        var chiavi = parole.Select(Chiave).ToList();
        var bersagli = Bersagli(parola).ToList();
        var (punteggio, da, quante) = Migliore(chiavi, bersagli);
        if (da < 0 || punteggio < parola.Soglia) return null;
        var sentito = string.Join(' ', parole.Skip(da).Take(quante));
        var prima = parole.Take(da).ToList();
        var dopo = parole.Skip(da + quante).ToList();
        // «Ehi babbasone, …»: i saluti davanti al nome non sono parte della richiesta.
        while (prima.Count > 0 && Riempitivi.Contains(Normalizza(prima[^1]))) prima.RemoveAt(prima.Count - 1);
        while (prima.Count > 0 && Riempitivi.Contains(Normalizza(prima[0]))) prima.RemoveAt(0);
        var resto = string.Join(' ', prima.Concat(dopo)).Trim().Trim(',', '.', '!', '?', ';', ':', ' ');
        return new Risveglio(Math.Round(punteggio, 3), sentito, resto);
    }

    /// <summary>
    /// Taratura sulle tue registrazioni. Ogni registrazione trascritta diventa una variante, se somiglia al nome:
    /// una trascrizione lontana (per esempio «grazie») si scarta, se no diventerebbe una parola per svegliarlo.
    /// La soglia sta a metà fra la registrazione tua più difficile e la parola italiana più simile.
    /// </summary>
    public static EsitoTaratura Tara(string parola, IReadOnlyList<string> sentiti)
    {
        parola = string.IsNullOrWhiteSpace(parola) ? DiSerie : parola.Trim();
        var canonica = Chiave(parola);
        var varianti = new List<string>();
        var accettate = new List<string>();
        foreach (var s in sentiti)
        {
            var parole = Normalizza(s).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parole.Length == 0) continue;
            string chiave;
            if (parole.Length <= 3) chiave = Chiave(string.Concat(parole));
            else
            {
                var (_, da, quante) = Migliore(parole.Select(Chiave).ToList(), [canonica]);
                chiave = da < 0 ? "" : Chiave(string.Concat(parole.Skip(da).Take(quante)));
            }
            if (chiave.Length < 3 || SomiglianzaChiavi(chiave, canonica) < 0.5) continue;
            accettate.Add(chiave);
            if (chiave != canonica && !varianti.Contains(chiave)) varianti.Add(chiave);
        }

        var tutti = new List<string> { canonica };
        tutti.AddRange(varianti);
        // Per ogni registrazione accettata: quanto somiglia al nome e alle altre, senza contare se stessa.
        var minimoTuo = 1.0;
        for (var j = 0; j < accettate.Count; j++)
        {
            var altre = accettate.Where((_, k) => k != j).Append(canonica).Distinct().ToList();
            var meglio = altre.Max(b => SomiglianzaChiavi(accettate[j], b));
            minimoTuo = Math.Min(minimoTuo, meglio);
        }
        if (accettate.Count == 0) minimoTuo = 0;

        var massimoAltrui = 0.0;
        foreach (var a in Altrui)
        {
            var pezzi = Normalizza(a).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var p = tutti.Max(b => SomiglianzaChiavi(Chiave(a), b)) - CostoUnione * (pezzi.Length - 1);
            massimoAltrui = Math.Max(massimoAltrui, p);
        }

        const double margine = 0.06;
        bool buona;
        double soglia;
        if (accettate.Count > 0 && minimoTuo >= massimoAltrui + margine)
        {
            soglia = Math.Clamp((minimoTuo + massimoAltrui) / 2, 0.66, 0.92);
            buona = true;
        }
        else
        {
            soglia = Math.Clamp(Math.Max(massimoAltrui + margine, SogliaDiSerie - 0.06), 0.66, 0.95);
            buona = false;
        }

        var riconosciute = 0;
        for (var j = 0; j < accettate.Count; j++)
        {
            var altre = accettate.Where((_, k) => k != j).Append(canonica).Distinct();
            if (altre.Max(b => SomiglianzaChiavi(accettate[j], b)) >= soglia) riconosciute++;
        }
        // Le varianti si tengono solo se la soglia le separa dalle parole simili: è il controllo di sicurezza finale.
        var sicure = varianti.Where(v => Altrui.All(a =>
            SomiglianzaChiavi(Chiave(a), v) - CostoUnione * (Normalizza(a).Split(' ').Length - 1) < soglia)).ToList();

        var esito = new ParolaPersonale(parola, sicure, Math.Round(soglia, 3), sentiti.Count, riconosciute, DateTimeOffset.Now);
        var frase = sentiti.Count == 0
            ? "Non c'è nessuna registrazione: uso la parola così com'è scritta."
            : "Ti riconosco in " + riconosciute + " registrazioni su " + sentiti.Count + ". Soglia " + Testo.FormatoVirgola(soglia) + ".";
        if (sentiti.Count > 0 && accettate.Count < sentiti.Count)
            frase += " " + (sentiti.Count - accettate.Count) + " le ho scartate: Parakeet ha capito un'altra cosa.";
        if (!buona && sentiti.Count > 0)
            frase += " Questo fallo tu: rifai qualche registrazione in un posto più silenzioso, dicendo «" + parola + "» come lo dici di solito.";
        else if (sentiti.Count is > 0 and < 5)
            frase += " Con 5 o più registrazioni ti sento meglio.";
        return new EsitoTaratura(esito, Math.Round(minimoTuo, 3), Math.Round(massimoAltrui, 3), buona, frase);
    }

    public static string FileParola(string? radiceDati = null) => Path.Combine(Percorsi.Orecchio(radiceDati), "parola.json");

    public static string CartellaRegistrazioni(string? radiceDati = null) => Path.Combine(Percorsi.Orecchio(radiceDati), "registrazioni");

    static readonly JsonSerializerOptions Opzioni = new() { WriteIndented = true };

    /// <summary>La parola salvata, se è la stessa che è nelle impostazioni. Se no la parola com'è scritta, senza taratura.</summary>
    public static ParolaPersonale Carica(string parola, string? radiceDati = null)
    {
        var diSerie = ParolaPersonale.DiSerie(parola);
        try
        {
            var file = FileParola(radiceDati);
            if (!File.Exists(file)) return diSerie;
            var letta = JsonSerializer.Deserialize<ParolaPersonale>(File.ReadAllText(file));
            if (letta is null || Chiave(letta.Parola) != Chiave(diSerie.Parola)) return diSerie;
            var soglia = double.IsFinite(letta.Soglia) ? Math.Clamp(letta.Soglia, 0.6, 0.98) : SogliaDiSerie;
            return letta with { Parola = diSerie.Parola, Soglia = soglia, Varianti = letta.Varianti ?? [] };
        }
        catch { return diSerie; }
    }

    public static void Salva(ParolaPersonale parola, string? radiceDati = null)
    {
        var file = FileParola(radiceDati);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temporaneo = file + ".tmp";
        File.WriteAllText(temporaneo, JsonSerializer.Serialize(parola, Opzioni));
        File.Move(temporaneo, file, overwrite: true);
    }
}
