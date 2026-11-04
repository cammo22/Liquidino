using Liquidino.Nucleo;

namespace Liquidino.Prove;

static class Aiuti
{
    public static string Temporanea()
    {
        var dir = Path.Combine(Path.GetTempPath(), "liquidino-prove-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static ImpostazioniUtente Impostazioni(string radice) =>
        new() { PercorsoFile = Path.Combine(radice, "impostazioni.json") };

    public static string RegistroDiOggi(string radice)
    {
        var dir = Percorsi.Registro(radice);
        return Directory.Exists(dir)
            ? string.Join("\n", Directory.GetFiles(dir, "*.jsonl").Select(File.ReadAllText))
            : "";
    }
}

/// <summary>Regista senza motore: tiene il conto di chi è acceso e delle richieste ricevute.</summary>
sealed class RegistaFinto : IRegista
{
    public bool MotorePresente { get; set; }
    public HashSet<RuoloModello> Presenti { get; } = [];
    public List<RuoloModello> AccesiLista { get; } = [];
    public IReadOnlyCollection<RuoloModello> Accesi => AccesiLista;
    public Func<string, SceltaAzione?>? SuDecidi { get; set; }
    public Func<RichiestaChat, RispostaChat?>? SuCompleta { get; set; }
    public List<RichiestaChat> Richieste { get; } = [];

    public bool Presente(RuoloModello ruolo) => Presenti.Contains(ruolo);
    public string? UltimoErrore => null;
    public string Riassunto() => Descrizione();
    public string Descrizione() => "finto";

    public Task<bool> Assicura(IReadOnlyList<RuoloModello> ruoli, CancellationToken ct)
    {
        if (ruoli.Distinct().Count() > 2) throw new InvalidOperationException("Mai più di due modelli insieme.");
        AccesiLista.Clear();
        AccesiLista.AddRange(ruoli);
        return Task.FromResult(MotorePresente && ruoli.All(Presente));
    }

    public Task ScaricaTutto(CancellationToken ct)
    {
        AccesiLista.Clear();
        return Task.CompletedTask;
    }

    public Task Ferma() => ScaricaTutto(CancellationToken.None);

    public Task<SceltaAzione?> Decidi(string stato, CatalogoAzioni catalogo, CancellationToken ct) =>
        Task.FromResult(SuDecidi?.Invoke(stato));

    public Task<RispostaChat?> Completa(RichiestaChat richiesta, CancellationToken ct)
    {
        Richieste.Add(richiesta);
        return Task.FromResult(SuCompleta is null ? new RispostaChat("{}", null) : SuCompleta(richiesta));
    }

    public Task<string?> Trascrivi(byte[] wav, CancellationToken ct) => Task.FromResult<string?>("ciao");
    public Task<byte[]?> Sintetizza(string testo, string voce, CancellationToken ct) => Task.FromResult<byte[]?>(null);
}
