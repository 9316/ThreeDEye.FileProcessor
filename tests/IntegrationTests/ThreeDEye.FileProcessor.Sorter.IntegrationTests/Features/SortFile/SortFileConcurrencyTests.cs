using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Options;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Contracts;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Handlers;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Pipeline;
using ThreeDEye.FileProcessor.Sorter.IntegrationTests.TestSupport;

namespace ThreeDEye.FileProcessor.Sorter.IntegrationTests.Features.SortFile;

/// <summary>
/// Exercises the bounded producer/consumer path with several workers and a tight channel.
/// These tests assert externally observable correctness only; they never rely on timing,
/// sleeps, or on proving that two tasks literally overlapped.
/// </summary>
public sealed class SortFileConcurrencyTests
{
    private static SortFileHandler CreateHandler() => new(new ExternalFileSorter());

    private static string[] GenerateRecords(int count)
    {
        // long arithmetic keeps the generated number non-negative for every count.
        var texts = new[] { "Apple", "Banana is yellow", "Cherry is the best", "Date", "Elderberry", "Fig" };
        return Enumerable.Range(1, count)
            .Select(i => $"{(long)i * 7919 % 1000000}. {texts[i % texts.Length]}")
            .ToArray();
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(4, 2)]
    [InlineData(8, 1)]
    public async Task BoundedPipeline_WithMultipleWorkers_ProducesGloballySortedOutput(
        int maxDegreeOfParallelism, int channelCapacity)
    {
        // Arrange
        using var temp = new TempWorkspace();
        var records = GenerateRecords(200_000);
        var input = temp.WriteFile("input.txt", records);
        var output = temp.GetPath("output.txt");

        var options = new SortFileOptions
        {
            TemporaryDirectory = temp.GetPath("tmp"),
            ChunkSizeMb = 1,
            BufferSizeKb = 4,
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            ChannelCapacity = channelCapacity,
            MergeFanIn = 3
        };

        // Act
        await CreateHandler().HandleAsync(new SortFileRequest(input, output), options);

        // Assert
        var stats = SortedOutputVerifier.VerifySortedAndCount(output);
        Assert.Equal(records.Length, stats.RecordCount);
    }

    [Fact]
    public async Task BoundedPipeline_PreservesExactRecordMultisetAcrossWorkers()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var records = GenerateRecords(150_000);
        var input = temp.WriteFile("input.txt", records);
        var output = temp.GetPath("output.txt");

        var options = new SortFileOptions
        {
            TemporaryDirectory = temp.GetPath("tmp"),
            ChunkSizeMb = 1,
            BufferSizeKb = 4,
            MaxDegreeOfParallelism = 4,
            ChannelCapacity = 1,
            MergeFanIn = 2
        };

        // Act
        await CreateHandler().HandleAsync(new SortFileRequest(input, output), options);

        // Assert
        var expected = records
            .GroupBy(l => l, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var stats = SortedOutputVerifier.VerifySortedAndCount(output);

        // No chunk lost, no chunk processed twice.
        Assert.Equal(records.Length, stats.RecordCount);
        Assert.Equal(expected.Count, stats.Occurrences.Count);
        foreach (var (line, count) in expected)
        {
            Assert.Equal(count, stats.Occurrences[line]);
        }
    }

    [Fact]
    public async Task BoundedPipeline_WithSmallestChannelCapacity_CompletesWithoutDeadlock()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var records = GenerateRecords(120_000);
        var input = temp.WriteFile("input.txt", records);
        var output = temp.GetPath("output.txt");

        var options = new SortFileOptions
        {
            TemporaryDirectory = temp.GetPath("tmp"),
            ChunkSizeMb = 1,
            BufferSizeKb = 4,
            MaxDegreeOfParallelism = 4,
            ChannelCapacity = 1,
            MergeFanIn = 2
        };

        // Act
        // A generous upper bound guards against a hang without being timing-sensitive:
        // a correct pipeline finishes far below this, a deadlocked one never finishes.
        var sortTask = CreateHandler().HandleAsync(new SortFileRequest(input, output), options);
        var completed = await Task.WhenAny(sortTask, Task.Delay(TimeSpan.FromMinutes(5)));

        // Assert
        Assert.Same(sortTask, completed);
        await sortTask;

        var stats = SortedOutputVerifier.VerifySortedAndCount(output);
        Assert.Equal(records.Length, stats.RecordCount);
    }

    [Fact]
    public async Task BoundedPipeline_SuccessfulRun_LeavesNoTemporaryFiles()
    {
        // Arrange
        using var temp = new TempWorkspace();
        var input = temp.WriteFile("input.txt", GenerateRecords(120_000));
        var output = temp.GetPath("output.txt");
        var tempDir = temp.GetPath("tmp");
        Directory.CreateDirectory(tempDir);

        var options = new SortFileOptions
        {
            TemporaryDirectory = tempDir,
            ChunkSizeMb = 1,
            BufferSizeKb = 4,
            MaxDegreeOfParallelism = 4,
            ChannelCapacity = 2,
            MergeFanIn = 2
        };

        // Act
        await CreateHandler().HandleAsync(new SortFileRequest(input, output), options);

        // Assert
        Assert.Empty(Directory.GetDirectories(tempDir));
        Assert.Empty(Directory.GetFiles(tempDir));

        // The processing file is uniquely named per execution, so the assertion has to be
        // a pattern match rather than a single fixed path.
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(output)!, "*.processing"));
    }
}
