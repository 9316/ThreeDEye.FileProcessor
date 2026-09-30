using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Defaults;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Options;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Contracts;

namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.Validation;

/// <summary>
/// Runtime validation for the handler entry point. This is intentionally separate from the
/// DataAnnotations rules on <see cref="SortFileOptions"/>, which only cover the
/// configuration binding boundary and do not run for directly constructed requests.
/// </summary>
internal static class SortFileRequestValidator
{
    public static void Validate(SortFileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath);
    }

    public static void ValidateOptions(SortFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.ChunkSizeMb);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.BufferSizeKb);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxDegreeOfParallelism);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.ChannelCapacity);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MergeFanIn, SorterDefaults.MinimumMergeFanIn);
    }
}
