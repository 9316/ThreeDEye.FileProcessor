using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Options;

namespace ThreeDEye.FileProcessor.Sorter.UnitTests.Features.SortFile.Configuration;

public sealed class SortFileOptionsValidatorTests
{
    private static SortFileOptions Valid() => new()
    {
        InputPath = "input.txt",
        OutputPath = "output.txt",
        ChunkSizeMb = 10,
        BufferSizeKb = 32,
        MaxDegreeOfParallelism = 4,
        ChannelCapacity = 4,
        MergeFanIn = 16
    };

    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        var result = new SortFileOptionsValidator().Validate(null, new SortFileOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_FullyPopulatedOptions_Succeeds()
    {
        var result = new SortFileOptionsValidator().Validate(null, Valid());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingInputPath_Fails(string inputPath)
    {
        var options = Valid();
        options.InputPath = inputPath;

        var result = new SortFileOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("InputPath", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingOutputPath_Fails(string outputPath)
    {
        var options = Valid();
        options.OutputPath = outputPath;

        var result = new SortFileOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("OutputPath", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_InputAndOutputPathsEqual_Fails()
    {
        var options = Valid();
        options.InputPath = "same.txt";
        options.OutputPath = "same.txt";

        var result = new SortFileOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("must differ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveChunkSize_Fails(int chunkSizeMb)
    {
        var options = Valid();
        options.ChunkSizeMb = chunkSizeMb;

        var result = new SortFileOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("ChunkSizeMb", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-32)]
    public void Validate_NonPositiveBufferSize_Fails(int bufferSizeKb)
    {
        var options = Valid();
        options.BufferSizeKb = bufferSizeKb;

        var result = new SortFileOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("BufferSizeKb", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Validate_NonPositiveMaxDegreeOfParallelism_Fails(int workers)
    {
        var options = Valid();
        options.MaxDegreeOfParallelism = workers;

        var result = new SortFileOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("MaxDegreeOfParallelism", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void Validate_NonPositiveChannelCapacity_Fails(int capacity)
    {
        var options = Valid();
        options.ChannelCapacity = capacity;

        var result = new SortFileOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("ChannelCapacity", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-3)]
    public void Validate_MergeFanInBelowTwo_Fails(int mergeFanIn)
    {
        var options = Valid();
        options.MergeFanIn = mergeFanIn;

        var result = new SortFileOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("MergeFanIn", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_MergeFanInOfTwo_Succeeds()
    {
        var options = Valid();
        options.MergeFanIn = 2;

        Assert.True(new SortFileOptionsValidator().Validate(null, options).Succeeded);
    }

    [Fact]
    public void Validate_NullTemporaryDirectory_Succeeds()
    {
        var options = Valid();
        options.TemporaryDirectory = null;

        Assert.True(new SortFileOptionsValidator().Validate(null, options).Succeeded);
    }

    [Fact]
    public void Validate_ReportsEveryInvalidValueAtOnce()
    {
        var options = new SortFileOptions
        {
            InputPath = "",
            OutputPath = "",
            ChunkSizeMb = 0,
            BufferSizeKb = 0,
            MaxDegreeOfParallelism = 0,
            ChannelCapacity = 0,
            MergeFanIn = 1
        };

        var result = new SortFileOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.True(result.Failures!.Count() >= 7);
    }

    [Fact]
    public void ChunkSizeBytes_ConvertsMegabytesWithoutOverflow()
    {
        var options = Valid();
        options.ChunkSizeMb = 4096;

        Assert.Equal(4096L * 1024 * 1024, options.ChunkSizeBytes);
    }

    [Fact]
    public void Snapshot_ReturnsIndependentCopy()
    {
        var options = Valid();
        var snapshot = options.Snapshot();

        options.ChunkSizeMb = 999;
        options.MergeFanIn = 3;
        options.InputPath = "changed.txt";

        Assert.Equal(10, snapshot.ChunkSizeMb);
        Assert.Equal(16, snapshot.MergeFanIn);
        Assert.Equal("input.txt", snapshot.InputPath);
    }
}
