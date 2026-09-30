using System.Text;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Options;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Contracts;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Handlers;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Pipeline;
using ThreeDEye.FileProcessor.Sorter.IntegrationTests.TestSupport;

namespace ThreeDEye.FileProcessor.Sorter.IntegrationTests.Features.SortFile;

public sealed class SortFileEndToEndTests
{
    private static SortFileHandler CreateHandler() => new(new ExternalFileSorter());

    private static SortFileOptions OptionsIn(TempWorkspace temp) => new()
    {
        TemporaryDirectory = temp.GetPath("tmp"),
        ChunkSizeMb = 1,
        BufferSizeKb = 4,
        MaxDegreeOfParallelism = 1,
        ChannelCapacity = 2,
        MergeFanIn = 4
    };

    private static string[] GenerateRecords(int count)
    {
        // Deterministic, intentionally unsorted, with duplicate text values.
        // Uses long arithmetic so the number never overflows into a negative value,
        // which the parser contract (digits only) would reject.
        var texts = new[] { "Apple", "Banana is yellow", "Cherry is the best", "Date", "Elderberry" };
        return Enumerable.Range(1, count)
            .Select(i => $"{(long)i * 7919 % 100000}. {texts[i % texts.Length]}")
            .ToArray();
    }

    // ---------- A ----------
    [Fact]
    public async Task AssignmentExample_ShouldProduceExactExpectedOutput()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var input = temp.WriteFile("input.txt",
            "415. Apple",
            "30432. Something something something",
            "1. Apple",
            "32. Cherry is the best",
            "2. Banana is yellow");
        var output = temp.GetPath("output.txt");

        // Act
        await CreateHandler().HandleAsync(new SortFileRequest(input, output), OptionsIn(temp));

        // Assert
        Assert.Equal(
            new[]
            {
                "1. Apple",
                "415. Apple",
                "2. Banana is yellow",
                "32. Cherry is the best",
                "30432. Something something something"
            },
            await File.ReadAllLinesAsync(output));
    }

    // ---------- B ----------
    [Fact]
    public async Task EmptyInput_ShouldProduceEmptyOutput()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var input = temp.WriteFile("input.txt");
        var output = temp.GetPath("output.txt");

        // Act
        await CreateHandler().HandleAsync(new SortFileRequest(input, output), OptionsIn(temp));

        // Assert
        Assert.True(File.Exists(output));
        Assert.Equal(0, new FileInfo(output).Length);
    }

    // ---------- C ----------
    [Fact]
    public async Task SingleRecord_ShouldBePreserved()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var input = temp.WriteFile("input.txt", "42. Only one");
        var output = temp.GetPath("output.txt");

        // Act
        await CreateHandler().HandleAsync(new SortFileRequest(input, output), OptionsIn(temp));

        // Assert
        Assert.Equal(new[] { "42. Only one" }, await File.ReadAllLinesAsync(output));
    }

    // ---------- D ----------
    [Fact]
    public async Task DuplicateRecords_ShouldBePreserved()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var input = temp.WriteFile("input.txt",
            "1. Apple", "1. Apple", "2. Apple", "1. Apple");
        var output = temp.GetPath("output.txt");

        // Act
        await CreateHandler().HandleAsync(new SortFileRequest(input, output), OptionsIn(temp));

        // Assert
        var stats = SortedOutputVerifier.VerifySortedAndCount(output);
        Assert.Equal(4, stats.RecordCount);
        Assert.Equal(3, stats.Occurrences["1. Apple"]);
        Assert.Equal(1, stats.Occurrences["2. Apple"]);
    }

    // ---------- E ----------
    [Fact]
    public async Task TextContainingPeriods_ShouldSortCorrectly()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var input = temp.WriteFile("input.txt",
            "2. B. second",
            "1. A. first",
            "3. A. first");
        var output = temp.GetPath("output.txt");

        // Act
        await CreateHandler().HandleAsync(new SortFileRequest(input, output), OptionsIn(temp));

        // Assert
        Assert.Equal(
            new[] { "1. A. first", "3. A. first", "2. B. second" },
            await File.ReadAllLinesAsync(output));
    }

    // ---------- F ----------
    [Fact]
    public async Task LongNumbers_ShouldSortCorrectly()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var input = temp.WriteFile("input.txt",
            $"{long.MaxValue}. Apple",
            "10. Apple",
            "9. Apple",
            "0. Apple");
        var output = temp.GetPath("output.txt");

        // Act
        await CreateHandler().HandleAsync(new SortFileRequest(input, output), OptionsIn(temp));

        // Assert
        Assert.Equal(
            new[] { "0. Apple", "9. Apple", "10. Apple", $"{long.MaxValue}. Apple" },
            await File.ReadAllLinesAsync(output));
    }

    // ---------- G ----------
    [Fact]
    public async Task MalformedInput_ShouldFailWithUsefulContext()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var input = temp.WriteFile("input.txt", "1. Apple", "this line is malformed", "2. Banana");
        var output = temp.GetPath("output.txt");

        // Act
        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => CreateHandler().HandleAsync(new SortFileRequest(input, output), OptionsIn(temp)));

        // Assert
        Assert.Contains("this line is malformed", ex.Message, StringComparison.Ordinal);
        Assert.Contains("line 2", ex.Message, StringComparison.Ordinal);
    }

    // ---------- H ----------
    [Fact]
    public async Task MultiChunkSort_ShouldProduceGloballySortedOutput()
    {
        // Arrange
        using var temp = new TempWorkspace();
        // ChunkSizeMb is bounded below by 1 MB, so the input must exceed that budget
        // several times over for the pipeline to emit more than one sorted run.
        var records = GenerateRecords(300_000);
        var input = temp.WriteFile("input.txt", records);
        var output = temp.GetPath("output.txt");

        var options = OptionsIn(temp);
        options.ChunkSizeMb = 1;
        options.BufferSizeKb = 4;

        Assert.True(
            new FileInfo(input).Length > 4L * 1024 * 1024,
            "Input must comfortably exceed the chunk budget for this test to be meaningful.");

        var logger = new CapturingLogger<ExternalFileSorter>();
        var handler = new SortFileHandler(new ExternalFileSorter(logger));

        // Act
        await handler.HandleAsync(new SortFileRequest(input, output), options);

        // Assert
        // The pipeline reports the number of sorted runs it produced.
        var runCount = logger.MaxIntFor("RunCount");
        Assert.True(runCount > 1, $"Expected more than one sorted run but the pipeline produced {runCount}.");

        var stats = SortedOutputVerifier.VerifySortedAndCount(output);
        Assert.Equal(records.Length, stats.RecordCount);
    }

    // ---------- I ----------
    [Fact]
    public async Task MultiPassMerge_ShouldProduceCorrectOutput()
    {
        // Arrange
        using var temp = new TempWorkspace();
        // At a 1 MB chunk floor this produces several runs; with MergeFanIn = 2 that
        // necessarily requires more than one merge pass.
        var records = GenerateRecords(400_000);
        var input = temp.WriteFile("input.txt", records);
        var output = temp.GetPath("output.txt");

        var options = OptionsIn(temp);
        options.MergeFanIn = 2;
        options.ChunkSizeMb = 1;
        options.MaxDegreeOfParallelism = 2;
        Directory.CreateDirectory(options.TemporaryDirectory!);

        var logger = new CapturingLogger<ExternalFileSorter>();
        var handler = new SortFileHandler(new ExternalFileSorter(logger));

        // Act
        await handler.HandleAsync(new SortFileRequest(input, output), options);

        // Assert
        // With fan-in 2, N runs need ceil(log2(N)) passes; more than 2 runs guarantees >1 pass.
        var runCount = logger.MaxIntFor("RunCount");
        var passCount = logger.MaxIntFor("Pass");

        Assert.True(runCount > 2, $"Expected more than two sorted runs but got {runCount}.");
        Assert.True(passCount > 1, $"Expected more than one merge pass but got {passCount}.");

        var stats = SortedOutputVerifier.VerifySortedAndCount(output);
        Assert.Equal(records.Length, stats.RecordCount);

        // Cleanup: temporary workspace removed and no processing artifact left behind.
        Assert.Empty(Directory.GetDirectories(options.TemporaryDirectory!));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(output)!, "*.processing"));
    }

    // ---------- J ----------
    [Fact]
    public async Task RecordCount_ShouldBePreserved()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var records = GenerateRecords(3000);
        var input = temp.WriteFile("input.txt", records);
        var output = temp.GetPath("output.txt");

        // Act
        await CreateHandler().HandleAsync(new SortFileRequest(input, output), OptionsIn(temp));

        // Assert
        var expected = records
            .GroupBy(l => l, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var stats = SortedOutputVerifier.VerifySortedAndCount(output);

        Assert.Equal(records.Length, stats.RecordCount);
        Assert.Equal(expected.Count, stats.Occurrences.Count);
        foreach (var (line, count) in expected)
        {
            Assert.Equal(count, stats.Occurrences[line]);
        }
    }

    // ---------- K ----------
    [Fact]
    public async Task SuccessfulSort_ShouldCleanupTemporaryWorkspace()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var input = temp.WriteFile("input.txt", GenerateRecords(2000));
        var output = temp.GetPath("output.txt");
        var options = OptionsIn(temp);
        Directory.CreateDirectory(options.TemporaryDirectory!);

        // Act
        await CreateHandler().HandleAsync(new SortFileRequest(input, output), options);

        // Assert
        Assert.Empty(Directory.GetDirectories(options.TemporaryDirectory!));
        Assert.Empty(Directory.GetFiles(options.TemporaryDirectory!));
    }

    // ---------- L ----------
    [Fact]
    public async Task Cancellation_ShouldCleanupTemporaryWorkspace()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var input = temp.WriteFile("input.txt", GenerateRecords(50000));
        var output = temp.GetPath("output.txt");
        var options = OptionsIn(temp);
        Directory.CreateDirectory(options.TemporaryDirectory!);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateHandler().HandleAsync(new SortFileRequest(input, output), options, cts.Token));

        // Assert
        Assert.Empty(Directory.GetDirectories(options.TemporaryDirectory!));
        Assert.False(File.Exists(output), "Cancelled sort must not publish final output.");
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(output)!, "*.processing"));
    }

    // ---------- M ----------
    [Fact]
    public async Task Failure_ShouldNotPublishPartialFinalOutput()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var input = temp.WriteFile("input.txt", "1. Apple", "BROKEN LINE", "2. Banana");
        var output = temp.GetPath("output.txt");

        // Act
        await Assert.ThrowsAsync<InvalidDataException>(
            () => CreateHandler().HandleAsync(new SortFileRequest(input, output), OptionsIn(temp)));

        // Assert
        Assert.False(File.Exists(output), "Failed sort must not publish final output.");
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(output)!, "*.processing"));
    }

    // ---------- N ----------
    [Fact]
    public async Task ExistingFinalOutput_ShouldOnlyBeReplacedAfterSuccessfulCompletion()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var input = temp.WriteFile("input.txt", "1. Apple", "BROKEN LINE");
        var output = temp.GetPath("output.txt");
        const string preexisting = "PREVIOUS RESULT";
        await File.WriteAllTextAsync(output, preexisting);

        // Act
        await Assert.ThrowsAsync<InvalidDataException>(
            () => CreateHandler().HandleAsync(new SortFileRequest(input, output), OptionsIn(temp)));

        // Assert
        Assert.Equal(preexisting, await File.ReadAllTextAsync(output));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(output)!, "*.processing"));
    }

    [Fact]
    public async Task ExistingFinalOutput_ShouldBeReplacedWhenSortSucceeds()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var input = temp.WriteFile("input.txt", "2. Banana", "1. Apple");
        var output = temp.GetPath("output.txt");
        await File.WriteAllTextAsync(output, "PREVIOUS RESULT");

        // Act
        await CreateHandler().HandleAsync(new SortFileRequest(input, output), OptionsIn(temp));

        // Assert
        Assert.Equal(new[] { "1. Apple", "2. Banana" }, await File.ReadAllLinesAsync(output));
    }

    [Fact]
    public async Task SameInputAndOutputPath_ShouldBeRejected()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var input = temp.WriteFile("input.txt", "1. Apple");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateHandler().HandleAsync(new SortFileRequest(input, input), OptionsIn(temp)));
    }

    [Fact]
    public async Task MissingInputFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        using var temp = new TempWorkspace();

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => CreateHandler().HandleAsync(
                new SortFileRequest(temp.GetPath("missing.txt"), temp.GetPath("out.txt")),
                OptionsIn(temp)));
    }

    [Fact]
    public async Task BomPrefixedInput_ShouldBeSortedAndOutputWrittenWithoutBom()
    {
        // Arrange
        // Input may be produced externally and legitimately carry a UTF-8 preamble.
        // ChunkReader enables BOM detection so the preamble is consumed, not parsed.
        using var temp = new TempWorkspace();
        var input = temp.GetPath("input-bom.txt");
        await File.WriteAllTextAsync(
            input,
            "415. Apple\n1. Apple\n2. Banana is yellow\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var output = temp.GetPath("output.txt");

        // Act
        await CreateHandler().HandleAsync(new SortFileRequest(input, output), OptionsIn(temp));

        // Assert
        Assert.Equal(
            new[] { "1. Apple", "415. Apple", "2. Banana is yellow" },
            await File.ReadAllLinesAsync(output));

        var outputBytes = await File.ReadAllBytesAsync(output);
        Assert.False(outputBytes.AsSpan(0, 3).SequenceEqual(Encoding.UTF8.Preamble));
    }

    [Fact]
    public async Task SortedOutput_ShouldPreserveInputByteCountExactly()
    {
        // Arrange
        // Sorting only reorders records, so the output must be byte-for-byte the same size
        // as the input. A line-terminator mismatch between reader and writer shows up here
        // as exactly one extra byte per record.
        using var temp = new TempWorkspace();
        var input = temp.GetPath("input-bytes.txt");
        var records = GenerateRecords(500);
        await File.WriteAllTextAsync(
            input,
            string.Concat(records.Select(r => r + "\n")),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var output = temp.GetPath("output-bytes.txt");

        // Act
        await CreateHandler().HandleAsync(new SortFileRequest(input, output), OptionsIn(temp));

        // Assert
        Assert.Equal(new FileInfo(input).Length, new FileInfo(output).Length);
        Assert.DoesNotContain('\r', await File.ReadAllTextAsync(output));
    }
}