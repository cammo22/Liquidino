using System.Diagnostics;
using System.Runtime.InteropServices;
using Liquidino.Nucleo;
using Disegno = System.Drawing;
using Formati = System.Drawing.Imaging;

namespace Liquidino;

/// <summary>
/// Le mani su Windows. Sanno fare solo i gesti di InterpretePc: aprire un programma del menu Start,
/// aprire una cartella nota, il volume coi tasti multimediali, una foto dello schermo che resta in memoria.
/// Ogni gesto viene ricontrollato qui: si apre solo ciò che sta negli elenchi letti da questo PC.
/// </summary>
public sealed class ManiWindows : IManiPc
{
    public const string PrefissoApp = @"shell:AppsFolder\";
    static readonly TimeSpan Durata = TimeSpan.FromMinutes(10);

    readonly object _lucchetto = new();
    IReadOnlyList<ProgrammaNoto>? _programmi;
    DateTime _lettiUtc;

    public IReadOnlyList<ProgrammaNoto> Programmi()
    {
        lock (_lucchetto)
        {
            if (_programmi is not null && DateTime.UtcNow - _lettiUtc < Durata) return _programmi;
        }
        var letti = LeggiProgrammi();
        lock (_lucchetto)
        {
            _programmi = letti;
            _lettiUtc = DateTime.UtcNow;
            return letti;
        }
    }

    /// <summary>Si chiama all'avvio, fuori dal filo della finestra, così il primo messaggio non aspetta.</summary>
    public void Prepara()
    {
        try { Programmi(); }
        catch { /* si riprova al primo messaggio */ }
    }

    public IReadOnlyDictionary<string, string> Cartelle()
    {
        var profilo = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cartelle = new Dictionary<string, string>
        {
            ["documenti"] = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            ["download"] = CartellaDownload() ?? Path.Combine(profilo, "Downloads"),
            ["immagini"] = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            ["musica"] = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            ["video"] = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            ["desktop"] = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };
        return cartelle
            .Where(c => !string.IsNullOrWhiteSpace(c.Value) && Directory.Exists(c.Value))
            .ToDictionary(c => c.Key, c => c.Value);
    }

    public Task<string> Esegui(GestoPc gesto, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        switch (gesto.Tipo)
        {
            case TipoGesto.ApriProgramma:
                if (!Programmi().Any(p => p.Avvio == gesto.Bersaglio))
                    throw new InvalidOperationException("«" + gesto.Nome + "» non è più nel menu Start");
                ApriProgramma(gesto.Bersaglio);
                return Task.FromResult("Apro «" + gesto.Nome + "».");
            case TipoGesto.ApriCartella:
                if (!Cartelle().Values.Contains(gesto.Bersaglio, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException("la cartella " + gesto.Nome + " non c'è");
                using (Process.Start(new ProcessStartInfo("explorer.exe", "\"" + gesto.Bersaglio + "\"") { UseShellExecute = true })) { }
                return Task.FromResult("Ti apro la cartella " + gesto.Nome + ".");
            case TipoGesto.Volume:
                return Task.FromResult(Volume(gesto.Bersaglio));
            default:
                throw new InvalidOperationException("questo gesto lo fa la chat, non le mani");
        }
    }

    public Task<ImmagineSchermo?> FotoSchermo(CancellationToken ct) => Task.Run<ImmagineSchermo?>(() =>
    {
        var schermo = System.Windows.Forms.Screen.PrimaryScreen;
        if (schermo is null) return null;
        var area = schermo.Bounds;
        if (area.Width <= 0 || area.Height <= 0) return null;
        using var foto = new Disegno.Bitmap(area.Width, area.Height, Formati.PixelFormat.Format24bppRgb);
        using (var g = Disegno.Graphics.FromImage(foto))
            g.CopyFromScreen(area.Left, area.Top, 0, 0, area.Size, Disegno.CopyPixelOperation.SourceCopy);
        ct.ThrowIfCancellationRequested();
        // Più piccola basta per descriverla e pesa meno sul modello.
        var scala = Math.Min(1.0, 1280.0 / area.Width);
        using var piccola = scala < 1
            ? new Disegno.Bitmap(foto, new Disegno.Size(Math.Max(1, (int)(area.Width * scala)), Math.Max(1, (int)(area.Height * scala))))
            : null;
        using var memoria = new MemoryStream();
        (piccola ?? foto).Save(memoria, Formati.ImageFormat.Jpeg);
        return new ImmagineSchermo(memoria.ToArray(), "image/jpeg");
    }, ct);

    static void ApriProgramma(string avvio)
    {
        if (avvio.StartsWith(PrefissoApp, StringComparison.OrdinalIgnoreCase))
        {
            using (Process.Start(new ProcessStartInfo("explorer.exe", "\"" + avvio + "\"") { UseShellExecute = true })) { }
            return;
        }
        if (!File.Exists(avvio)) throw new InvalidOperationException("il collegamento non c'è più");
        using (Process.Start(new ProcessStartInfo(avvio) { UseShellExecute = true })) { }
    }

    static string Volume(string verso)
    {
        switch (verso)
        {
            case "su":
                for (var i = 0; i < 5; i++) Tasto(VkVolumeSu);
                return "Ho alzato il volume.";
            case "giu":
                for (var i = 0; i < 5; i++) Tasto(VkVolumeGiu);
                return "Ho abbassato il volume.";
            case "muto":
                Tasto(VkMuto);
                return "Ho premuto il tasto del muto.";
            case "pausa":
                Tasto(VkPlayPausa);
                return "Ho premuto play/pausa.";
            default:
                throw new InvalidOperationException("non so che cosa fare col volume");
        }
    }

    /// <summary>Le app del menu Start (anche quelle moderne), poi i collegamenti .lnk se la shell non risponde.</summary>
    static IReadOnlyList<ProgrammaNoto> LeggiProgrammi()
    {
        var lista = new List<ProgrammaNoto>();
        lista.AddRange(AppDellaShell());
        lista.AddRange(Collegamenti());
        var visti = new HashSet<string>(StringComparer.Ordinal);
        return lista
            .Where(p => InterpretePc.ProgrammaAmmesso(p.Nome) && visti.Add(InterpretePc.Normalizza(p.Nome)))
            .ToList();
    }

    static List<ProgrammaNoto> AppDellaShell()
    {
        var trovate = new List<ProgrammaNoto>();
        // La shell è un oggetto COM a filo singolo: si legge su un filo STA tutto suo.
        var filo = new Thread(() =>
        {
            try
            {
                var tipo = Type.GetTypeFromProgID("Shell.Application");
                if (tipo is null) return;
                dynamic shell = Activator.CreateInstance(tipo)!;
                try
                {
                    dynamic cartella = shell.NameSpace("shell:AppsFolder");
                    if (cartella is null) return;
                    dynamic voci = cartella.Items();
                    int quante = voci.Count;
                    for (var i = 0; i < quante && i < 2000; i++)
                    {
                        dynamic voce = voci.Item(i);
                        if (voce is null) continue;
                        string? nome = voce.Name;
                        string? id = voce.Path;
                        if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(id)) continue;
                        if (id.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
                        trovate.Add(new ProgrammaNoto(nome.Trim(), PrefissoApp + id.Trim()));
                    }
                }
                finally { Marshal.FinalReleaseComObject(shell); }
            }
            catch { /* senza la shell restano i collegamenti .lnk */ }
        }) { IsBackground = true, Name = "Liquidino: elenco app" };
        filo.SetApartmentState(ApartmentState.STA);
        filo.Start();
        if (!filo.Join(TimeSpan.FromSeconds(10))) return [];
        return trovate;
    }

    static IEnumerable<ProgrammaNoto> Collegamenti()
    {
        var radici = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
        };
        foreach (var radice in radici.Where(r => !string.IsNullOrWhiteSpace(r) && Directory.Exists(r)))
        {
            IEnumerable<string> file;
            try { file = Directory.EnumerateFiles(radice, "*.lnk", SearchOption.AllDirectories).ToList(); }
            catch { continue; }
            foreach (var f in file)
                yield return new ProgrammaNoto(Path.GetFileNameWithoutExtension(f), f);
        }
    }

    static string? CartellaDownload()
    {
        try
        {
            var download = new Guid("374DE290-123F-4565-9164-39C4925E467B");
            if (SHGetKnownFolderPath(download, 0, IntPtr.Zero, out var percorso) != 0 || percorso == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringUni(percorso); }
            finally { Marshal.FreeCoTaskMem(percorso); }
        }
        catch { return null; }
    }

    const byte VkMuto = 0xAD;
    const byte VkVolumeGiu = 0xAE;
    const byte VkVolumeSu = 0xAF;
    const byte VkPlayPausa = 0xB3;
    const uint TastoEsteso = 0x0001;
    const uint TastoSu = 0x0002;

    static void Tasto(byte vk)
    {
        keybd_event(vk, 0, TastoEsteso, UIntPtr.Zero);
        keybd_event(vk, 0, TastoEsteso | TastoSu, UIntPtr.Zero);
    }

    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("shell32.dll")]
    static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);
}
