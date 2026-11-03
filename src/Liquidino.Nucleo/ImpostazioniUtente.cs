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

    [JsonIgnore]
    public string PercorsoFile { get; set; } = "";

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
        ImpostazioniUtente imp;
        try
        {
            if (File.Exists(percorso))
            {
                var letto = JsonSerializer.Deserialize<ImpostazioniUtente>(File.ReadAllText(percorso), JsonIo.Opzioni);
                imp = letto ?? new ImpostazioniUtente();
            }
            else imp = new ImpostazioniUtente();
        }
        catch
        {
            imp = new ImpostazioniUtente();
        }
        imp.PercorsoFile = percorso;
        if (!Caratteri.Contains(imp.Carattere)) imp.Carattere = "calmo";
        if (imp.Voce is not ("female" or "male")) imp.Voce = "female";
        if (imp.Porta is < 1 or > 65535) imp.Porta = 8094;
        if (imp.CicloMinuti < 5) imp.CicloMinuti = 5;
        if (string.IsNullOrWhiteSpace(imp.CartellaMotore)) imp.CartellaMotore = Percorsi.CartellaProvaCammo;
        return imp;
    }

    public void Salva()
    {
        if (string.IsNullOrWhiteSpace(PercorsoFile))
            PercorsoFile = Percorsi.FileImpostazioni();
        Directory.CreateDirectory(Path.GetDirectoryName(PercorsoFile)!);
        var json = JsonSerializer.Serialize(this, JsonIo.Opzioni);
        File.WriteAllText(PercorsoFile, json);
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
}
