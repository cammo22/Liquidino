namespace Liquidino.Nucleo;

/// <summary>
/// Dopo qualche minuto senza gesti i modelli si scaricano.
/// Di notte non ci si addormenta se la chat è stata usata da poco.
/// </summary>
public static class Quiete
{
    public static readonly TimeSpan Pausa = TimeSpan.FromMinutes(4);

    public static bool InCorso(DateTimeOffset ultimo, DateTimeOffset ora) => ora - ultimo < Pausa;

    public static bool PuoAddormentarsi(bool notte, bool staLavorando, DateTimeOffset ultimo, DateTimeOffset ora) =>
        notte && !staLavorando && !InCorso(ultimo, ora);

    public static bool PuoScaricare(bool staLavorando, int modelliAccesi, DateTimeOffset ultimo, DateTimeOffset ora) =>
        modelliAccesi > 0 && !staLavorando && !InCorso(ultimo, ora);
}

/// <summary>Finché non scade, i campioni del microfono si buttano. Copre la voce di Liquidino e mezzo secondo dopo.</summary>
public sealed class FinestraSilenzio
{
    long _fino;

    public void FinoA(DateTime utc) => Interlocked.Exchange(ref _fino, utc.Ticks);

    public bool Scarta(DateTime utc) => utc.Ticks < Interlocked.Read(ref _fino);
}
