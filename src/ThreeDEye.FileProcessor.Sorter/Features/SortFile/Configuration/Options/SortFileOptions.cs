using System.ComponentModel.DataAnnotations;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Defaults;

namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Options;

/// <summary>
/// Strongly typed sorter configuration bound from the "Sorter" section of appsettings.json.
/// The External Merge Sort algorithm and the Text ASC then Number ASC ordering are fixed
/// assignment rules and are therefore not configurable.
/// </summary>
public sealed class SortFileOptions
{
    public const string SectionName = "Sorter";

    /// <summary>
    /// Path of the unsorted input file.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Sorter:InputPath must be provided.")]
    public string InputPath { get; set; } = SorterDefaults.InputPath;

    /// <summary>
    /// Path of the sorted output file.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Sorter:OutputPath must be provided.")]
    public string OutputPath { get; set; } = SorterDefaults.OutputPath;

    /// <summary>
    /// Base directory for temporary run files. Falls back to the system temp path when empty.
    /// </summary>
    public string? TemporaryDirectory { get; set; }

    /// <summary>
    /// Chunk size in megabytes. Bounds peak memory usage for the in-memory sort stage.
    /// </summary>
    [Range(1, 1024 * 64, ErrorMessage = "Sorter:ChunkSizeMb must be greater than zero.")]
    public int ChunkSizeMb { get; set; } = SorterDefaults.ChunkSizeMb;

    /// <summary>
    /// IO buffer size in kilobytes.
    /// </summary>
    [Range(1, 1024 * 1024, ErrorMessage = "Sorter:BufferSizeKb must be greater than zero.")]
    public int BufferSizeKb { get; set; } = SorterDefaults.BufferSizeKb;

    /// <summary>
    /// Number of concurrent chunk-sorting workers.
    /// </summary>
    [Range(1, 1024, ErrorMessage = "Sorter:MaxDegreeOfParallelism must be greater than zero.")]
    public int MaxDegreeOfParallelism { get; set; } = SorterDefaults.DefaultMaxDegreeOfParallelism;

    /// <summary>
    /// Bounded channel capacity between the reader and the sorting workers. This is the
    /// backpressure control for the producer/consumer pipeline.
    /// </summary>
    [Range(1, 1024, ErrorMessage = "Sorter:ChannelCapacity must be greater than zero.")]
    public int ChannelCapacity { get; set; } = SorterDefaults.ChannelCapacity;

    /// <summary>
    /// Maximum number of runs merged per k-way merge group.
    /// </summary>
    [Range(SorterDefaults.MinimumMergeFanIn, 4096, ErrorMessage = "Sorter:MergeFanIn must be at least 2.")]
    public int MergeFanIn { get; set; } = SorterDefaults.MergeFanIn;

    /// <summary>
    /// Chunk size expressed in bytes, so implementation classes do not repeat the conversion.
    /// </summary>
    public long ChunkSizeBytes => ChunkSizeMb * SorterDefaults.BytesPerMegabyte;

    /// <summary>
    /// Returns an immutable copy so an active sort run is never affected by
    /// configuration reloads.
    /// </summary>
    public SortFileOptions Snapshot() => new()
    {
        InputPath = InputPath,
        OutputPath = OutputPath,
        TemporaryDirectory = TemporaryDirectory,
        ChunkSizeMb = ChunkSizeMb,
        BufferSizeKb = BufferSizeKb,
        MaxDegreeOfParallelism = MaxDegreeOfParallelism,
        ChannelCapacity = ChannelCapacity,
        MergeFanIn = MergeFanIn
    };
}
