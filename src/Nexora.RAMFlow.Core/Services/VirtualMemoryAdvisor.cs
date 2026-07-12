using Nexora.RAMFlow.Core.Models;

namespace Nexora.RAMFlow.Core.Services;

public sealed class VirtualMemoryAdvisor
{
    private const long Megabyte = 1024L * 1024L;

    public VirtualMemoryRecommendation Recommend(MemorySnapshot snapshot, WorkloadProfile profile)
    {
        if (profile == WorkloadProfile.SystemManaged)
        {
            return SystemManaged("Windows manages the page file dynamically. This is the safest default for most computers.");
        }

        var ramMb = Math.Max(1L, (long)(snapshot.TotalPhysicalBytes / Megabyte));
        var freeDiskMb = Math.Max(0L, (long)(snapshot.SystemDriveFreeBytes / Megabyte));
        var protectedReserveMb = Math.Max(10_240L, freeDiskMb / 10L);
        var safeDiskBudgetMb = Math.Max(0L, freeDiskMb - protectedReserveMb);
        var maximumAllowedMb = Math.Min(65_536L, safeDiskBudgetMb / 2L);

        if (maximumAllowedMb < 2_048)
        {
            return SystemManaged("There is not enough safely available disk space for a fixed custom profile. Free storage before increasing virtual memory.");
        }

        (long desiredInitial, long desiredMaximum, string title, string explanation) = profile switch
        {
            WorkloadProfile.HeavyApplications => (
                Math.Max(4_096L, ramMb),
                Math.Max(8_192L, ramMb * 2L),
                "Heavy Applications profile",
                "Designed for DAWs, video editors, compilers, local AI tools, and other workloads that can exhaust physical RAM."),
            WorkloadProfile.LowStorage => (
                1_024L,
                Math.Max(2_048L, ramMb / 2L),
                "Low Storage profile",
                "Uses a smaller safety net while preserving more free space on the Windows drive."),
            _ => (
                Math.Max(2_048L, ramMb / 2L),
                Math.Max(4_096L, ramMb),
                "Balanced profile",
                "Provides conservative overflow capacity for everyday multitasking without reserving excessive disk space.")
        };

        var initial = (int)Math.Min(desiredInitial, maximumAllowedMb);
        var maximum = (int)Math.Min(Math.Max(desiredMaximum, initial), maximumAllowedMb);
        var warning = snapshot.SystemDriveFreeBytes < 20UL * 1024UL * 1024UL * 1024UL
            ? "Your Windows drive has less than 20 GB free. Storage pressure may reduce performance."
            : "Virtual memory is much slower than physical RAM. A larger page file mainly improves stability, not speed.";

        return new VirtualMemoryRecommendation(
            profile,
            false,
            initial,
            maximum,
            title,
            explanation,
            warning);
    }

    private static VirtualMemoryRecommendation SystemManaged(string explanation) => new(
        WorkloadProfile.SystemManaged,
        true,
        0,
        0,
        "Windows system-managed profile",
        explanation,
        "Windows may resize the paging file and a restart can still be required.");
}
