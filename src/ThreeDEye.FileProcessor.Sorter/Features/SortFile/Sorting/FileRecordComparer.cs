using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Parsing;

namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.Sorting;

/// <summary>
/// The single authoritative ordering for <see cref="FileRecord"/>: Text ascending
/// (ordinal), then Number ascending. Both the in-memory chunk sort and the k-way merge
/// use this type, so a run can never be ordered differently from the merge consuming it.
/// </summary>
internal sealed class FileRecordComparer : IComparer<FileRecord>
{
    /// <summary>
    /// Shared stateless instance, avoiding a fresh comparer per chunk sort and per merge.
    /// </summary>
    public static readonly FileRecordComparer Instance = new();

    public int Compare(FileRecord x, FileRecord y)
    {
        var textComparison = string.CompareOrdinal(x.Text, y.Text);
        return textComparison != 0 ? textComparison : x.Number.CompareTo(y.Number);
    }
}
