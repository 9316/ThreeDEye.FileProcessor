using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Merging;
using ThreeDEye.FileProcessor.Sorter.UnitTests.Shared;

namespace ThreeDEye.FileProcessor.Sorter.UnitTests.Features.SortFile.Merging;

public sealed class ChunkMergerTests
{
    private readonly ChunkMerger _merger = new();

    [Fact]
    public async Task MergeAsync_SingleRun_ReproducesThatRun()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var run = temp.WriteFile("run0.run", "1. Apple", "2. Banana");
        var output = temp.GetPath("merged.txt");

        // Act
        await _merger.MergeAsync(new[] { run }, output);

        // Assert
        var stats = SortedOutputVerifier.VerifySortedAndCount(output);
        Assert.Equal(2, stats.RecordCount);
    }

    [Fact]
    public async Task MergeAsync_TwoSortedRuns_ProducesGloballySortedOutput()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runA = temp.WriteFile("a.run", "1. Apple", "3. Cherry");
        var runB = temp.WriteFile("b.run", "2. Banana", "4. Date");
        var output = temp.GetPath("merged.txt");

        // Act
        await _merger.MergeAsync(new[] { runA, runB }, output);

        // Assert
        var stats = SortedOutputVerifier.VerifySortedAndCount(output);
        Assert.Equal(4, stats.RecordCount);
        Assert.Equal(
            new[] { "1. Apple", "2. Banana", "3. Cherry", "4. Date" },
            File.ReadAllLines(output));
    }

    [Fact]
    public async Task MergeAsync_SeveralSortedRuns_ProducesGloballySortedOutput()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runs = new[]
        {
            temp.WriteFile("a.run", "1. Apple", "10. Grape"),
            temp.WriteFile("b.run", "2. Banana", "5. Fig"),
            temp.WriteFile("c.run", "3. Cherry", "4. Date"),
            temp.WriteFile("d.run", "6. Elderberry")
        };
        var output = temp.GetPath("merged.txt");

        // Act
        await _merger.MergeAsync(runs, output);

        // Assert
        var stats = SortedOutputVerifier.VerifySortedAndCount(output);
        Assert.Equal(7, stats.RecordCount);
    }

    [Fact]
    public async Task MergeAsync_OneEmptyRun_MergesRemainingRunsWithoutLoss()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runA = temp.WriteFile("a.run", "1. Apple", "3. Cherry");
        var empty = temp.WriteFile("empty.run");
        var runB = temp.WriteFile("b.run", "2. Banana");
        var output = temp.GetPath("merged.txt");

        // Act
        await _merger.MergeAsync(new[] { runA, empty, runB }, output);

        // Assert
        var stats = SortedOutputVerifier.VerifySortedAndCount(output);
        Assert.Equal(3, stats.RecordCount);
    }

    [Fact]
    public async Task MergeAsync_AllRunsEmpty_ProducesEmptyOutput()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runs = new[] { temp.WriteFile("a.run"), temp.WriteFile("b.run") };
        var output = temp.GetPath("merged.txt");

        // Act
        await _merger.MergeAsync(runs, output);

        // Assert
        Assert.True(File.Exists(output));
        Assert.Equal(0, new FileInfo(output).Length);
    }

    [Fact]
    public async Task MergeAsync_TerminatesLinesWithLineFeedOnly()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runs = new[]
        {
            temp.WriteFile("lf-a.run", "1. Apple"),
            temp.WriteFile("lf-b.run", "2. Banana")
        };
        var output = temp.GetPath("merged-lf.txt");

        // Act
        await _merger.MergeAsync(runs, output);

        // Assert
        // Environment.NewLine would make merged output differ byte-for-byte from the
        // generated input format and inflate the file by one byte per record on Windows.
        var text = await File.ReadAllTextAsync(output);
        Assert.Equal("1. Apple\n2. Banana\n", text);
        Assert.DoesNotContain('\r', text);
    }

    [Fact]
    public async Task MergeAsync_NoRunsAtAll_ProducesEmptyOutput()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var output = temp.GetPath("merged.txt");

        // Act
        await _merger.MergeAsync(Array.Empty<string>(), output);

        // Assert
        Assert.True(File.Exists(output));
        Assert.Equal(0, new FileInfo(output).Length);
    }

    [Fact]
    public async Task MergeAsync_DuplicateTextAcrossRuns_OrdersByNumberWithinText()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runA = temp.WriteFile("a.run", "1. Apple", "300. Apple");
        var runB = temp.WriteFile("b.run", "2. Apple", "100. Apple");
        var output = temp.GetPath("merged.txt");

        // Act
        await _merger.MergeAsync(new[] { runA, runB }, output);

        // Assert
        SortedOutputVerifier.VerifySortedAndCount(output);
        Assert.Equal(
            new[] { "1. Apple", "2. Apple", "100. Apple", "300. Apple" },
            File.ReadAllLines(output));
    }

    [Fact]
    public async Task MergeAsync_DuplicateCompleteRecordsAcrossRuns_PreservesEveryOccurrence()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runA = temp.WriteFile("a.run", "1. Apple", "1. Apple");
        var runB = temp.WriteFile("b.run", "1. Apple");
        var output = temp.GetPath("merged.txt");

        // Act
        await _merger.MergeAsync(new[] { runA, runB }, output);

        // Assert
        var stats = SortedOutputVerifier.VerifySortedAndCount(output);
        Assert.Equal(3, stats.RecordCount);
        Assert.Equal(3, stats.Occurrences["1. Apple"]);
    }

    [Fact]
    public async Task MergeAsync_LongNumberValues_SortNumericallyNotLexicographically()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runA = temp.WriteFile("a.run", "9. Apple", $"{long.MaxValue}. Apple");
        var runB = temp.WriteFile("b.run", "10. Apple", "100. Apple");
        var output = temp.GetPath("merged.txt");

        // Act
        await _merger.MergeAsync(new[] { runA, runB }, output);

        // Assert
        SortedOutputVerifier.VerifySortedAndCount(output);
        Assert.Equal(
            new[] { "9. Apple", "10. Apple", "100. Apple", $"{long.MaxValue}. Apple" },
            File.ReadAllLines(output));
    }

    [Fact]
    public async Task MergeAsync_PreservesExactMultisetOfInputRecords()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var runA = new[] { "1. Apple", "5. Apple", "2. Banana" };
        var runB = new[] { "1. Apple", "3. Cherry", "9. Cherry" };
        var pathA = temp.WriteFile("a.run", runA);
        var pathB = temp.WriteFile("b.run", runB);
        var output = temp.GetPath("merged.txt");

        // Act
        await _merger.MergeAsync(new[] { pathA, pathB }, output);

        // Assert
        var expected = runA.Concat(runB)
            .GroupBy(l => l, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var actual = SortedOutputVerifier.CountOccurrences(output);

        Assert.Equal(expected.Count, actual.Count);
        foreach (var (line, count) in expected)
        {
            Assert.Equal(count, actual[line]);
        }
    }

    [Fact]
    public async Task MergeAsync_MalformedRunLine_ThrowsInvalidDataExceptionWithContext()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var run = temp.WriteFile("bad.run", "1. Apple", "not-a-record");
        var output = temp.GetPath("merged.txt");

        // Act
        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => _merger.MergeAsync(new[] { run }, output));

        // Assert
        Assert.Contains("not-a-record", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MergeAsync_AlreadyCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var run = temp.WriteFile("a.run", "1. Apple", "2. Banana");
        var output = temp.GetPath("merged.txt");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _merger.MergeAsync(new[] { run }, output, 32, cts.Token));
    }
}
