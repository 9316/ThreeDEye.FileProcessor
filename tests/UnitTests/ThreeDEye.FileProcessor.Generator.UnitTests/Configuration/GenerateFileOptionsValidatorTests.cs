using ThreeDEye.FileProcessor.Generator.Configuration.Options;

namespace ThreeDEye.FileProcessor.Generator.UnitTests.Configuration;

public sealed class GenerateFileOptionsValidatorTests
{
    private static GenerateFileOptions Valid() => new()
    {
        OutputPath = "generated.txt",
        TargetFileSizeBytes = 1024,
        BufferSizeKb = 32,
        StringPoolSize = 100,
        StartingNumber = 1
    };

    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        // Arrange
        var options = new GenerateFileOptions();

        // Act
        var result = new GenerateFileOptionsValidator().Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_FullyPopulatedOptions_Succeeds()
    {
        // Arrange
        var options = Valid();

        // Act
        var result = new GenerateFileOptionsValidator().Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingOutputPath_Fails(string outputPath)
    {
        // Arrange
        var options = Valid();
        options.OutputPath = outputPath;

        // Act
        var result = new GenerateFileOptionsValidator().Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("OutputPath", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveTargetFileSize_Fails(long targetSize)
    {
        // Arrange
        var options = Valid();
        options.TargetFileSizeBytes = targetSize;

        // Act
        var result = new GenerateFileOptionsValidator().Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("TargetFileSizeBytes", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-8)]
    public void Validate_NonPositiveBufferSize_Fails(int bufferSizeKb)
    {
        // Arrange
        var options = Valid();
        options.BufferSizeKb = bufferSizeKb;

        // Act
        var result = new GenerateFileOptionsValidator().Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("BufferSizeKb", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_NonPositiveStringPoolSize_Fails(int poolSize)
    {
        // Arrange
        var options = Valid();
        options.StringPoolSize = poolSize;

        // Act
        var result = new GenerateFileOptionsValidator().Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("StringPoolSize", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_NegativeStartingNumber_Fails()
    {
        // Arrange
        var options = Valid();
        options.StartingNumber = -1;

        // Act
        var result = new GenerateFileOptionsValidator().Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("StartingNumber", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReportsEveryInvalidValueAtOnce()
    {
        // Arrange
        var options = new GenerateFileOptions
        {
            OutputPath = "",
            TargetFileSizeBytes = 0,
            BufferSizeKb = 0,
            StringPoolSize = 0,
            StartingNumber = -1
        };

        // Act
        var result = new GenerateFileOptionsValidator().Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.Failures!.Count() >= 5);
    }

    [Fact]
    public void Snapshot_ReturnsIndependentCopy()
    {
        // Arrange
        var options = Valid();
        var snapshot = options.Snapshot();

        // Act
        options.BufferSizeKb = 999;
        options.OutputPath = "changed.txt";

        // Assert
        Assert.Equal(32, snapshot.BufferSizeKb);
        Assert.Equal("generated.txt", snapshot.OutputPath);
    }
}
