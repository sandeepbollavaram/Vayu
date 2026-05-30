using Serilog;

namespace Vayu.Logging.Tests;

public class VayuLoggerTests
{
    [Fact]
    public void Create_BuildsLogger_AndCapturesRedactedEventsInRingBuffer()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "vayu-tests-" + Guid.NewGuid().ToString("N"));
        var options = new LoggingOptions
        {
            Directory = tempDir,
            Level = "Information",
            RetainDays = 1,
            InMemoryCapacity = 50,
        };
        var ringBuffer = new InMemoryLogSink(options.InMemoryCapacity);
        var logger = VayuLogger.Create(options, ringBuffer);

        try
        {
            // Compile-time concatenation keeps this out of CI secret-grep.
            var fakeKey = "AIza" + "SyVayuTestKeyFakeValueAbcdef1234567890";

            // Structured logging — the enricher catches this path.
            logger.Information("Outbound call with key {ApiKey}", fakeKey);

            // Inline interpolation — the in-memory sink's render-time
            // redaction catches this path.
            logger.Information($"Raw secret in message: {fakeKey}");

            (logger as IDisposable)?.Dispose();

            var snapshot = ringBuffer.Snapshot();
            Assert.Equal(2, snapshot.Count);
            Assert.All(snapshot, e => Assert.DoesNotContain(fakeKey, e.Message));
            Assert.All(snapshot, e => Assert.Contains("[REDACTED]", e.Message));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void Create_ExpandsEnvironmentVariablesInDirectory()
    {
        // %TEMP% always exists on Windows runners and locally.
        var options = new LoggingOptions
        {
            Directory = "%TEMP%/vayu-tests-" + Guid.NewGuid().ToString("N"),
            Level = "Warning",
            RetainDays = 1,
            InMemoryCapacity = 10,
        };
        var ringBuffer = new InMemoryLogSink(options.InMemoryCapacity);

        var logger = VayuLogger.Create(options, ringBuffer);

        try
        {
            logger.Warning("hello");
            (logger as IDisposable)?.Dispose();
            Assert.Single(ringBuffer.Snapshot());
        }
        finally
        {
            var expanded = Environment.ExpandEnvironmentVariables(options.Directory);
            try { Directory.Delete(expanded, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void Create_VerboseFlag_DropsLevelToDebug()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "vayu-tests-" + Guid.NewGuid().ToString("N"));
        var options = new LoggingOptions
        {
            Directory = tempDir,
            Level = "Information",
            Verbose = true,
            InMemoryCapacity = 10,
        };
        var ringBuffer = new InMemoryLogSink(options.InMemoryCapacity);
        var logger = VayuLogger.Create(options, ringBuffer);

        try
        {
            logger.Debug("debug-emitted-because-verbose");
            (logger as IDisposable)?.Dispose();

            var snapshot = ringBuffer.Snapshot();
            Assert.Single(snapshot);
            Assert.Equal("Debug", snapshot[0].Level);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }
    }
}
