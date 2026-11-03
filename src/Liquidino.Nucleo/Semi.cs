namespace Liquidino.Nucleo;

/// <summary>Crea le cartelle e copia prompt e catalogo, solo se mancano. I file restano tuoi.</summary>
public static class Semi
{
    public static void Prepara(string? radiceDati = null, string? cartellaApp = null)
    {
        radiceDati ??= Percorsi.RadiceDati();
        cartellaApp ??= AppContext.BaseDirectory;
        var mondo = new Mondo(radiceDati);
        mondo.Prepara();
        Directory.CreateDirectory(Percorsi.Orecchio(radiceDati));
        Directory.CreateDirectory(Percorsi.Slot(radiceDati));

        var prompt = Percorsi.Prompt(radiceDati);
        if (!File.Exists(prompt))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(prompt)!);
            var sorgente = Path.Combine(cartellaApp, "prompt", "sistema.md");
            File.WriteAllText(prompt, File.Exists(sorgente) ? File.ReadAllText(sorgente) : Dialogo.PromptDiSerie);
        }

        var catalogo = Percorsi.Catalogo(radiceDati);
        if (!File.Exists(catalogo))
        {
            var sorgente = Path.Combine(cartellaApp, "catalogo", "azioni.json");
            if (File.Exists(sorgente))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(catalogo)!);
                File.Copy(sorgente, catalogo);
            }
            else CatalogoAzioni.Semina(catalogo);
        }
    }
}
