using SherpaOnnx;

namespace Liquidino.Nucleo;

/// <summary>Trascrive una frase a 16 kHz mono in testo.</summary>
public interface ITrascrittore
{
    string Trascrivi(float[] campioni16k);
}

/// <summary>
/// Parakeet TDT 0.6B v3 (NVIDIA, CC-BY-4.0) in int8, con sherpa-onnx (Apache-2.0), sulla CPU.
/// Capisce l'italiano e altre 24 lingue europee. Una frase di qualche secondo si trascrive in una frazione di secondo.
/// </summary>
public sealed class TrascrittoreParakeet : ITrascrittore, IDisposable
{
    readonly OfflineRecognizer _riconoscitore;
    readonly object _uno = new();
    bool _chiuso;

    public TrascrittoreParakeet(string cartellaModelli, int fili = 0)
    {
        var cartella = CatalogoOrecchio.Parakeet.Cartella(cartellaModelli);
        if (!CatalogoOrecchio.Parakeet.Presente(cartellaModelli))
            throw new FileNotFoundException("Parakeet non è nella cartella dei modelli", cartella);
        var config = new OfflineRecognizerConfig();
        config.FeatConfig.SampleRate = Campioni.Frequenza;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Transducer.Encoder = Path.Combine(cartella, "encoder.int8.onnx");
        config.ModelConfig.Transducer.Decoder = Path.Combine(cartella, "decoder.int8.onnx");
        config.ModelConfig.Transducer.Joiner = Path.Combine(cartella, "joiner.int8.onnx");
        config.ModelConfig.Tokens = Path.Combine(cartella, "tokens.txt");
        config.ModelConfig.ModelType = "nemo_transducer";
        config.ModelConfig.NumThreads = fili > 0 ? fili : Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
        config.ModelConfig.Provider = "cpu";
        config.ModelConfig.Debug = 0;
        config.DecodingMethod = "greedy_search";
        _riconoscitore = new OfflineRecognizer(config);
    }

    public string Trascrivi(float[] campioni16k)
    {
        if (campioni16k.Length < Campioni.Frequenza / 10) return "";
        lock (_uno)
        {
            ObjectDisposedException.ThrowIf(_chiuso, this);
            using var flusso = _riconoscitore.CreateStream();
            flusso.AcceptWaveform(Campioni.Frequenza, campioni16k);
            _riconoscitore.Decode(flusso);
            return (flusso.Result.Text ?? "").Trim();
        }
    }

    public void Dispose()
    {
        lock (_uno)
        {
            if (_chiuso) return;
            _chiuso = true;
            _riconoscitore.Dispose();
        }
    }
}

/// <summary>I modelli delle orecchie, nella pagina Modelli accanto agli LFM3.</summary>
public static class CatalogoOrecchio
{
    public static readonly SchedaFissa Parakeet = new(
        "Parakeet v3",
        "Le orecchie. Trascrive sulla CPU quello che dici e quello che suona il PC, anche in italiano, e serve per sentire «babbasone». " +
        "Senza, le Orecchie restano spente e mi svegli col clic o con «Parla».",
        "csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8",
        "parakeet-tdt-0.6b-v3",
        ["encoder.int8.onnx", "decoder.int8.onnx", "joiner.int8.onnx", "tokens.txt"]);
}
