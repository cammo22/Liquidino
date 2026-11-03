namespace Liquidino.Nucleo;

public sealed record StatoOrecchio(bool Pronto, string Messaggio);

/// <summary>
/// Posto per l'orecchio leggero sulla CPU: Silero VAD (codice e modello MIT)
/// e openWakeWord (codice Apache-2.0). La parola «Ehi Liquidino» va addestrata da noi:
/// i modelli già fatti di openWakeWord sono inglesi e non commerciali, qui non si usano.
/// Nel prototipo l'attivazione è il clic e il tasto. Quando i due file onnx ci saranno, questo dice che è pronto.
/// </summary>
public static class OrecchioLeggero
{
    public const string Parola = "Ehi Liquidino";

    public static StatoOrecchio Stato(string? radiceDati = null)
    {
        var dir = Percorsi.Orecchio(radiceDati);
        var vad = Path.Combine(dir, "silero_vad.onnx");
        var wake = Path.Combine(dir, "ehi-liquidino.onnx");
        if (File.Exists(vad) && File.Exists(wake))
            return new StatoOrecchio(true, "L'orecchio è pronto per «" + Parola + "».");
        return new StatoOrecchio(false,
            "La parola «" + Parola + "» non è ancora addestrata. Per ora svegliami con un clic, con «Parla» o con Ctrl+Spazio.");
    }
}
