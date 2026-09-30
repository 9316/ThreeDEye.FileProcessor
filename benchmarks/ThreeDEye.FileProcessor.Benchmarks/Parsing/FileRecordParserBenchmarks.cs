using BenchmarkDotNet.Attributes;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Parsing;

namespace ThreeDEye.FileProcessor.Benchmarks.Parsing;

/// <summary>
/// Parsing runs exactly once per input line, so it is on the critical path for the whole
/// read stage. Line shapes are varied because number width and text length both affect cost.
/// </summary>
[MemoryDiagnoser]
public class FileRecordParserBenchmarks
{
    private readonly FileRecordParser _parser = new();
    private string[] _lines = [];

    [Params(10_000)]
    public int LineCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(20240518);
        var pool = Enumerable.Range(0, 100)
            .Select(i => $"Item {i} - {new string('x', random.Next(4, 40))}")
            .ToArray();

        _lines = Enumerable.Range(1, LineCount)
            .Select(i => $"{random.NextInt64(1, long.MaxValue / 2)}. {pool[i % pool.Length]}")
            .ToArray();
    }

    [Benchmark]
    public long ParseLines()
    {
        long checksum = 0;
        for (var i = 0; i < _lines.Length; i++)
        {
            checksum += _parser.Parse(_lines[i], i + 1).Number;
        }

        return checksum;
    }
}
