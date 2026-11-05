namespace Liquidino.Nucleo;

/// <summary>I sei modelli che il regista conosce. In memoria, mai più di due insieme.</summary>
public enum RuoloModello
{
    Mani,
    Vl,
    Audio,
    Omni,
    D1,
    D1Omni
}

public sealed record SchedaModello(RuoloModello Ruolo, string Id, string Percorso, string? Mmproj, string NomeFile);

public sealed record Rilevazione(string? Eseguibile, string CartellaModelli, IReadOnlyList<SchedaModello> Modelli);

public static class NomiModello
{
    public static string IdDi(RuoloModello ruolo) => ruolo switch
    {
        RuoloModello.Mani => "liquidino-mani",
        RuoloModello.Vl => "liquidino-vl",
        RuoloModello.Audio => "liquidino-audio",
        RuoloModello.Omni => "liquidino-omni",
        RuoloModello.D1 => "liquidino-d1",
        RuoloModello.D1Omni => "liquidino-d1-omni",
        _ => "liquidino"
    };

    public static string Etichetta(RuoloModello ruolo) => ruolo switch
    {
        RuoloModello.Mani => "mani (LFM3-1.7B)",
        RuoloModello.Vl => "occhi (VL-2B)",
        RuoloModello.Audio => "audio (Audio-2B)",
        RuoloModello.Omni => "voce (Omni-2B)",
        RuoloModello.D1 => "scelta (d1-2B)",
        RuoloModello.D1Omni => "scelta sulla voce (d1-Omni)",
        _ => ruolo.ToString()
    };

    public static int Contesto(RuoloModello ruolo, ImpostazioniUtente imp)
    {
        var n = ruolo switch
        {
            RuoloModello.Mani => imp.ContestoMani,
            RuoloModello.D1 => imp.ContestoD1,
            _ => imp.ContestoOmni
        };
        if (n < 2048) n = 2048;
        return ruolo switch
        {
            RuoloModello.Mani => Math.Min(n, SceltaContesto.ObiettivoMani),
            RuoloModello.Omni => Math.Min(n, 32768),
            RuoloModello.D1 or RuoloModello.D1Omni => Math.Min(n, 32768),
            _ => Math.Min(n, 32768)
        };
    }
}

/// <summary>Trova llama-server (o llama.exe) e i GGUF già scaricati, senza inventarne.</summary>
public static class RilevatoreModelli
{
    public static Rilevazione Cerca(string cartellaConfigurata)
    {
        var radice = string.IsNullOrWhiteSpace(cartellaConfigurata) ? Percorsi.CartellaProvaCammo : cartellaConfigurata;
        var exe = TrovaEseguibile(radice);
        if (exe is null)
        {
            var profilo = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var alternativa = string.IsNullOrWhiteSpace(profilo) ? null : Path.Combine(profilo, "LiquidinoProva");
            if (alternativa is not null && !string.Equals(Path.GetFullPath(alternativa), Path.GetFullPath(radice), StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(alternativa))
            {
                exe = TrovaEseguibile(alternativa);
                if (exe is not null) radice = alternativa;
            }
        }

        var cartellaModelli = TrovaCartellaModelli(radice, exe);
        var modelli = cartellaModelli is null ? [] : LeggiGguf(cartellaModelli);
        return new Rilevazione(exe, cartellaModelli ?? Path.Combine(radice, "modelli"), modelli);
    }

    public static string? TrovaEseguibile(string cartella)
    {
        if (string.IsNullOrWhiteSpace(cartella) || !Directory.Exists(cartella)) return null;
        var trovati = Elenca(cartella, 3)
            .Where(f =>
            {
                var n = Path.GetFileName(f);
                return n.Equals("llama-server.exe", StringComparison.OrdinalIgnoreCase)
                    || n.Equals("llama.exe", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
        return trovati.FirstOrDefault(f => Path.GetFileName(f).Equals("llama-server.exe", StringComparison.OrdinalIgnoreCase))
            ?? trovati.FirstOrDefault();
    }

    public static bool EServerDedicato(string eseguibile) =>
        Path.GetFileNameWithoutExtension(eseguibile).Equals("llama-server", StringComparison.OrdinalIgnoreCase);

    /// <summary>Argomenti del processo. Il router non carica nessun modello finché non glielo chiediamo, e mai più di due.</summary>
    public static string Argomenti(string eseguibile, int porta, string preset)
    {
        var coda = $"--host 127.0.0.1 --port {porta} --models-preset \"{preset}\" --models-max 2 --no-models-autoload --offline";
        return EServerDedicato(eseguibile) ? coda : "serve " + coda;
    }

    public static IReadOnlyList<SchedaModello> LeggiGguf(string cartella)
    {
        if (!Directory.Exists(cartella)) return [];
        var file = Directory.EnumerateFiles(cartella, "*.gguf", SearchOption.AllDirectories)
            .Where(f => Profondita(cartella, f) <= 2)
            .ToList();
        var pesi = new List<(string Percorso, RuoloModello Ruolo, string Chiave)>();
        var proiettori = new List<(string Percorso, string Chiave)>();
        foreach (var f in file)
        {
            var nome = Path.GetFileName(f);
            if (nome.Contains("mmproj", StringComparison.OrdinalIgnoreCase))
            {
                proiettori.Add((f, Chiave(nome)));
                continue;
            }
            var ruolo = RuoloDi(nome);
            if (ruolo is null) continue;
            pesi.Add((f, ruolo.Value, Chiave(nome)));
        }

        var schede = new List<SchedaModello>();
        var usati = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scelti = pesi.GroupBy(p => p.Ruolo)
            .Select(g => g.OrderByDescending(p => new FileInfo(p.Percorso).Length).First())
            .ToList();
        foreach (var scelto in scelti)
        {
            var mm = proiettori
                .Where(p => !usati.Contains(p.Percorso) && string.Equals(p.Chiave, scelto.Chiave, StringComparison.Ordinal))
                .Select(p => p.Percorso)
                .FirstOrDefault();
            if (mm is not null) usati.Add(mm);
            schede.Add(new SchedaModello(scelto.Ruolo, NomiModello.IdDi(scelto.Ruolo), scelto.Percorso, mm, Path.GetFileName(scelto.Percorso)));
        }
        return schede.OrderBy(s => s.Ruolo).ToList();
    }

    public static RuoloModello? RuoloDi(string nomeFile)
    {
        var n = nomeFile.ToLowerInvariant();
        if (n.Contains("mmproj")) return null;
        var d1 = n.Contains("d1");
        var omni = n.Contains("omni");
        if (d1 && omni) return RuoloModello.D1Omni;
        if (d1) return RuoloModello.D1;
        if (omni) return RuoloModello.Omni;
        if (n.Contains("audio")) return RuoloModello.Audio;
        if (n.Contains("-vl") || n.Contains("vl-") || n.Contains("_vl") || n.Contains("vision")) return RuoloModello.Vl;
        if (n.Contains("1.7") || n.Contains("1_7") || n.Contains("1-7")) return RuoloModello.Mani;
        return null;
    }

    public static string Chiave(string nomeFile)
    {
        var n = Path.GetFileNameWithoutExtension(nomeFile).ToLowerInvariant();
        if (n.StartsWith("mmproj-", StringComparison.Ordinal)) n = n["mmproj-".Length..];
        string[] code = ["-q8_0", "-q6_k", "-q5_k_m", "-q5_k_s", "-q4_k_m", "-q4_k_s", "-q4_0", "-q3_k_m", "-f16", "-bf16", "-q8", "-q4"];
        foreach (var q in code)
            if (n.EndsWith(q, StringComparison.Ordinal)) { n = n[..^q.Length]; break; }
        return n;
    }

    static string? TrovaCartellaModelli(string radice, string? exe)
    {
        var candidati = new List<string>
        {
            Path.Combine(radice, "modelli"),
            Path.Combine(radice, "models"),
            radice
        };
        if (exe is not null)
        {
            var dir = Path.GetDirectoryName(exe);
            if (dir is not null)
            {
                candidati.Add(Path.Combine(dir, "modelli"));
                candidati.Add(Path.GetFullPath(Path.Combine(dir, "..", "modelli")));
            }
        }
        foreach (var c in candidati.Distinct())
        {
            if (Directory.Exists(c) && Directory.EnumerateFiles(c, "*.gguf", SearchOption.AllDirectories).Any())
                return Path.GetFullPath(c);
        }
        return null;
    }

    static IEnumerable<string> Elenca(string cartella, int profonditaMassima)
    {
        var pila = new Stack<(string Dir, int Prof)>();
        pila.Push((cartella, 0));
        while (pila.Count > 0)
        {
            var (dir, prof) = pila.Pop();
            IEnumerable<string> file;
            try { file = Directory.EnumerateFiles(dir, "llama*.exe"); }
            catch { continue; }
            foreach (var f in file) yield return f;
            if (prof >= profonditaMassima) continue;
            IEnumerable<string> sotto;
            try { sotto = Directory.EnumerateDirectories(dir); }
            catch { continue; }
            foreach (var s in sotto) pila.Push((s, prof + 1));
        }
    }

    static int Profondita(string radice, string file)
    {
        var rel = Path.GetRelativePath(radice, file);
        return rel.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);
    }
}

/// <summary>Il file ini del router di llama-server: un modello per sezione, argomenti comuni in [*].</summary>
public static class PresetMotore
{
    public static string Scrivi(string percorsoIni, string cartellaSlot, IReadOnlyList<SchedaModello> modelli, ImpostazioniUtente imp)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(percorsoIni)!);
        Directory.CreateDirectory(cartellaSlot);
        var righe = new List<string>
        {
            "version = 1",
            "",
            "; Generato da Liquidino. Si può leggere. Al prossimo avvio del motore viene riscritto.",
            "[*]",
            "flash-attn = on",
            // Il GGUF minuscolo della CI ha la testa da 48: q4_0 (blocchi da 32) non ci sta.
            // Sul PC vero la cache resta q4. LIQUIDINO_E2E non è mai acceso nell'installazione.
            "cache-type-k = " + (ProvaInCi() ? "f16" : "q4_0"),
            "cache-type-v = " + (ProvaInCi() ? "f16" : "q4_0"),
            "n-gpu-layers = 99",
            "parallel = 1",
            "slot-save-path = " + PerIni(cartellaSlot),
            ""
        };
        foreach (var m in modelli.OrderBy(m => m.Id))
        {
            righe.Add("[" + m.Id + "]");
            righe.Add("model = " + PerIni(m.Percorso));
            if (!string.IsNullOrWhiteSpace(m.Mmproj))
                righe.Add("mmproj = " + PerIni(m.Mmproj));
            righe.Add("ctx-size = " + NomiModello.Contesto(m.Ruolo, imp));
            if (m.Ruolo is RuoloModello.Mani or RuoloModello.Omni or RuoloModello.Vl or RuoloModello.Audio
                && m.NomeFile.Contains("lfm", StringComparison.OrdinalIgnoreCase))
            {
                righe.Add("jinja = true");
                righe.Add("reasoning = off");
            }
            if (m.Ruolo == RuoloModello.Mani)
                righe.Add("repeat-penalty = 1.1");
            if (m.Ruolo is RuoloModello.D1 or RuoloModello.D1Omni)
            {
                righe.Add("batch-size = 8192");
                righe.Add("ubatch-size = 8192");
            }
            righe.Add("");
        }
        var testo = SenzaRete(string.Join(Environment.NewLine, righe));
        File.WriteAllText(percorsoIni, testo);
        return testo;
    }

    /// <summary>Toglie hf-repo, indirizzi e sezioni che non sono file locali. Il motore non deve scaricare nulla.</summary>
    public static string SenzaRete(string testo)
    {
        var righe = (testo ?? "").Replace("\r\n", "\n").Split('\n');
        var uscita = new List<string>();
        var blocco = new List<string>();
        string? titolo = null;

        void Chiudi()
        {
            if (titolo is null)
            {
                uscita.AddRange(blocco.Where(RigaLocale));
            }
            else if (SezioneLocale(titolo))
            {
                uscita.Add("[" + titolo + "]");
                uscita.AddRange(blocco.Where(RigaLocale));
            }
            blocco.Clear();
        }

        foreach (var riga in righe)
        {
            var t = riga.Trim();
            if (t.StartsWith('[') && t.EndsWith(']') && t.Length > 2)
            {
                Chiudi();
                titolo = t[1..^1].Trim();
                continue;
            }
            blocco.Add(riga);
        }
        Chiudi();
        return string.Join(Environment.NewLine, uscita).TrimEnd() + Environment.NewLine;
    }

    static bool SezioneLocale(string titolo) =>
        !titolo.Contains('/')
        && !titolo.Contains("hf-repo", StringComparison.OrdinalIgnoreCase)
        && !titolo.Contains("huggingface", StringComparison.OrdinalIgnoreCase);

    static bool RigaLocale(string riga)
    {
        var t = riga.TrimStart();
        if (t.StartsWith("hf-repo", StringComparison.OrdinalIgnoreCase)) return false;
        if (t.StartsWith("hf-file", StringComparison.OrdinalIgnoreCase)) return false;
        if (t.Contains("huggingface.co", StringComparison.OrdinalIgnoreCase)) return false;
        if (t.Contains("http://", StringComparison.OrdinalIgnoreCase) || t.Contains("https://", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    static string PerIni(string percorso) => percorso.Replace('\\', '/');

    static bool ProvaInCi() => Environment.GetEnvironmentVariable("LIQUIDINO_E2E") == "1";
}

/// <summary>Tiene il conto di chi è acceso. Rifiuta un terzo modello.</summary>
public static class PoliticaCarico
{
    public const int MassimoInsieme = 2;

    public readonly record struct Piano(IReadOnlyList<RuoloModello> Spegni, IReadOnlyList<RuoloModello> Accendi);

    public static Piano Calcola(IReadOnlyCollection<RuoloModello> accesi, IReadOnlyList<RuoloModello> voluti)
    {
        var distinti = voluti.Distinct().ToList();
        if (distinti.Count > MassimoInsieme)
            throw new InvalidOperationException("Mai più di due modelli insieme.");
        var accendi = distinti.Where(v => !accesi.Contains(v)).ToList();
        var spegni = new List<RuoloModello>();
        var occupati = accesi.Count + accendi.Count;
        if (occupati > MassimoInsieme)
        {
            var daTogliere = occupati - MassimoInsieme;
            foreach (var a in accesi.OrderBy(a => a == RuoloModello.D1 ? 1 : 0))
            {
                if (distinti.Contains(a)) continue;
                spegni.Add(a);
                if (spegni.Count == daTogliere) break;
            }
        }
        var dopo = accesi.Count - spegni.Count + accendi.Count;
        if (dopo > MassimoInsieme)
            throw new InvalidOperationException("Mai più di due modelli insieme.");
        return new Piano(spegni, accendi);
    }
}
