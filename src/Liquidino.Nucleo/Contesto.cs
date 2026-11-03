namespace Liquidino.Nucleo;

/// <summary>Numeri del PC, letti all'avvio. La scelta del contesto sta qui, la misura vera la fa la finestra.</summary>
public sealed record InfoMacchina(long RamByte, long? VramByte)
{
    public static long Gb(double giga) => (long)(giga * 1024d * 1024d * 1024d);
}

public sealed record Contesti(int Mani, int Omni, int D1, string Frase);

/// <summary>
/// Sceglie il contesto. Obiettivo 128K per LFM3-1.7B quando la macchina somiglia
/// al PC di prova (RTX 4060 da 8 GB e tanta RAM). Omni resta entro i suoi 32K.
/// La cache KV q4 e il flash-attention si accendono sempre, nel preset del regista.
/// </summary>
public static class SceltaContesto
{
    public const int ObiettivoMani = 131072;

    public static Contesti Scegli(InfoMacchina info)
    {
        var ram = info.RamByte;
        var vram = info.VramByte;
        if (vram is long tanta && tanta >= InfoMacchina.Gb(7.5) && ram >= InfoMacchina.Gb(24))
        {
            return new Contesti(ObiettivoMani, 32768, 16384,
                "Ho misurato il PC: c'è spazio. Le mani tengono 128K di contesto, Omni 32K, d1 16K. La memoria del contesto è compressa (q4) e uso il flash-attention.");
        }
        if (vram is long media && media >= InfoMacchina.Gb(6) || ram >= InfoMacchina.Gb(16))
        {
            return new Contesti(32768, 16384, 8192,
                "Ho misurato il PC e tengo un contesto medio, così la scheda video non si riempie. Le mani a 32K, Omni a 16K, d1 a 8K.");
        }
        var perche = vram is null
            ? "Non sono riuscito a leggere la scheda video, parto con un contesto corto (8K) per non strafare."
            : "Il PC è piccolo per il contesto lungo: parto da 8K, così resto leggero.";
        return new Contesti(8192, 8192, 8192, perche);
    }
}
