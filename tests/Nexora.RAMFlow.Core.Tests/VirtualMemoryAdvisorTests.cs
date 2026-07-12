using Nexora.RAMFlow.Core.Models;
using Nexora.RAMFlow.Core.Services;
using Xunit;

namespace Nexora.RAMFlow.Core.Tests;

public sealed class VirtualMemoryAdvisorTests
{
    private readonly VirtualMemoryAdvisor _advisor = new();

    [Fact]
    public void BalancedProfile_ProducesOrderedSizes()
    {
        var result = _advisor.Recommend(Snapshot(ramGb: 8, freeDiskGb: 100), WorkloadProfile.Balanced);

        Assert.False(result.UseSystemManaged);
        Assert.True(result.InitialSizeMegabytes >= 2_048);
        Assert.True(result.MaximumSizeMegabytes >= result.InitialSizeMegabytes);
    }

    [Fact]
    public void HeavyProfile_IsLargerThanBalancedProfile()
    {
        var snapshot = Snapshot(ramGb: 16, freeDiskGb: 200);
        var balanced = _advisor.Recommend(snapshot, WorkloadProfile.Balanced);
        var heavy = _advisor.Recommend(snapshot, WorkloadProfile.HeavyApplications);

        Assert.True(heavy.InitialSizeMegabytes >= balanced.InitialSizeMegabytes);
        Assert.True(heavy.MaximumSizeMegabytes >= balanced.MaximumSizeMegabytes);
    }

    [Fact]
    public void LowDisk_FallsBackToSystemManaged()
    {
        var result = _advisor.Recommend(Snapshot(ramGb: 8, freeDiskGb: 11), WorkloadProfile.HeavyApplications);

        Assert.True(result.UseSystemManaged);
    }

    [Fact]
    public void Recommendation_NeverExceedsSafetyCap()
    {
        var result = _advisor.Recommend(Snapshot(ramGb: 128, freeDiskGb: 500), WorkloadProfile.HeavyApplications);

        Assert.InRange(result.MaximumSizeMegabytes, 0, 65_536);
    }

    private static MemorySnapshot Snapshot(int ramGb, int freeDiskGb)
    {
        const ulong gb = 1024UL * 1024UL * 1024UL;
        return new MemorySnapshot(
            DateTimeOffset.UtcNow,
            (ulong)ramGb * gb,
            (ulong)ramGb * gb / 2,
            (ulong)ramGb * gb * 2,
            (ulong)ramGb * gb,
            (ulong)freeDiskGb * gb,
            [],
            []);
    }
}
