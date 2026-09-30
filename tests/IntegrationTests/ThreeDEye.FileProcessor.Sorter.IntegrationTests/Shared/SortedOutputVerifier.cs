using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Parsing;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Sorting;

namespace ThreeDEye.FileProcessor.Sorter.IntegrationTests.TestSupport;

internal readonly record struct SortedFileStatistics(long RecordCount, IReadOnlyDictionary<string, int> Occurrences);

/// <summary>
/// Streams an output file line-by-line and asserts global ordering using the production
/// comparer. Never materialises the whole file, so it stays valid for large outputs.
/// </summary>
internal static class SortedOutputVerifier
{
    public static SortedFileStatistics VerifySortedAndCount(string path)
    {
        var parser = new FileRecordParser();
        var comparer = new FileRecordComparer();
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);

        using var reader = new StreamReader(path);

        FileRecord? previous = null;
        long count = 0;
        long lineNumber = 0;
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var current = parser.Parse(line, lineNumber);

            if (previous is not null && comparer.Compare(previous.Value, current) > 0)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Output is not globally sorted at line {lineNumber}. " +
                    $"Previous='{previous.Value}' Current='{current}'.");
            }

            occurrences[line] = occurrences.TryGetValue(line, out var existing) ? existing + 1 : 1;
            previous = current;
            count++;
        }

        return new SortedFileStatistics(count, occurrences);
    }

    public static IReadOnlyDictionary<string, int> CountOccurrences(string path)
    {
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        using var reader = new StreamReader(path);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            occurrences[line] = occurrences.TryGetValue(line, out var existing) ? existing + 1 : 1;
        }

        return occurrences;
    }
}
