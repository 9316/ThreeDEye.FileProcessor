using System.Text;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Parsing;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.TemporaryFiles;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Writing;
using ThreeDEye.FileProcessor.Sorter.UnitTests.Shared;

namespace ThreeDEye.FileProcessor.Sorter.UnitTests.Features.SortFile.Writing;

/// <summary>
/// Covers the writer contract after ownership was split: <c>TemporaryFileManager</c> allocates the
/// run path and <c>SortedChunkWriter</c> only writes the supplied records to that path.
/// </summary>
public sealed class SortedChunkWriterTests
{
    private readonly SortedChunkWriter _writer = new();

    [Fact]
    public async Task WriteChunkAsync_WritesNumberDotSpaceTextFormat()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runPath = temp.GetPath("run0.run");
        var records = new List<FileRecord> { new(415, "Apple"), new(30432, "Something something") };

        // Act
        await _writer.WriteChunkAsync(runPath, records);

        // Assert
        Assert.Equal(
            new[] { "415. Apple", "30432. Something something" },
            await File.ReadAllLinesAsync(runPath));
    }

    [Fact]
    public async Task WriteChunkAsync_PreservesSuppliedOrderWithoutReordering()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runPath = temp.GetPath("run0.run");

        // Deliberately unsorted: the writer must not impose any ordering of its own.
        var records = new List<FileRecord> { new(9, "Zebra"), new(1, "Apple"), new(5, "Mango") };

        // Act
        await _writer.WriteChunkAsync(runPath, records);

        // Assert
        Assert.Equal(
            new[] { "9. Zebra", "1. Apple", "5. Mango" },
            await File.ReadAllLinesAsync(runPath));
    }

    [Fact]
    public async Task WriteChunkAsync_EmptyCollection_CreatesEmptyRunFile()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runPath = temp.GetPath("empty.run");

        // Act
        await _writer.WriteChunkAsync(runPath, new List<FileRecord>());

        // Assert
        Assert.True(File.Exists(runPath));
        Assert.Equal(0, new FileInfo(runPath).Length);
    }

    [Fact]
    public async Task WriteChunkAsync_DuplicateRecords_WritesEveryOccurrence()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runPath = temp.GetPath("dupes.run");
        var records = new List<FileRecord> { new(1, "Apple"), new(1, "Apple"), new(1, "Apple") };

        // Act
        await _writer.WriteChunkAsync(runPath, records);

        // Assert
        var lines = await File.ReadAllLinesAsync(runPath);
        Assert.Equal(3, lines.Length);
        Assert.All(lines, l => Assert.Equal("1. Apple", l));
    }

    [Fact]
    public async Task WriteChunkAsync_TextContainingPeriods_IsWrittenVerbatim()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runPath = temp.GetPath("periods.run");
        var records = new List<FileRecord> { new(7, "Apple. Banana. Cherry") };

        // Act
        await _writer.WriteChunkAsync(runPath, records);

        // Assert
        // The record separator is only the first ". ", so inner periods must survive untouched.
        Assert.Equal("7. Apple. Banana. Cherry", Assert.Single(await File.ReadAllLinesAsync(runPath)));
    }

    [Fact]
    public async Task WriteChunkAsync_WritesUtf8WithoutByteOrderMark()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runPath = temp.GetPath("bom.run");

        // Act
        await _writer.WriteChunkAsync(runPath, new List<FileRecord> { new(1, "Apple") });

        // Assert
        // A BOM would corrupt the first record of the run when it is parsed back during merge.
        var bytes = await File.ReadAllBytesAsync(runPath);
        var preamble = Encoding.UTF8.GetPreamble();
        Assert.False(
            bytes.Length >= preamble.Length && bytes.Take(preamble.Length).SequenceEqual(preamble),
            "Run files must not start with a UTF-8 byte-order mark.");
    }

    [Fact]
    public async Task WriteChunkAsync_TerminatesLinesWithLineFeedOnly()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runPath = temp.GetPath("newline.run");

        // Act
        await _writer.WriteChunkAsync(runPath, new List<FileRecord> { new(1, "Apple"), new(2, "Banana") });

        // Assert
        // Environment.NewLine would add a byte per record on Windows, breaking the byte
        // accounting ChunkReader uses to size chunks and making output platform dependent.
        var text = await File.ReadAllTextAsync(runPath);
        Assert.Equal("1. Apple\n2. Banana\n", text);
        Assert.DoesNotContain('\r', text);
    }

    [Fact]
    public async Task WriteChunkAsync_ExistingRunFile_IsOverwrittenNotAppended()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runPath = temp.GetPath("run0.run");
        await File.WriteAllLinesAsync(runPath, new[] { "999. Stale", "998. Stale" });

        // Act
        await _writer.WriteChunkAsync(runPath, new List<FileRecord> { new(1, "Apple") });

        // Assert
        Assert.Equal(new[] { "1. Apple" }, await File.ReadAllLinesAsync(runPath));
    }

    [Fact]
    public async Task WriteChunkAsync_WritesToPathAllocatedByTemporaryFileManager()
    {
        // Arrange
        using var temp = new TempWorkspace();
        using var workspace = new TemporaryFileManager(temp.Root);
        var runPath = workspace.AllocateRunPath(0);

        // Act
        await _writer.WriteChunkAsync(runPath, new List<FileRecord> { new(1, "Apple") });

        // Assert
        // Confirms the split ownership model: the manager decides the path, the writer fills it.
        Assert.True(File.Exists(runPath));
        Assert.Equal(new[] { "1. Apple" }, await File.ReadAllLinesAsync(runPath));
    }

    [Fact]
    public async Task WriteChunkAsync_AlreadyCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runPath = temp.GetPath("cancelled.run");
        var records = new List<FileRecord> { new(1, "Apple"), new(2, "Banana") };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _writer.WriteChunkAsync(runPath, records, 32, cts.Token));
    }

    [Fact]
    public async Task WriteChunkAsync_NullRecords_ThrowsArgumentNullException()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runPath = temp.GetPath("run0.run");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _writer.WriteChunkAsync(runPath, null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WriteChunkAsync_MissingRunPath_ThrowsArgumentException(string runPath)
    {
        // Arrange
        var records = new List<FileRecord> { new(1, "Apple") };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _writer.WriteChunkAsync(runPath, records));
    }
}
