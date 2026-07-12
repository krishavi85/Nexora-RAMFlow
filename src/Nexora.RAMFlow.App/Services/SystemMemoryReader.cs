using System.Diagnostics;
using System.Management;
using Nexora.RAMFlow.Core.Models;

namespace Nexora.RAMFlow.App.Services;

public sealed class SystemMemoryReader
{
    public Task<MemorySnapshot> CaptureAsync(CancellationToken cancellationToken = default) =>
        Task.Run(Capture, cancellationToken);

    private static MemorySnapshot Capture()
    {
        ulong totalPhysical = 0;
        ulong availablePhysical = 0;
        ulong totalVirtual = 0;
        ulong availableVirtual = 0;

        using (var searcher = new ManagementObjectSearcher(
                   "SELECT TotalVisibleMemorySize, FreePhysicalMemory, TotalVirtualMemorySize, FreeVirtualMemory FROM Win32_OperatingSystem"))
        using (var results = searcher.Get())
        {
            var os = results.Cast<ManagementObject>().FirstOrDefault()
                ?? throw new InvalidOperationException("Windows memory information was not returned.");

            totalPhysical = KibibytesToBytes(ReadUInt64(os["TotalVisibleMemorySize"]));
            availablePhysical = KibibytesToBytes(ReadUInt64(os["FreePhysicalMemory"]));
            totalVirtual = KibibytesToBytes(ReadUInt64(os["TotalVirtualMemorySize"]));
            availableVirtual = KibibytesToBytes(ReadUInt64(os["FreeVirtualMemory"]));
        }

        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory)
            ?? throw new InvalidOperationException("The Windows drive could not be determined.");
        var drive = new DriveInfo(systemRoot);

        return new MemorySnapshot(
            DateTimeOffset.Now,
            totalPhysical,
            availablePhysical,
            totalVirtual,
            availableVirtual,
            (ulong)Math.Max(0, drive.AvailableFreeSpace),
            ReadPageFiles(),
            ReadTopProcesses());
    }

    private static IReadOnlyList<PageFileInfo> ReadPageFiles()
    {
        var pageFiles = new List<PageFileInfo>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, AllocatedBaseSize, CurrentUsage, PeakUsage FROM Win32_PageFileUsage");
        using var results = searcher.Get();

        foreach (ManagementObject item in results)
        {
            pageFiles.Add(new PageFileInfo(
                Convert.ToString(item["Name"]) ?? "Unknown",
                ReadUInt64(item["AllocatedBaseSize"]),
                ReadUInt64(item["CurrentUsage"]),
                ReadUInt64(item["PeakUsage"])));
        }

        return pageFiles;
    }

    private static IReadOnlyList<ProcessMemoryInfo> ReadTopProcesses()
    {
        var processes = new List<ProcessMemoryInfo>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    processes.Add(new ProcessMemoryInfo(process.Id, process.ProcessName, process.WorkingSet64));
                }
                catch
                {
                    // Protected or short-lived processes may disappear while being sampled.
                }
            }
        }

        return processes
            .OrderByDescending(item => item.WorkingSetBytes)
            .Take(12)
            .ToArray();
    }

    private static ulong ReadUInt64(object? value) => value is null ? 0UL : Convert.ToUInt64(value);
    private static ulong KibibytesToBytes(ulong kibibytes) => kibibytes * 1024UL;
}
