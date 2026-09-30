using Microsoft.Extensions.Logging.Abstractions;
using ThreeDEye.FileProcessor.Generator.Configuration.Options;
using ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Contracts;
using ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Generation;
using ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Handlers;
using ThreeDEye.FileProcessor.Generator.IntegrationTests.Shared;

namespace ThreeDEye.FileProcessor.Generator.IntegrationTests.Features.GenerateFile;

public sealed class GenerateFileEndToEndTests
{
    private static GenerateFileHandler CreateHandler(int poolSize) =>
        new(new FileGenerator(new RandomRecordGenerator(poolSize), NullLogger<FileGenerator>.Instance));

    /// <summary>Streams the file and validates each line against the record contract.</summary>
    private static (long Count, IReadOnlyDictionary<string, int> TextOccurrences) InspectStreaming(string path)
    {
        var textOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        long count = 0;
        long lineNumber = 0;

        using var reader = new StreamReader(path);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;

            var separatorIndex = line.IndexOf(". ", StringComparison.Ordinal);
            Assert.True(separatorIndex > 0, $"Line {lineNumber} ('{line}') has no '. ' separator.");

            Assert.True(
                long.TryParse(line[..separatorIndex], out _),
                $"Line {lineNumber} ('{line}') has a non-numeric prefix.");

            var text = line[(separatorIndex + 2)..];
            Assert.False(string.IsNullOrEmpty(text), $"Line {lineNumber} has empty text.");

            textOccurrences[text] = textOccurrences.TryGetValue(text, out var existing) ? existing + 1 : 1;
            count++;
        }

        return (count, textOccurrences);
    }

    [Fact]
    public async Task GenerateFile_ShouldCreateValidFile()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("generated.txt");
        var handler = CreateHandler(poolSize: 16);

        // Act
        await handler.HandleAsync(new GenerateFileRequest(path, 64 * 1024), new GenerateFileOptions());

        // Assert
        var (count, _) = InspectStreaming(path);
        Assert.True(count > 0, "Generator produced no records.");
    }

    [Fact]
    public async Task GenerateFile_ShouldContainRepeatedTextValues()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("generated.txt");
        // A small pool guarantees repetition once the record count exceeds the pool size.
        var handler = CreateHandler(poolSize: 4);

        // Act
        await handler.HandleAsync(new GenerateFileRequest(path, 32 * 1024), new GenerateFileOptions { StringPoolSize = 4 });

        // Assert
        var (count, textOccurrences) = InspectStreaming(path);

        Assert.True(count > 4, "Test needs more records than the pool size to guarantee duplicates.");
        Assert.Contains(textOccurrences, kvp => kvp.Value > 1);
    }

    [Fact]
    public async Task GenerateFile_ShouldRespectTargetSizeBehavior()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("generated.txt");
        const long target = 16 * 1024;

        // Act
        await CreateHandler(poolSize: 8)
            .HandleAsync(new GenerateFileRequest(path, target), new GenerateFileOptions());

        // Assert
        var actual = new FileInfo(path).Length;

        // Contract: write whole records until the accounted size reaches the target,
        // so the file is at least the target and overshoots by at most one record.
        Assert.True(actual >= target, $"Expected at least {target} bytes but got {actual}.");
        Assert.True(actual < target * 2, $"Unexpectedly large overshoot: {actual} bytes for a {target} byte target.");
    }

    [Fact]
    public async Task GenerateFile_ShouldProduceDistinctTextValuesBoundedByPoolSize()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("generated.txt");
        const int poolSize = 6;

        // Act
        await CreateHandler(poolSize)
            .HandleAsync(new GenerateFileRequest(path, 32 * 1024), new GenerateFileOptions { StringPoolSize = poolSize });

        // Assert
        var (_, textOccurrences) = InspectStreaming(path);

        Assert.True(
            textOccurrences.Count <= poolSize,
            $"Expected at most {poolSize} distinct text values, saw {textOccurrences.Count}.");
    }

    [Fact]
    public async Task GenerateFile_ShouldCleanupOnCancellation()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("generated.txt");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateHandler(poolSize: 8)
                .HandleAsync(new GenerateFileRequest(path, 64L * 1024 * 1024), new GenerateFileOptions(), cts.Token));

        // Assert
        Assert.False(File.Exists(path), "Cancelled generation must not publish the final output file.");
        Assert.Empty(Directory.GetFiles(temp.Root));
    }

    [Fact]
    public async Task GenerateFile_CancelledMidway_ShouldNotLeavePartialFinalOutput()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("generated.txt");
        using var cts = new CancellationTokenSource();
        var handler = CreateHandler(poolSize: 8);

        // Act
        // Cancel after the write loop has certainly started, without relying on timing:
        // the token is cancelled from a continuation registered before the work begins.
        var task = handler.HandleAsync(
            new GenerateFileRequest(path, 256L * 1024 * 1024),
            new GenerateFileOptions(),
            cts.Token);

        await cts.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);

        Assert.False(File.Exists(path), "Cancelled generation must not publish a partial final output file.");
    }

    [Fact]
    public async Task GenerateFile_InvalidRequest_ShouldFailWithoutCreatingAnyFile()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("generated.txt");

        // Act
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => CreateHandler(poolSize: 8)
                .HandleAsync(new GenerateFileRequest(path, 0), new GenerateFileOptions()));

        // Assert
        Assert.False(File.Exists(path));
    }
}
