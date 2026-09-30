using ThreeDEye.FileProcessor.Generator.Configuration.Options;

namespace ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Contracts;

public sealed record GenerateFileRequest(string OutputPath, long TargetFileSizeBytes)
{
    /// <summary>
    /// Builds the request from configured options so paths and sizes have a single source of truth.
    /// </summary>
    public static GenerateFileRequest FromOptions(GenerateFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new GenerateFileRequest(options.OutputPath, options.TargetFileSizeBytes);
    }
}
