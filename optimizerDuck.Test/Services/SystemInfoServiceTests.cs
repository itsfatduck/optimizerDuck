using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Services.System;

namespace optimizerDuck.Test.Services;

public class SystemInfoServiceTests
{
    [Fact]
    public async Task RefreshAsync_SetsSnapshot()
    {
        var service = new SystemInfoService(NullLogger<SystemInfoService>.Instance);
        var cancellationToken = TestContext.Current.CancellationToken;

        var snapshot = await service.RefreshAsync(cancellationToken);

        Assert.Equal(snapshot, service.Snapshot);
    }

    [Fact]
    public void LogSummary_DoesNotThrow()
    {
        var service = new SystemInfoService(NullLogger<SystemInfoService>.Instance);

        service.LogSummary();
    }

    [Fact]
    public async Task LogSummary_ReportsDiskSizeInWholeGigabytes()
    {
        var logger = new CapturingLogger<SystemInfoService>();
        var service = new SystemInfoService(logger);

        await service.RefreshAsync(TestContext.Current.CancellationToken);
        service.LogSummary();

        var diskLines = logger
            .Lines.Where(line => line.StartsWith("Disk ", StringComparison.Ordinal))
            .ToList();
        if (diskLines.Count == 0)
            Assert.Skip("This host reports no ready volumes, so there is no disk line to check.");

        // A raw byte double renders as "109.17089462280273 GB"; the summary must show whole
        // gigabytes. The DoesNotMatch assertion is the one that catches that regression.
        Assert.All(diskLines, line => Assert.DoesNotMatch(@"\d+\.\d+ GB", line));
        Assert.All(diskLines, line => Assert.Matches(@"\d+ GB \(free \d+ GB\)", line));
    }

    /// <summary>
    ///     Hand-written logger double that keeps the rendered messages, so a test can assert on
    ///     what a log line actually says.
    /// </summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Lines.Add(formatter(state, exception));
    }
}
