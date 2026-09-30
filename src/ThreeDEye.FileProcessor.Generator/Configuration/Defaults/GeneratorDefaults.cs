namespace ThreeDEye.FileProcessor.Generator.Configuration.Defaults;

/// <summary>
/// Single source of truth for generator tuning defaults so implementation classes never
/// embed magic numbers. Values here are only fallbacks; appsettings.json is authoritative.
/// </summary>
internal static class GeneratorDefaults
{
    /// <summary>
    /// Separator between the Number and Text fields of a generated record. The
    /// "&lt;Number&gt;. &lt;Text&gt;" layout is a fixed assignment rule, not a tuning value.
    /// </summary>
    public const string RecordSeparator = ". ";

    public const string OutputPath = "generated.txt";

    public const long TargetFileSizeBytes = 10L * 1024 * 1024;

    public const int BufferSizeKb = 32;

    public const int StringPoolSize = 100;

    public const long StartingNumber = 1;

    /// <summary>
    /// Lower bound enforced on any configured IO buffer, in bytes.
    /// </summary>
    public const int MinimumBufferBytes = 4096;
}
