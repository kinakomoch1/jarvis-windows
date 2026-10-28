using System;
using System.Runtime.InteropServices;

namespace Jarvis;

/// <summary>Lightweight, read-only Windows physical memory monitoring.</summary>
internal static class PcHealth
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    internal static (int Percent, double UsedGiB, double TotalGiB) ReadMemory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        var total = status.TotalPhysical / 1073741824.0;
        var used = (status.TotalPhysical - status.AvailablePhysical) / 1073741824.0;
        return ((int)status.MemoryLoad, Math.Round(used, 1), Math.Round(total, 1));
    }
}
