using ThreeDEye.FileProcessor.Generator.Configuration.Options;
using ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Contracts;
using ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Generation;
using ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Validations;

namespace ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Handlers;

internal sealed class GenerateFileHandler
{
    private readonly FileGenerator _generator;

    public GenerateFileHandler(FileGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
    }

    public Task HandleAsync(GenerateFileRequest request, GenerateFileOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        // Snapshot first so a configuration reload cannot mutate tuning values mid-run,
        // and so validation applies to exactly the values this run will use.
        var settings = options.Snapshot();

        GenerateFileRequestValidator.Validate(request);
        GenerateFileRequestValidator.ValidateOptions(settings);

        return _generator.GenerateAsync(
            request.OutputPath,
            request.TargetFileSizeBytes,
            settings.BufferSizeKb,
            settings.StartingNumber,
            cancellationToken);
    }
}
