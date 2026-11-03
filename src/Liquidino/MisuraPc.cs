using System.Diagnostics;
using System.Runtime.InteropServices;
using Liquidino.Nucleo;

namespace Liquidino;

/// <summary>Legge RAM e, se c'è, la VRAM della scheda NVIDIA. Nessun dato esce dal PC.</summary>
public static class MisuraPc
{
    public static InfoMacchina Misura()
    {
        long ram = 0;
        try
        {
            var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref mem)) ram = (long)mem.ullTotalPhys;
        }
        catch { /* senza la misura si sceglie il contesto corto */ }

        long? vram = null;
        try
        {
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=memory.total --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p is not null && p.WaitForExit(4000))
            {
                var riga = p.StandardOutput.ReadToEnd().Trim();
                var prima = riga.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (prima is not null && long.TryParse(prima.Trim(), out var mib) && mib > 0)
                    vram = mib * 1024L * 1024L;
            }
        }
        catch { /* niente scheda NVIDIA, o nvidia-smi non è nel percorso */ }

        return new InfoMacchina(ram, vram);
    }

    /// <summary>Vero se c'è un programma a schermo intero, un gioco o una presentazione.</summary>
    public static bool Occupato()
    {
        try { return SHQueryUserNotificationState(out var s) == 0 && s is 2 or 3 or 4; }
        catch { return false; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("shell32.dll")]
    static extern int SHQueryUserNotificationState(out int stato);
}
