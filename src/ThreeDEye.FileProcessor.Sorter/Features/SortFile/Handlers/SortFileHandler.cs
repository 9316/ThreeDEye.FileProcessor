using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Contracts;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Options;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Validation;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Pipeline;

namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.Handlers;

internal sealed class SortFileHandler
{
    private readonly ExternalFileSorter _sorter;

    public SortFileHandler(ExternalFileSorter sorter)
    {
        ArgumentNullException.ThrowIfNull(sorter);
        _sorter = sorter;
    }

    public Task HandleAsync(SortFileRequest request, SortFileOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        // Snapshot first so a configuration reload cannot mutate tuning values mid-run,
        // and so validation applies to exactly the values this run will use.
        var settings = options.Snapshot();

        SortFileRequestValidator.Validate(request);
        SortFileRequestValidator.ValidateOptions(settings);

        return _sorter.SortAsync(request.InputPath, request.OutputPath, settings, cancellationToken);
    }
}
