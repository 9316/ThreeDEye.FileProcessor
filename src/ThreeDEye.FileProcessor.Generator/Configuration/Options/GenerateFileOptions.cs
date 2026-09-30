using System.ComponentModel.DataAnnotations;
using ThreeDEye.FileProcessor.Generator.Configuration.Defaults;

namespace ThreeDEye.FileProcessor.Generator.Configuration.Options;

/// <summary>
/// Strongly typed generator configuration bound from the "Generator" section of appsettings.json.
/// Record format and field semantics are fixed assignment rules and are therefore not configurable.
/// </summary>
public sealed class GenerateFileOptions
{
    public const string SectionName = "Generator";

    /// <summary>
    /// Destination path of the generated file.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Generator:OutputPath must be provided.")]
    public string OutputPath { get; set; } = GeneratorDefaults.OutputPath;

    /// <summary>
    /// Target size of the generated file, in bytes.
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "Generator:TargetFileSizeBytes must be greater than zero.")]
    public long TargetFileSizeBytes { get; set; } = GeneratorDefaults.TargetFileSizeBytes;

    /// <summary>
    /// IO buffer size in kilobytes.
    /// </summary>
    [Range(1, 1024 * 1024, ErrorMessage = "Generator:BufferSizeKb must be greater than zero.")]
    public int BufferSizeKb { get; set; } = GeneratorDefaults.BufferSizeKb;

    /// <summary>
    /// Number of distinct text values in the pool. A small pool guarantees repeated Text values.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Generator:StringPoolSize must be greater than zero.")]
    public int StringPoolSize { get; set; } = GeneratorDefaults.StringPoolSize;

    /// <summary>
    /// First value of the sequential Number field. Numbering remains sequential because the
    /// record format is a fixed assignment rule; only its starting point is configurable.
    /// </summary>
    [Range(0, long.MaxValue, ErrorMessage = "Generator:StartingNumber must not be negative.")]
    public long StartingNumber { get; set; } = GeneratorDefaults.StartingNumber;

    /// <summary>
    /// Returns an immutable copy so an active generation run is never affected by
    /// configuration reloads.
    /// </summary>
    public GenerateFileOptions Snapshot() => new()
    {
        OutputPath = OutputPath,
        TargetFileSizeBytes = TargetFileSizeBytes,
        BufferSizeKb = BufferSizeKb,
        StringPoolSize = StringPoolSize,
        StartingNumber = StartingNumber
    };
}
