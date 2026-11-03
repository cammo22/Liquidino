using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Liquidino;

/// <summary>
/// Fotogrammi dell'avatar, stesso formato del DaP Convertitore
/// (https://github.com/cammo22/DaP-Convertitore, file Avatar.cs, licenza MIT,
/// Copyright 2026 DaProdProduzioni). Le immagini sono quelle già approvate: non si rifanno.
/// </summary>
public sealed class FoglioAvatar
{
    public string Nome { get; }
    public int Lato { get; }
    public int Colonne { get; }
    public double Fps { get; }
    readonly Dictionary<string, (int Da, int N)> _animazioni;
    readonly BitmapSource _immagine;
    readonly Dictionary<int, BitmapSource> _cache = [];

    FoglioAvatar(string nome, BitmapSource immagine, int lato, int colonne, double fps, Dictionary<string, (int, int)> animazioni)
    {
        Nome = nome;
        _immagine = immagine;
        Lato = lato;
        Colonne = colonne;
        Fps = fps;
        _animazioni = animazioni;
    }

    public static string Cartella => Path.Combine(AppContext.BaseDirectory, "avatar");

    public static IReadOnlyList<string> Disponibili() =>
        Directory.Exists(Cartella)
            ? Directory.EnumerateFiles(Cartella, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n is not null && File.Exists(Path.Combine(Cartella, n + ".png")))
                .Order()
                .Cast<string>()
                .ToList()
            : [];

    public static FoglioAvatar? Carica(string nome)
    {
        try
        {
            var png = Path.Combine(Cartella, nome + ".png");
            var json = Path.Combine(Cartella, nome + ".json");
            if (!File.Exists(png) || !File.Exists(json)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(json));
            var r = doc.RootElement;
            var anim = new Dictionary<string, (int, int)>();
            foreach (var a in r.GetProperty("animazioni").EnumerateObject())
                anim[a.Name] = (a.Value.GetProperty("da").GetInt32(), a.Value.GetProperty("n").GetInt32());
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(png);
            bmp.EndInit();
            bmp.Freeze();
            return new FoglioAvatar(nome, bmp, r.GetProperty("lato").GetInt32(), r.GetProperty("colonne").GetInt32(),
                r.TryGetProperty("fps", out var f) ? f.GetDouble() : 24, anim);
        }
        catch { return null; }
    }

    public bool Ha(string animazione) => _animazioni.ContainsKey(animazione);
    public int Quanti(string animazione) => _animazioni.TryGetValue(animazione, out var a) ? a.N : 0;

    public BitmapSource? Fotogramma(string animazione, int i)
    {
        if (!_animazioni.TryGetValue(animazione, out var a) || a.N <= 0) return null;
        var k = a.Da + Math.Clamp(i, 0, a.N - 1);
        if (_cache.TryGetValue(k, out var f)) return f;
        var ritaglio = new CroppedBitmap(_immagine, new Int32Rect(k % Colonne * Lato, k / Colonne * Lato, Lato, Lato));
        ritaglio.Freeze();
        return _cache[k] = ritaglio;
    }
}
