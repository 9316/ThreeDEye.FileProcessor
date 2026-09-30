using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Generation;
using ThreeDEye.FileProcessor.Generator.UnitTests.Shared;

namespace ThreeDEye.FileProcessor.Generator.UnitTests.Features.GenerateFile.Generation;

public sealed class FileGeneratorTests
{
    private static FileGenerator CreateGenerator(int poolSize = 8) =>
        new(new RandomRecordGenerator(poolSize), NullLogger<FileGenerator>.Instance);

    [Fact]
    public async Task GenerateAsync_WritesLinesInNumberDotSpaceTextFormat()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("out.txt");

        // Act
        await CreateGenerator().GenerateAsync(path, targetSizeBytes: 2048);

        // Assert
        var lines = await File.ReadAllLinesAsync(path);
        Assert.NotEmpty(lines);
        Assert.All(lines, line =>
        {
            var separatorIndex = line.IndexOf(". ", StringComparison.Ordinal);
            Assert.True(separatorIndex > 0, $"Line '{line}' has no '. ' separator.");
            Assert.True(long.TryParse(line[..separatorIndex], out _), $"Line '{line}' has a non-numeric prefix.");
            Assert.NotEmpty(line[(separatorIndex + 2)..]);
        });
    }

    [Fact]
    public async Task GenerateAsync_NumbersStartAtOneAndIncreaseMonotonically()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("out.txt");

        // Act
        await CreateGenerator().GenerateAsync(path, targetSizeBytes: 2048);

        // Assert
        var numbers = (await File.ReadAllLinesAsync(path))
            .Select(l => long.Parse(l[..l.IndexOf(". ", StringComparison.Ordinal)]))
            .ToList();

        Assert.Equal(1, numbers[0]);
        for (var i = 1; i < numbers.Count; i++)
        {
            Assert.Equal(numbers[i - 1] + 1, numbers[i]);
        }
    }

    [Fact]
    public async Task GenerateAsync_WritesCompleteRecordsOnlyWithNoTrailingPartialLine()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("out.txt");

        // Act
        await CreateGenerator().GenerateAsync(path, targetSizeBytes: 4096);

        // Assert
        // The generator writes '\n' explicitly so output and byte accounting stay platform independent.
        var content = await File.ReadAllTextAsync(path);
        Assert.EndsWith("\n", content, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_ReachesAtLeastTheRequestedTargetSize()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("out.txt");
        const long target = 8192;

        // Act
        await CreateGenerator().GenerateAsync(path, target);

        // Assert
        // The generator stops only once the accounted byte total reaches the target,
        // so the produced file must be at least that large.
        Assert.True(new FileInfo(path).Length >= target);
    }

    [Fact]
    public async Task GenerateAsync_DoesNotOvershootTargetByMoreThanOneRecord()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("out.txt");
        const long target = 8192;

        // Act
        await CreateGenerator().GenerateAsync(path, target);

        // Assert
        var lines = await File.ReadAllLinesAsync(path);
        var longestRecordBytes = lines.Max(l => Encoding.UTF8.GetByteCount(l)) + Encoding.UTF8.GetByteCount(Environment.NewLine);

        Assert.True(
            new FileInfo(path).Length <= target + longestRecordBytes,
            "Generator overshot the target size by more than a single record.");
    }

    [Fact]
    public async Task GenerateAsync_CreatesMissingParentDirectory()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = Path.Combine(temp.Root, "nested", "deeper", "out.txt");

        // Act
        await CreateGenerator().GenerateAsync(path, targetSizeBytes: 512);

        // Assert
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task GenerateAsync_NegativeTargetSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var temp = new TempWorkspace();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => CreateGenerator().GenerateAsync(temp.GetPath("out.txt"), -1));
    }

    [Fact]
    public async Task GenerateAsync_NullPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => CreateGenerator().GenerateAsync(null!, 512));
    }

    [Fact]
    public async Task GenerateAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("out.txt");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateGenerator().GenerateAsync(path, 1024 * 1024, 32, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task GenerateAsync_CancelledToken_DoesNotPublishPartialOutputFile()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("out.txt");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateGenerator().GenerateAsync(path, 1024 * 1024, 32, cancellationToken: cts.Token));

        // Assert
        Assert.False(File.Exists(path), "A cancelled generation must not publish the final output file.");
    }

    [Fact]
    public async Task GenerateAsync_CancelledToken_LeavesNoTemporaryArtifacts()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("out.txt");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateGenerator().GenerateAsync(path, 1024 * 1024, 32, cancellationToken: cts.Token));

        // Assert
        Assert.Empty(Directory.GetFiles(temp.Root));
    }

    [Fact]
    public async Task GenerateAsync_ExistingOutputFile_IsReplacedOnSuccess()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var path = temp.GetPath("out.txt");
        await File.WriteAllTextAsync(path, "stale content");

        // Act
        await CreateGenerator().GenerateAsync(path, targetSizeBytes: 1024);

        // Assert
        var content = await File.ReadAllTextAsync(path);
        Assert.DoesNotContain("stale content", content, StringComparison.Ordinal);
    }
}
