namespace Liquidino.Nucleo;

public enum StatoCompagno
{
    Dorme,
    Ascolta,
    Pensa,
    Parla,
    Sveglio
}

public static class Stati
{
    public static string Etichetta(StatoCompagno stato) => stato switch
    {
        StatoCompagno.Dorme => "dorme",
        StatoCompagno.Ascolta => "ascolta",
        StatoCompagno.Pensa => "pensa",
        StatoCompagno.Parla => "parla",
        _ => "sveglio"
    };
}
