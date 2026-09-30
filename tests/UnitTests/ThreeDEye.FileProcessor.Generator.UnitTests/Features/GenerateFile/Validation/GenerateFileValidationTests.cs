using Microsoft.Extensions.Logging.Abstractions;
using ThreeDEye.FileProcessor.Generator.Configuration.Options;
using ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Contracts;
using ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Generation;
using ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Handlers;
using ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Validations;

namespace ThreeDEye.FileProcessor.Generator.UnitTests.Features.GenerateFile.Validation;

public sealed class GenerateFileRequestValidatorTests
{
    [Fact]
    public void Validate_ValidRequest_DoesNotThrow()
    {
        // Arrange
        var request = new GenerateFileRequest("output.txt", 1024);

        // Act
        GenerateFileRequestValidator.Validate(request);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingOutputPath_ThrowsArgumentException(string outputPath)
    {
        // Arrange
        var request = new GenerateFileRequest(outputPath, 1024);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => GenerateFileRequestValidator.Validate(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Validate_NonPositiveTargetSize_ThrowsArgumentOutOfRangeException(long targetSize)
    {
        // Arrange
        var request = new GenerateFileRequest("output.txt", targetSize);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => GenerateFileRequestValidator.Validate(request));
    }

    [Fact]
    public void Validate_NullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => GenerateFileRequestValidator.Validate(null!));
    }
}

public sealed class GenerateFileHandlerValidationTests
{
    private static GenerateFileHandler CreateHandler() =>
        new(new FileGenerator(new RandomRecordGenerator(poolSize: 4), NullLogger<FileGenerator>.Instance));

    [Fact]
    public async Task HandleAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = CreateHandler();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.HandleAsync(null!, new GenerateFileOptions()));
    }

    [Fact]
    public async Task HandleAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = CreateHandler();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.HandleAsync(new GenerateFileRequest("out.txt", 10), null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-8)]
    public async Task HandleAsync_NonPositiveBufferSize_FailsFastBeforeWriting(int bufferSizeKb)
    {
        // Arrange
        var handler = CreateHandler();
        var options = new GenerateFileOptions { BufferSizeKb = bufferSizeKb };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => handler.HandleAsync(new GenerateFileRequest("out.txt", 10), options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task HandleAsync_NonPositiveStringPoolSize_FailsFastBeforeWriting(int poolSize)
    {
        // Arrange
        var handler = CreateHandler();
        var options = new GenerateFileOptions { StringPoolSize = poolSize };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => handler.HandleAsync(new GenerateFileRequest("out.txt", 10), options));
    }

    [Fact]
    public async Task HandleAsync_InvalidTargetSize_FailsFastBeforeWriting()
    {
        // Arrange
        var handler = CreateHandler();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => handler.HandleAsync(new GenerateFileRequest("out.txt", 0), new GenerateFileOptions()));
    }

    [Fact]
    public void Constructor_NullGenerator_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GenerateFileHandler(null!));
    }
}
