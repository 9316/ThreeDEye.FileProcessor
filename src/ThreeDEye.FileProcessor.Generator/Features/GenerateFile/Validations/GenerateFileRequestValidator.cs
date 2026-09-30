using ThreeDEye.FileProcessor.Generator.Configuration.Options;
using ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Contracts;

namespace ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Validations;

/// <summary>
/// Runtime validation for the handler entry point. This is intentionally separate from the
/// DataAnnotations rules on <see cref="GenerateFileOptions"/>, which only cover the
/// configuration binding boundary and do not run for directly constructed requests.
/// </summary>
internal static class GenerateFileRequestValidator
{
    public static void Validate(GenerateFileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.TargetFileSizeBytes);
    }

    public static void ValidateOptions(GenerateFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.BufferSizeKb);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.StringPoolSize);
        ArgumentOutOfRangeException.ThrowIfNegative(options.StartingNumber);
    }
}
