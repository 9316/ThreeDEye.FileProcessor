using Microsoft.Extensions.Logging;

namespace ThreeDEye.FileProcessor.Sorter.IntegrationTests.TestSupport;

/// <summary>
/// Captures the structured state emitted by the sort pipeline so tests can assert on
/// externally observable pipeline facts (run counts, merge passes) instead of reaching
/// into private state or guessing from timing.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<IReadOnlyList<KeyValuePair<string, object?>>> _entries = new();
    private readonly object _gate = new();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (state is IReadOnlyList<KeyValuePair<string, object?>> values)
        {
            lock (_gate)
            {
                _entries.Add(values.ToList());
            }
        }
    }

    /// <summary>Returns every value logged for the given structured property name.</summary>
    public IReadOnlyList<object?> ValuesFor(string propertyName)
    {
        lock (_gate)
        {
            return _entries
                .SelectMany(e => e)
                .Where(kvp => string.Equals(kvp.Key, propertyName, StringComparison.Ordinal))
                .Select(kvp => kvp.Value)
                .ToList();
        }
    }

    public int MaxIntFor(string propertyName)
    {
        var values = ValuesFor(propertyName).OfType<int>().ToList();
        return values.Count == 0 ? 0 : values.Max();
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
