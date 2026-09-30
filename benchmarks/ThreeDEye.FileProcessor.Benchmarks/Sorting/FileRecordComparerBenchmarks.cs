using BenchmarkDotNet.Attributes;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Parsing;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Sorting;

namespace ThreeDEye.FileProcessor.Benchmarks.Sorting;

/// <summary>
/// The comparer is invoked O(n log n) times per chunk sort, making it the dominant cost of
/// the in-memory stage. Duplicate text values are included deliberately because they force
/// the secondary Number comparison, which is the realistic worst case for this data set.
/// </summary>
[MemoryDiagnoser]
public class FileRecordComparerBenchmarks
{
    private readonly FileRecordComparer _comparer = new();
    private FileRecord[] _source = [];
    private List<FileRecord> _buffer = [];

    [Params(100_000)]
    public int RecordCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(20240518);
        // A small text pool guarantees frequent ties, exercising the Number tie-break.
        var pool = Enumerable.Range(0, 200).Select(i => $"Item {i}").ToArray();

        _source = Enumerable.Range(0, RecordCount)
            .Select(_ => new FileRecord(random.NextInt64(1, 1_000_000), pool[random.Next(pool.Length)]))
            .ToArray();

        _buffer = new List<FileRecord>(RecordCount);
    }

    [IterationSetup]
    public void ResetBuffer()
    {
        _buffer.Clear();
        _buffer.AddRange(_source);
    }

    /// <summary>
    /// Measures a full chunk sort, which is how the comparer is actually used in production.
    /// </summary>
    [Benchmark]
    public void SortChunk() => _buffer.Sort(_comparer);
}
