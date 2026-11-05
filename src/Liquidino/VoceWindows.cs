using System.Globalization;
using System.Speech.Synthesis;
using Liquidino.Nucleo;

namespace Liquidino;

/// <summary>
/// Le voci italiane già installate in Windows (SAPI). Offline, niente download.
/// Se nel PC non ce n'è nessuna, Disponibile è falso e resta il testo.
/// </summary>
public static class VoceWindows
{
    static readonly Lazy<bool> _disponibile = new(() =>
    {
        try { return VociItaliane().Count > 0; }
        catch { return false; }
    });

    public static bool Disponibile => _disponibile.Value;

    /// <summary>Il WAV della frase, in memoria. Null se non c'è una voce italiana.</summary>
    public static byte[]? Sintetizza(string testo, string voce)
    {
        if (string.IsNullOrWhiteSpace(testo)) return null;
        using var sintesi = new SpeechSynthesizer();
        var voci = sintesi.GetInstalledVoices()
            .Where(v => v.Enabled && EItaliana(v.VoiceInfo.Culture))
            .Select(v => v.VoiceInfo)
            .ToList();
        var scelta = SceltaVoce.Preferita(voci, v => v.Gender == VoiceGender.Female, voce);
        if (scelta is null) return null;
        sintesi.SelectVoice(scelta.Name);
        using var memoria = new MemoryStream();
        sintesi.SetOutputToWaveStream(memoria);
        sintesi.Speak(testo);
        sintesi.SetOutputToNull();
        return memoria.ToArray();
    }

    public static string Descrizione()
    {
        try
        {
            var voci = VociItaliane();
            return voci.Count == 0
                ? "Nessuna voce italiana di Windows: si aggiunge da Impostazioni di Windows, Ora e lingua, Voce."
                : "Voci italiane di Windows: " + string.Join(", ", voci) + ".";
        }
        catch (Exception e) { return "Le voci di Windows non rispondono: " + e.Message; }
    }

    static List<string> VociItaliane()
    {
        using var sintesi = new SpeechSynthesizer();
        return sintesi.GetInstalledVoices()
            .Where(v => v.Enabled && EItaliana(v.VoiceInfo.Culture))
            .Select(v => v.VoiceInfo.Name)
            .ToList();
    }

    static bool EItaliana(CultureInfo? cultura) =>
        cultura is not null && cultura.TwoLetterISOLanguageName.Equals("it", StringComparison.OrdinalIgnoreCase);
}
