using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Parsing;

namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.Sorting;

internal sealed class ChunkSorter
{
    /// <summary>
    /// Sorts in place and returns the same instance; chunks are owned by the caller and
    /// are large, so copying them would defeat the bounded-memory design.
    /// </summary>
    public List<FileRecord> SortChunk(List<FileRecord> chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        chunk.Sort(FileRecordComparer.Instance);
        return chunk;
    }
}
