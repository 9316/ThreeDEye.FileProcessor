namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Defaults;

/// <summary>
/// Single source of truth for sorter tuning defaults so implementation classes never
/// embed magic numbers. Values here are only fallbacks; appsettings.json is authoritative.
/// </summary>
internal static class SorterDefaults
{
    public const string InputPath = "input.txt";

    public const string OutputPath = "output.txt";

    public const int ChunkSizeMb = 10;

    public const int BufferSizeKb = 32;

    public const int ChannelCapacity = 4;

    public const int MergeFanIn = 16;

    /// <summary>
    /// A k-way merge requires at least two inputs to make progress.
    /// </summary>
    public const int MinimumMergeFanIn = 2;

    /// <summary>
    /// Lower bound enforced on any configured IO buffer, in bytes.
    /// </summary>
    public const int MinimumBufferBytes = 4096;

    public const long BytesPerMegabyte = 1024L * 1024L;

    public const int BytesPerKilobyte = 1024;

    public static int DefaultMaxDegreeOfParallelism => Math.Max(1, Environment.ProcessorCount / 2);
}
