namespace Liquidino.Nucleo;

/// <summary>Da dove arriva una frase: il tuo microfono, l'audio del PC o la chiamata.</summary>
public enum FonteAscolto { Tu, Pc, Chiamata }

public sealed record RigaSentita(DateTimeOffset Quando, FonteAscolto Fonte, string Chi, string Testo);

/// <summary>
/// Quello che ha sentito di recente, solo in memoria: niente si scrive su disco e tutto si scorda dopo pochi minuti.
/// Serve a rispondere a «cosa stavano dicendo?» e a seguire una chiamata.
/// </summary>
public sealed class RegistroAscolto
{
    readonly object _uno = new();
    readonly List<RigaSentita> _righe = [];
    public TimeSpan Durata { get; init; } = TimeSpan.FromMinutes(3);
    public int MassimoRighe { get; init; } = 60;

    public void Aggiungi(RigaSentita riga)
    {
        if (string.IsNullOrWhiteSpace(riga.Testo)) return;
        lock (_uno)
        {
            _righe.Add(riga with { Testo = riga.Testo.Trim() });
            Pota(riga.Quando);
        }
    }

    void Pota(DateTimeOffset ora)
    {
        _righe.RemoveAll(r => ora - r.Quando > Durata);
        if (_righe.Count > MassimoRighe) _righe.RemoveRange(0, _righe.Count - MassimoRighe);
    }

    public IReadOnlyList<RigaSentita> Righe(DateTimeOffset ora)
    {
        lock (_uno)
        {
            Pota(ora);
            return _righe.ToList();
        }
    }

    public void Svuota()
    {
        lock (_uno) _righe.Clear();
    }

    /// <summary>Le ultime righe, dalle più vecchie, tagliate dall'inizio se superano il massimo di caratteri.</summary>
    public string Testo(DateTimeOffset ora, int massimoCaratteri = 900)
    {
        var righe = Righe(ora).Select(r => "[" + Etichetta(r) + "] " + r.Testo).ToList();
        var uscita = new List<string>();
        var totale = 0;
        for (var i = righe.Count - 1; i >= 0; i--)
        {
            if (totale + righe[i].Length + 1 > massimoCaratteri) break;
            uscita.Insert(0, righe[i]);
            totale += righe[i].Length + 1;
        }
        return string.Join("\n", uscita);
    }

    static string Etichetta(RigaSentita r) => r.Fonte switch
    {
        FonteAscolto.Tu => "tu",
        FonteAscolto.Chiamata => "in chiamata su " + r.Chi,
        _ => "audio del PC"
    };
}

public enum CosaFare { Niente, Comando, Svegliato, RispostaChiamata }

public sealed record DecisioneOrecchio(CosaFare Cosa, string Testo, Risveglio? Risveglio = null)
{
    public static readonly DecisioneOrecchio Niente = new(CosaFare.Niente, "");
}

/// <summary>
/// Decide cosa fare di ogni frase trascritta.
/// - Dal tuo microfono: solo se contiene il nome. «babbasone» da solo apre 8 secondi in cui la frase dopo è per lui.
/// - Dall'audio del PC: si ricorda e basta. Non sveglia mai e non dà ordini.
/// - Dalla chiamata: si ricorda; se qualcuno lo chiama per nome, risponde a voce, ma solo con le parole: niente mani.
/// Fuori dalla chiamata, quello che dici senza il nome non si ricorda: non è per lui.
/// </summary>
public sealed class Orecchie
{
    readonly Func<ParolaPersonale> _parola;
    public RegistroAscolto Registro { get; }
    public TimeSpan AttesaDopoNome { get; init; } = TimeSpan.FromSeconds(8);
    public bool InChiamata { get; set; }
    DateTimeOffset _attesaFino = DateTimeOffset.MinValue;

    public Orecchie(Func<ParolaPersonale> parola, RegistroAscolto? registro = null)
    {
        _parola = parola;
        Registro = registro ?? new RegistroAscolto();
    }

    public bool InAttesa(DateTimeOffset ora) => ora < _attesaFino;

    /// <summary>Quanto indietro si guarda per l'eco, e quanto deve somigliare.</summary>
    public TimeSpan FinestraEco { get; init; } = TimeSpan.FromSeconds(8);
    public double SogliaEco { get; init; } = 0.75;

    public bool GiaDallaChiamata(string testo, DateTimeOffset ora)
    {
        if (ParolaSveglia.Chiave(testo).Length < 4) return false;
        var chiamata = Registro.Righe(ora)
            .Where(r => r.Fonte == FonteAscolto.Chiamata && ora - r.Quando <= FinestraEco)
            .Select(r => r.Testo).ToList();
        return chiamata.Count > 0 && ParolaSveglia.Contenuto(testo, string.Join(" ", chiamata)) >= SogliaEco;
    }

    /// <summary>
    /// Il microfono ha sentito quello che è appena uscito dalle casse (un video, un amico in chiamata)?
    /// Allora non è la tua voce: non sveglia e non comanda.
    /// </summary>
    public bool Eco(string testo, DateTimeOffset ora)
    {
        if (ParolaSveglia.Chiave(testo).Length < 6) return false;
        var recenti = Registro.Righe(ora)
            .Where(r => r.Fonte != FonteAscolto.Tu && ora - r.Quando <= FinestraEco && r.Quando - ora <= TimeSpan.FromSeconds(2))
            .Select(r => r.Testo).ToList();
        if (recenti.Count == 0) return false;
        return ParolaSveglia.Contenuto(testo, string.Join(" ", recenti)) >= SogliaEco;
    }

    public void ChiudiAttesa() => _attesaFino = DateTimeOffset.MinValue;

    public DecisioneOrecchio Sentito(FonteAscolto fonte, string chi, string testo, DateTimeOffset ora)
    {
        testo = (testo ?? "").Trim();
        if (ParolaSveglia.Normalizza(testo).Length == 0) return DecisioneOrecchio.Niente;
        switch (fonte)
        {
            case FonteAscolto.Pc:
                // L'audio di tutto il PC contiene anche la chiamata: se è già arrivata da lì, non si conta due volte.
                if (GiaDallaChiamata(testo, ora)) return DecisioneOrecchio.Niente;
                Registro.Aggiungi(new RigaSentita(ora, fonte, chi, testo));
                return DecisioneOrecchio.Niente;
            case FonteAscolto.Chiamata:
            {
                Registro.Aggiungi(new RigaSentita(ora, fonte, chi, testo));
                if (!InChiamata) return DecisioneOrecchio.Niente;
                var trovato = ParolaSveglia.Trova(testo, _parola());
                return trovato is null
                    ? DecisioneOrecchio.Niente
                    : new DecisioneOrecchio(CosaFare.RispostaChiamata, trovato.Resto.Length > 0 ? trovato.Resto : testo, trovato);
            }
            default:
            {
                if (Eco(testo, ora)) return DecisioneOrecchio.Niente;
                if (InChiamata) Registro.Aggiungi(new RigaSentita(ora, fonte, chi, testo));
                if (InAttesa(ora))
                {
                    _attesaFino = DateTimeOffset.MinValue;
                    var dentro = ParolaSveglia.Trova(testo, _parola());
                    var comando = dentro is { Resto.Length: > 0 } ? dentro.Resto : dentro is null ? testo : "";
                    if (comando.Length > 0) return new DecisioneOrecchio(CosaFare.Comando, comando, dentro);
                    _attesaFino = ora + AttesaDopoNome;
                    return new DecisioneOrecchio(CosaFare.Svegliato, "", dentro);
                }
                var trovato = ParolaSveglia.Trova(testo, _parola());
                if (trovato is null) return DecisioneOrecchio.Niente;
                if (ParolaSveglia.Normalizza(trovato.Resto).Length == 0)
                {
                    _attesaFino = ora + AttesaDopoNome;
                    return new DecisioneOrecchio(CosaFare.Svegliato, "", trovato);
                }
                return new DecisioneOrecchio(CosaFare.Comando, trovato.Resto, trovato);
            }
        }
    }
}

/// <summary>I programmi da chiamata che le orecchie sanno ascoltare da soli, con la cattura dell'audio di un programma.</summary>
public static class AppChiamata
{
    public static readonly (string Processo, string Nome)[] Note =
    [
        ("discord", "Discord"), ("discordptb", "Discord PTB"), ("discordcanary", "Discord Canary"),
        ("ms-teams", "Teams"), ("teams", "Teams"), ("zoom", "Zoom"), ("telegram", "Telegram"),
        ("whatsapp", "WhatsApp"), ("signal", "Signal")
    ];

    /// <summary>Il primo programma da chiamata aperto, nell'ordine dell'elenco (Discord prima di tutti).</summary>
    public static (int Pid, string Nome)? Scegli(IEnumerable<(int Pid, string Processo)> processi)
    {
        var aperti = processi.ToList();
        foreach (var (processo, nome) in Note)
        {
            var trovato = aperti.Where(p => string.Equals(p.Processo, processo, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Pid).FirstOrDefault();
            if (trovato.Processo is not null) return (trovato.Pid, nome);
        }
        return null;
    }
}

/// <summary>
/// VB-CABLE (VB-Audio, donationware): un microfono finto. Liquidino suona in «CABLE Input» la tua voce più la sua,
/// e nel programma della chiamata scegli «CABLE Output» come microfono. Non si ridistribuisce: si installa dal sito ufficiale.
/// </summary>
public static class CavoVirtuale
{
    public const string PaginaUfficiale = "https://vb-audio.com/Cable/";

    public static bool ECavo(string nome) =>
        nome.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase)
        || nome.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase)
        || nome.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase);

    /// <summary>L'uscita su cui suonare: «CABLE Input». Null se VB-CABLE non è installato.</summary>
    public static string? Ingresso(IEnumerable<string> uscite) =>
        uscite.FirstOrDefault(n => n.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Il tuo microfono vero: quello scelto, se c'è ancora; se no quello predefinito di Windows; mai l'uscita del cavo,
    /// che durante la chiamata ha dentro anche la voce di Liquidino.
    /// </summary>
    public static string? Microfono(IReadOnlyList<string> ingressi, string? predefinito, string? scelto)
    {
        if (!string.IsNullOrWhiteSpace(scelto) && ingressi.Contains(scelto) && !ECavo(scelto)) return scelto;
        if (!string.IsNullOrWhiteSpace(predefinito) && ingressi.Contains(predefinito) && !ECavo(predefinito)) return predefinito;
        return ingressi.FirstOrDefault(n => !ECavo(n));
    }
}
