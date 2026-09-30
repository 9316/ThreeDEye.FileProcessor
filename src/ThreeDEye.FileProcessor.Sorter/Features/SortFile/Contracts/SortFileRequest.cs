using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Options;

namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.Contracts;

public sealed record SortFileRequest(string InputPath, string OutputPath)
{
    /// <summary>
    /// Builds the request from configured options so paths have a single source of truth.
    /// </summary>
    public static SortFileRequest FromOptions(SortFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new SortFileRequest(options.InputPath, options.OutputPath);
    }
}
