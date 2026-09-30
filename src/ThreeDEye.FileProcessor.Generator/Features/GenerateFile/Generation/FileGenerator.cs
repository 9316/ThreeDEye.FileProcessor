using System.Text;
using Microsoft.Extensions.Logging;
using ThreeDEye.FileProcessor.Generator.Configuration.Defaults;

namespace ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Generation;

internal sealed class FileGenerator
{
    private const string NewLine = "\n";

    private static readonly Encoding Utf8WithoutBom =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly RandomRecordGenerator _recordGenerator;
    private readonly ILogger<FileGenerator> _logger;

    public FileGenerator(
        RandomRecordGenerator recordGenerator,
        ILogger<FileGenerator> logger)
    {
        ArgumentNullException.ThrowIfNull(recordGenerator);
        ArgumentNullException.ThrowIfNull(logger);

        _recordGenerator = recordGenerator;
        _logger = logger;
    }

    public async Task GenerateAsync(
        string path,
        long targetSizeBytes,
        int bufferSizeKb = GeneratorDefaults.BufferSizeKb,
        long startingNumber = GeneratorDefaults.StartingNumber,
        CancellationToken cancellationToken = default)
    {
        ValidateArguments(
            path,
            targetSizeBytes,
            bufferSizeKb,
            startingNumber);

        var outputPath = Path.GetFullPath(path);
        EnsureOutputDirectoryExists(outputPath);

        var processingPath = CreateProcessingPath(outputPath);

        _logger.LogInformation(
            "File generation started. Output={OutputPath} TargetBytes={TargetBytes} BufferSizeKb={BufferSizeKb}",
            outputPath,
            targetSizeBytes,
            bufferSizeKb);

        try
        {
            var bytesWritten = await GenerateProcessingFileAsync(
                processingPath,
                targetSizeBytes,
                bufferSizeKb,
                startingNumber,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            File.Move(
                processingPath,
                outputPath,
                overwrite: true);

            _logger.LogInformation(
                "File generation completed. Output={OutputPath} BytesWritten={BytesWritten}",
                outputPath,
                bytesWritten);
        }
        catch (OperationCanceledException)
        {
            TryDeleteProcessingFile(processingPath);

            _logger.LogWarning(
                "File generation cancelled. Output={OutputPath}",
                outputPath);

            throw;
        }
        catch (Exception ex)
        {
            TryDeleteProcessingFile(processingPath);

            _logger.LogError(
                ex,
                "File generation failed. Output={OutputPath}",
                outputPath);

            throw;
        }
    }

    private async Task<long> GenerateProcessingFileAsync(
        string processingPath,
        long targetSizeBytes,
        int bufferSizeKb,
        long startingNumber,
        CancellationToken cancellationToken)
    {
        if (targetSizeBytes == 0)
        {
            await File.WriteAllBytesAsync(
                processingPath,
                [],
                cancellationToken);

            return 0;
        }

        var bufferSizeBytes = checked(
            Math.Max(
                GeneratorDefaults.MinimumBufferBytes,
                bufferSizeKb * 1024));

        await using var stream = new FileStream(
            processingPath,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                BufferSize = bufferSizeBytes
            });

        await using var writer = new StreamWriter(
            stream,
            Utf8WithoutBom,
            bufferSizeBytes,
            leaveOpen: false)
        {
            NewLine = NewLine
        };

        long bytesWritten = 0;
        var number = startingNumber;

        while (bytesWritten < targetSizeBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var text = _recordGenerator.Next();
            var line = $"{number}{GeneratorDefaults.RecordSeparator}{text}";

            await writer.WriteLineAsync(
                line.AsMemory(),
                cancellationToken);

            bytesWritten += GetSerializedSize(line);

            number = checked(number + 1);
        }

        await writer.FlushAsync(cancellationToken);

        return bytesWritten;
    }

    private static long GetSerializedSize(string line)
    {
        return Utf8WithoutBom.GetByteCount(line)
            + Utf8WithoutBom.GetByteCount(NewLine);
    }

    private static void ValidateArguments(
        string path,
        long targetSizeBytes,
        int bufferSizeKb,
        long startingNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        ArgumentOutOfRangeException.ThrowIfNegative(targetSizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(startingNumber);

        if (bufferSizeKb <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bufferSizeKb),
                bufferSizeKb,
                "Buffer size must be greater than zero.");
        }
    }

    private static void EnsureOutputDirectoryExists(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string CreateProcessingPath(string outputPath)
    {
        return $"{outputPath}.{Guid.NewGuid():N}.processing";
    }

    private void TryDeleteProcessingFile(string processingPath)
    {
        if (!File.Exists(processingPath))
        {
            return;
        }

        try
        {
            File.Delete(processingPath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to delete processing file. Path={ProcessingPath}",
                processingPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to delete processing file. Path={ProcessingPath}",
                processingPath);
        }
    }
}