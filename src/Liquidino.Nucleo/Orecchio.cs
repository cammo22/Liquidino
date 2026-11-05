namespace Liquidino.Nucleo;

public sealed record StatoOrecchio(bool Pronto, string Messaggio);

/// <summary>
/// Cosa dire delle orecchie: se Parakeet c'è, e se la parola è tarata sulla tua voce.
/// Il rilevatore della voce non ha modelli: guarda l'energia del suono.
/// </summary>
public static class OrecchioLeggero
{
    public static StatoOrecchio Stato(string parola, string cartellaModelli, string? radiceDati = null)
    {
        parola = string.IsNullOrWhiteSpace(parola) ? ParolaSveglia.DiSerie : parola.Trim();
        if (!CatalogoOrecchio.Parakeet.Presente(cartellaModelli))
            return new StatoOrecchio(false,
                "Per sentirmi chiamare «" + parola + "» mi serve Parakeet: lo scarichi dalla pagina Modelli. Intanto svegliami con un clic, con «Parla» o con Ctrl+Spazio.");
        var tarata = ParolaSveglia.Carica(parola, radiceDati);
        return new StatoOrecchio(true, tarata.Tarata
            ? "Accendi le Orecchie (👂) e chiamami «" + parola + "»: la parola è tarata su " + tarata.Registrazioni + " tue registrazioni."
            : "Accendi le Orecchie (👂) e chiamami «" + parola + "». Se registri la parola qualche volta (Mainframe → Orecchie), ti sento meglio.");
    }
}
