namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.Parsing;

/// <summary>
/// A single "&lt;Number&gt;. &lt;Text&gt;" record and the authoritative definition of that format.
/// Parsing and writing both derive from <see cref="Separator"/> so the two can never drift.
/// </summary>
internal readonly record struct FileRecord(long Number, string Text)
{
    public const string Separator = ". ";

    public override string ToString() => $"{Number}{Separator}{Text}";
}
