namespace Nexora.RAMFlow.Core.Models;

public enum WorkloadProfile
{
    Balanced,
    HeavyApplications,
    LowStorage,
    SystemManaged
}

public sealed record PageFileInfo(
    string Name,
    ulong AllocatedMegabytes,
    ulong CurrentUsageMegabytes,
    ulong PeakUsageMegabytes);

public sealed record ProcessMemoryInfo(int Id, string Name, long WorkingSetBytes)
{
    public string WorkingSetDisplay => ByteSizeFormatter.Format((ulong)Math.Max(0, WorkingSetBytes));
}

public sealed record MemorySnapshot(
    DateTimeOffset CapturedAt,
    ulong TotalPhysicalBytes,
    ulong AvailablePhysicalBytes,
    ulong TotalVirtualBytes,
    ulong AvailableVirtualBytes,
    ulong SystemDriveFreeBytes,
    IReadOnlyList<PageFileInfo> PageFiles,
    IReadOnlyList<ProcessMemoryInfo> TopProcesses)
{
    public double PhysicalUsagePercent => PercentageUsed(TotalPhysicalBytes, AvailablePhysicalBytes);
    public double VirtualUsagePercent => PercentageUsed(TotalVirtualBytes, AvailableVirtualBytes);

    private static double PercentageUsed(ulong total, ulong available)
    {
        if (total == 0)
        {
            return 0;
        }

        var used = total > available ? total - available : 0;
        return Math.Clamp(used * 100d / total, 0d, 100d);
    }
}

public sealed record VirtualMemoryRecommendation(
    WorkloadProfile Profile,
    bool UseSystemManaged,
    int InitialSizeMegabytes,
    int MaximumSizeMegabytes,
    string Title,
    string Explanation,
    string Warning);

public static class ByteSizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Format(ulong bytes)
    {
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {Units[unit]}";
    }
}
