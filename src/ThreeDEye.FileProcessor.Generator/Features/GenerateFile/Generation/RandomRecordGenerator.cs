using ThreeDEye.FileProcessor.Generator.Configuration.Defaults;

namespace ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Generation;

/// <summary>
/// Supplies Text values from a fixed-size pool. The pool is deliberately bounded so the
/// generated file is guaranteed to contain repeated Text values, which the sort must handle.
/// </summary>
internal sealed class RandomRecordGenerator
{
    private readonly Random _rng = new();
    private readonly string[] _pool;

    public RandomRecordGenerator(int poolSize = GeneratorDefaults.StringPoolSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(poolSize);

        _pool = new string[poolSize];
        for (var i = 0; i < poolSize; i++)
        {
            _pool[i] = $"Item {i} - {Guid.NewGuid().ToString("N")[..8]}";
        }
    }

    public string Next() => _pool[_rng.Next(_pool.Length)];
}
