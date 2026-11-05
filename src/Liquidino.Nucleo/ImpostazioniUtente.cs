using System.Text.Json;
using System.Text.Json.Serialization;

namespace Liquidino.Nucleo;

/// <summary>Scelte del primo avvio e del seguito. File leggibile, si può aprire con un editor.</summary>
public sealed class ImpostazioniUtente
{
    public bool PrimoAvvioCompletato { get; set; }
    public string NomeUtente { get; set; } = "";
    public string Carattere { get; set; } = "calmo";
    /// <summary>female oppure male, come le vuole l'endpoint /tts.</summary>
    public string Voce { get; set; } = "female";
    public string CartellaMotore { get; set; } = Percorsi.CartellaProvaCammo;
    public int Porta { get; set; } = 8094;
    public bool CicloAutonomo { get; set; } = true;
    public int CicloMinuti { get; set; } = 60;
    public int OraNotteInizio { get; set; } = 23;
    public int OraNotteFine { get; set; } = 7;
    public int GiorniAmicizia { get; set; } = 14;
    public DateTimeOffset PrimoAvvio { get; set; } = DateTimeOffset.Now;
    public bool PermessoPc { get; set; }
    public bool CompagniaVocale { get; set; } = true;
    /// <summary>Se Omni non c'è o non parla, leggo con le voci italiane di Windows.</summary>
    public bool VoceWindows { get; set; } = true;
    public int CicliFatti { get; set; }
    public double? AvatarX { get; set; }
    public double? AvatarY { get; set; }
    public double AvatarScala { get; set; } = 1;
    public string AvatarOggetto { get; set; } = "cassetta";
    public bool AvatarCambioAuto { get; set; } = true;
    public int ContestoMani { get; set; } = 8192;
    public int ContestoOmni { get; set; } = 8192;
    public int ContestoD1 { get; set; } = 8192;
    public bool ContestoMisurato { get; set; }
    public string FraseContesto { get; set; } = "";
    public string VersioneNovitaViste { get; set; } = "";
    /// <summary>La parola per svegliarlo con le Orecchie accese.</summary>
    public string ParolaSveglia { get; set; } = Nucleo.ParolaSveglia.DiSerie;
    /// <summary>Le Orecchie si riaccendono all'avvio se erano accese.</summary>
    public bool OrecchieAccese { get; set; }
    /// <summary>Con le Orecchie accese sente anche l'audio del PC (senza la sua voce).</summary>
    public bool AscoltaPc { get; set; } = true;
    /// <summary>Il microfono da usare; vuoto vuol dire quello predefinito di Windows.</summary>
    public string Microfono { get; set; } = "";
    /// <summary>In chiamata la sua voce va anche nel microfono, attraverso VB-CABLE. Spento finché non lo accendi tu.</summary>
    public bool VoceNelMicrofono { get; set; }
    /// <summary>Quanto forte si sente lui nel microfono, in percento (50-200).</summary>
    public int VolumeVoceMicrofono { get; set; } = 100;

    [JsonIgnore]
    public string PercorsoFile { get; set; } = "";

    /// <summary>Se il file era rovinato, qui c'è la copia messa da parte. Si riparte dalle scelte di serie.</summary>
    [JsonIgnore]
    public string? CopiaRovinata { get; private set; }

    public static readonly string[] Caratteri = ["allegro", "calmo", "ironico", "pazzerello", "saggio"];

    public static string DescrizioneCarattere(string carattere) => carattere switch
    {
        "allegro" => "Sei allegro: frasi corte, luce, un sorriso. Non esageri e non fai il pagliaccio.",
        "ironico" => "Sei ironico con gentilezza. Una punta di spirito, mai cattivo, mai sopra le righe.",
        "pazzerello" => "Sei un po' pazzerello: idee storte e tenere, un filo fuori dalle righe, sempre affettuoso.",
        "saggio" => "Sei saggio: poche frasi, chiare, come un amico più grande. Niente prediche.",
        _ => "Sei calmo: parli piano, poche parole, rassicurante. Non riempi il silenzio per forza."
    };

    public bool InAmicizia(DateTimeOffset ora) =>
        (ora - PrimoAvvio).TotalDays < Math.Max(1, GiorniAmicizia);

    public bool ENotte(DateTimeOffset ora)
    {
        var h = ora.Hour;
        var inizio = Math.Clamp(OraNotteInizio, 0, 23);
        var fine = Math.Clamp(OraNotteFine, 0, 23);
        return inizio == fine ? false : inizio < fine ? h >= inizio && h < fine : h >= inizio || h < fine;
    }

    public void ApplicaContesto(Contesti scelti)
    {
        ContestoMani = scelti.Mani;
        ContestoOmni = scelti.Omni;
        ContestoD1 = scelti.D1;
        FraseContesto = scelti.Frase;
        ContestoMisurato = true;
    }

    public static ImpostazioniUtente Carica(string? radice = null)
    {
        var percorso = Percorsi.FileImpostazioni(radice);
        ImpostazioniUtente? imp = null;
        string? rovinata = null;
        if (File.Exists(percorso))
        {
            try
            {
                imp = JsonSerializer.Deserialize<ImpostazioniUtente>(File.ReadAllText(percorso), JsonIo.Opzioni);
            }
            catch
            {
                imp = null;
            }
            if (imp is null) rovinata = MettiDaParte(percorso);
        }
        imp ??= new ImpostazioniUtente();
        imp.CopiaRovinata = rovinata;
        imp.PercorsoFile = percorso;
        if (!Caratteri.Contains(imp.Carattere)) imp.Carattere = "calmo";
        if (imp.Voce is not ("female" or "male")) imp.Voce = "female";
        if (imp.Porta is < 1 or > 65535) imp.Porta = 8094;
        if (imp.CicloMinuti < 5) imp.CicloMinuti = 5;
        if (string.IsNullOrWhiteSpace(imp.CartellaMotore)) imp.CartellaMotore = Percorsi.CartellaProvaCammo;
        imp.ParolaSveglia = string.IsNullOrWhiteSpace(imp.ParolaSveglia) ? Nucleo.ParolaSveglia.DiSerie : imp.ParolaSveglia.Trim();
        if (imp.ParolaSveglia.Length > 40) imp.ParolaSveglia = imp.ParolaSveglia[..40];
        imp.Microfono ??= "";
        imp.VolumeVoceMicrofono = Math.Clamp(imp.VolumeVoceMicrofono, 50, 200);
        return imp;
    }

    /// <summary>Rinomina il file che non si legge, così non si perde e non si riscrive sopra.</summary>
    static string? MettiDaParte(string percorso)
    {
        try
        {
            var copia = percorso + ".rovinato-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var n = 1;
            while (File.Exists(copia)) copia = percorso + ".rovinato-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + n++;
            File.Move(percorso, copia);
            return copia;
        }
        catch
        {
            return null;
        }
    }

    readonly object _scrittura = new();

    public void Salva()
    {
        if (string.IsNullOrWhiteSpace(PercorsoFile))
            PercorsoFile = Percorsi.FileImpostazioni();
        var json = JsonSerializer.Serialize(this, JsonIo.Opzioni);
        lock (_scrittura)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PercorsoFile)!);
            File.WriteAllText(PercorsoFile, json);
        }
    }
}

public static class JsonIo
{
    public static readonly JsonSerializerOptions Opzioni = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Una riga sola, per il registro JSONL.</summary>
    public static readonly JsonSerializerOptions Riga = new(Opzioni) { WriteIndented = false };
}
