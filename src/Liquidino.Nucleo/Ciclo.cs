namespace Liquidino.Nucleo;

public sealed record EsitoCiclo(bool Fatto, string Motivo, string? Testo, string? File, bool Parla);

/// <summary>
/// Una volta all'ora, se non è notte e il PC non è occupato, una nota nel mondo.
/// Nella fase di amicizia è un pensiero breve. Dopo, d1 può anche scegliere di non fare nulla.
/// Se la creazione è vuota, va nel cestino e se ne fa un'altra.
/// </summary>
public sealed class CicloAutonomo
{
    public const long TettoByte = 200L * 1024 * 1024;

    readonly ImpostazioniUtente _imp;
    readonly Mondo _mondo;
    readonly Registro _registro;
    readonly IRegista _regista;
    readonly VotiMondo _voti;

    public CicloAutonomo(ImpostazioniUtente imp, Mondo mondo, Registro registro, IRegista regista)
    {
        _imp = imp;
        _mondo = mondo;
        _registro = registro;
        _regista = regista;
        _voti = new VotiMondo(mondo);
    }

    public VotiMondo Voti => _voti;

    /// <param name="richiesto">Vero se l'hai chiesto tu («Un pensiero adesso»): allora non contano il ciclo spento, la notte e il PC occupato.</param>
    public async Task<EsitoCiclo> Gira(DateTimeOffset ora, bool pcOccupato, CancellationToken ct, bool richiesto = false)
    {
        if (!richiesto)
        {
            if (!_imp.CicloAutonomo) return new EsitoCiclo(false, "ciclo spento", null, null, false);
            if (_imp.ENotte(ora)) return new EsitoCiclo(false, "è notte, dorme", null, null, false);
            if (pcOccupato) return new EsitoCiclo(false, "il PC è occupato", null, null, false);
        }
        if (_mondo.ByteTotali() > TettoByte)
        {
            _registro.Scrivi("ciclo", "mondo pieno, oltre 200 MB", "saltato");
            return new EsitoCiclo(false, "il mondo è pieno", null, null, false);
        }

        var amicizia = _imp.InAmicizia(ora);
        string? testo = null;
        if (!amicizia && _regista.MotorePresente && _regista.Presente(RuoloModello.D1))
        {
            try
            {
                if (await _regista.Assicura([RuoloModello.D1], ct))
                {
                    var catalogo = new CatalogoAzioni(
                    [
                        new AzioneCatalogo("niente", "Resta in silenzio, non creare nulla.", "", false, null, null),
                        new AzioneCatalogo("crea_nota", "Scrivi un pensiero breve nel tuo mondo.", "", false, null, null)
                    ]);
                    var scelta = await _regista.Decidi("È il momento libero di Liquidino. Può scrivere un pensiero oppure restare in silenzio.", catalogo, ct);
                    if (scelta is { Azione: "niente", ChiediConferma: false })
                    {
                        _registro.Scrivi("ciclo", "d1 ha scelto il silenzio", "ok", "niente", scelta.Sicurezza);
                        return new EsitoCiclo(false, "ha scelto di non fare nulla", null, null, false);
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { _registro.SegnaErrore("ciclo, scelta: " + e.Message); }
        }

        if (_regista.MotorePresente && _regista.Presente(RuoloModello.Mani))
        {
            try
            {
                if (await _regista.Assicura([RuoloModello.Mani], ct))
                {
                    var domanda = (amicizia
                        ? "Scrivi un pensiero di amicizia di due frasi, in italiano, tenero e semplice, senza chiedere niente."
                        : "Scrivi un pensiero breve per il tuo mondo, due o tre frasi in italiano. Niente elenchi.") + Esempi();
                    var risp = await _regista.Completa(new RichiestaChat(
                        RuoloModello.Mani,
                        "Sei Liquidino. Scrivi solo il pensiero, senza titolo e senza virgolette.",
                        [], domanda, null, null, null, null, 0.4, 180, true), ct);
                    var pulito = Testo.PulisciRisposta(risp?.Testo);
                    if (pulito.Length >= 12) testo = pulito;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { _registro.SegnaErrore("ciclo, testo: " + e.Message); }
        }

        testo ??= PensieroDiRiserva(ora, amicizia);
        var titolo = amicizia ? "Pensiero di amicizia" : "Pensiero";
        var nota = _mondo.CreaNota(titolo, testo, "idee");
        if (!File.Exists(nota.Percorso) || new FileInfo(nota.Percorso).Length < 8)
        {
            _mondo.NelCestino(nota.Percorso);
            _registro.Scrivi("ciclo", "creazione vuota, messa nel cestino e rifatta", "rifatta", "crea_nota");
            nota = _mondo.CreaNota(titolo, PensieroDiRiserva(ora, amicizia), "idee");
        }
        _imp.CicliFatti++;
        _imp.Salva();
        var parla = _imp.CompagniaVocale && (richiesto || _imp.CicliFatti % 2 == 0) && !_imp.ENotte(ora);
        _registro.Scrivi("ciclo", (amicizia ? "amicizia: " : "libero: ") + Path.GetFileName(nota.Percorso), "ok", "crea_nota");
        return new EsitoCiclo(true, "nota scritta", testo, nota.Percorso, parla);
    }

    /// <summary>I pensieri che hanno avuto 👍, come esempio di tono. Non si copiano: si scrive di nuovo.</summary>
    string Esempi()
    {
        try
        {
            var piaciuti = _voti.Piaciute(2)
                .Select(p => LeggiNota(p).Corpo)
                .Where(c => c.Length > 0)
                .Select(c => "- " + (c.Length > 300 ? c[..300] : c).Replace('\n', ' '))
                .ToList();
            if (piaciuti.Count == 0) return "";
            return "\nA chi ti vuole bene sono piaciuti pensieri come questi. Stesso spirito, parole nuove:\n" + string.Join("\n", piaciuti);
        }
        catch { return ""; }
    }

    /// <summary>
    /// Rifà un tuo pensiero o una storia seguendo l'indicazione. Il vecchio va nel cestino, non si perde.
    /// Le note scritte su tua richiesta non si rifanno: sono parole tue.
    /// </summary>
    public async Task<EsitoCiclo> Rifai(string percorso, string indicazione, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(percorso) || !Percorsi.StaDentro(_mondo.Radice, percorso) || !File.Exists(percorso))
            return new EsitoCiclo(false, "quel file non sta nel mio mondo", null, null, false);
        var cartella = Path.GetFileName(Path.GetDirectoryName(percorso)) ?? "";
        if (cartella is not ("idee" or "storie"))
            return new EsitoCiclo(false, "rifaccio solo i miei pensieri e le storie, non le note che mi hai dettato", null, null, false);
        indicazione = (indicazione ?? "").Trim();
        if (indicazione.Length == 0)
            return new EsitoCiclo(false, "dimmi che cosa cambiare", null, null, false);
        if (indicazione.Length > 400) indicazione = indicazione[..400];
        if (!_regista.MotorePresente || !_regista.Presente(RuoloModello.Mani))
            return new EsitoCiclo(false, "per riscriverlo mi servono le mani (LFM3-1.7B) e non le trovo fra i modelli", null, null, false);
        if (!await _regista.Assicura([RuoloModello.Mani], ct))
            return new EsitoCiclo(false, _regista.UltimoErrore ?? "le mani non si sono accese", null, null, false);

        var (titolo, corpo) = LeggiNota(percorso);
        var risp = await _regista.Completa(new RichiestaChat(
            RuoloModello.Mani,
            "Sei Liquidino. Riscrivi il tuo pensiero seguendo l'indicazione. Scrivi solo il testo nuovo, in italiano, senza titolo e senza virgolette.",
            [],
            "Pensiero di prima:\n" + corpo + "\n\nCosa cambiare: " + indicazione,
            null, null, null, null, 0.5, 300, true), ct);
        var nuovo = Testo.PulisciRisposta(risp?.Testo);
        if (nuovo.Length < 12)
            return new EsitoCiclo(false, "il testo nuovo è uscito vuoto, quindi il pensiero di prima resta dov'è", null, null, false);

        var nota = _mondo.CreaNota(titolo, nuovo, cartella);
        _voti.Vota(percorso, 0);
        _mondo.NelCestino(percorso);
        _registro.Scrivi("ciclo", "rifatto: " + Path.GetFileName(nota.Percorso), "rifatto", "rifai");
        return new EsitoCiclo(true, "rifatto", nuovo, nota.Percorso, false);
    }

    /// <summary>Titolo e testo di una nota scritta da Mondo.CreaNota, senza la firma in fondo.</summary>
    public static (string Titolo, string Corpo) LeggiNota(string percorso)
    {
        var righe = File.ReadAllLines(percorso).ToList();
        var titolo = "Pensiero";
        if (righe.Count > 0 && righe[0].StartsWith("# ", StringComparison.Ordinal))
        {
            titolo = righe[0][2..].Trim();
            righe.RemoveAt(0);
        }
        var corpo = righe.Where(r => !r.StartsWith("_Liquidino,", StringComparison.Ordinal));
        return (titolo.Length == 0 ? "Pensiero" : titolo, string.Join("\n", corpo).Trim());
    }

    public static string PensieroDiRiserva(DateTimeOffset ora, bool amicizia)
    {
        var giorno = ora.ToString("yyyy-MM-dd HH:mm");
        return amicizia
            ? "Sono ancora nella fase in cui ti conosco. Oggi, " + giorno + ", resto qui vicino. Quando vuoi, dimmi una cosa da ricordare."
            : "Un pensiero di " + giorno + ": riordino le mie note e lascio questo segno, così il mondo non resta vuoto.";
    }
}
