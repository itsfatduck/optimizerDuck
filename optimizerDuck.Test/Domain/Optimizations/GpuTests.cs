using optimizerDuck.Domain.Optimizations.Categories;

namespace optimizerDuck.Test.Domain.Optimizations;

public class GpuTests
{
    private const string ClassRoot =
        @"HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    private const string AmdIgpu = @"PCI\VEN_1002&DEV_15BF&SUBSYS_0B7D1028&REV_C4\4&2D1E1B8&0&0041";
    private const string NvidiaDgpu =
        @"PCI\VEN_10DE&DEV_28E0&SUBSYS_0B7D1028&REV_A1\4&1A3F2C5B&0&0008";

    [Fact]
    public void TryResolveClassKeyPath_HybridLaptop_UsesEachAdaptersOwnKey()
    {
        // Layout from issue #108: WMI lists the AMD iGPU first (VideoController1) but its class
        // key is \0001, while the NVIDIA dGPU (VideoController2) owns \0000.
        var drivers = new Dictionary<string, string>
        {
            [AmdIgpu] = @"{4d36e968-e325-11ce-bfc1-08002be10318}\0001",
            [NvidiaDgpu] = @"{4d36e968-e325-11ce-bfc1-08002be10318}\0000",
        };

        Assert.True(Gpu.TryResolveClassKeyPath(AmdIgpu, Lookup(drivers), out var amdPath));
        Assert.True(Gpu.TryResolveClassKeyPath(NvidiaDgpu, Lookup(drivers), out var nvidiaPath));

        Assert.Equal($@"{ClassRoot}\0001", amdPath);
        Assert.Equal($@"{ClassRoot}\0000", nvidiaPath);
    }

    [Fact]
    public void TryResolveClassKeyPath_ClassGuidCasing_IsNormalized()
    {
        Assert.True(
            Gpu.TryResolveClassKeyPath(
                AmdIgpu,
                _ => @"{4D36E968-E325-11CE-BFC1-08002BE10318}\0002",
                out var path
            )
        );
        Assert.Equal($@"{ClassRoot}\0002", path);
    }

    [Fact]
    public void TryResolveClassKeyPath_PassesThePnpDeviceIdToTheReader()
    {
        string? requested = null;
        Gpu.TryResolveClassKeyPath(
            NvidiaDgpu,
            id =>
            {
                requested = id;
                return null;
            },
            out _
        );

        Assert.Equal(NvidiaDgpu, requested);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolveClassKeyPath_MissingPnpDeviceId_FailsWithoutReading(string? pnpDeviceId)
    {
        var read = false;

        var resolved = Gpu.TryResolveClassKeyPath(
            pnpDeviceId,
            _ =>
            {
                read = true;
                return @"{4d36e968-e325-11ce-bfc1-08002be10318}\0000";
            },
            out var path
        );

        Assert.False(resolved);
        Assert.False(read);
        Assert.Empty(path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    // Another device class (network adapters) must never be written as a GPU key.
    [InlineData(@"{4d36e972-e325-11ce-bfc1-08002be10318}\0001")]
    [InlineData(@"{4d36e968-e325-11ce-bfc1-08002be10318}")]
    [InlineData(@"{4d36e968-e325-11ce-bfc1-08002be10318}\")]
    [InlineData(@"{4d36e968-e325-11ce-bfc1-08002be10318}\001")]
    [InlineData(@"{4d36e968-e325-11ce-bfc1-08002be10318}\00001")]
    [InlineData(@"{4d36e968-e325-11ce-bfc1-08002be10318}\00a1")]
    [InlineData(@"{4d36e968-e325-11ce-bfc1-08002be10318}\0001\Settings")]
    [InlineData(@"{4d36e968-e325-11ce-bfc1-08002be10318}\..\0001")]
    public void TryResolveClassKeyPath_UnexpectedDriverValue_IsRejected(string? driver)
    {
        Assert.False(Gpu.TryResolveClassKeyPath(AmdIgpu, _ => driver, out var path));
        Assert.Empty(path);
    }

    private static Func<string, string?> Lookup(IReadOnlyDictionary<string, string> drivers) =>
        id => drivers.GetValueOrDefault(id);
}
