using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.TemporaryFiles;

/// <summary>
/// Owns the temporary workspace for a single sort execution: creates it, allocates all run
/// and intermediate paths inside it, and removes it on disposal. Lifetime is per-operation,
/// so this type is never registered in the container.
/// </summary>
internal sealed class TemporaryFileManager : IDisposable
{
    private readonly ILogger _logger;
    private bool _disposed;

    public TemporaryFileManager(string? baseTemporaryDirectory = null, ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;

        var baseDir = string.IsNullOrWhiteSpace(baseTemporaryDirectory) ? Path.GetTempPath() : baseTemporaryDirectory;
        Workspace = Path.Combine(baseDir, "sort-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Workspace);
    }

    public string Workspace { get; }

    public string AllocateRunPath(int index)
        => Path.Combine(Workspace, $"run-{index:000000}.run");

    public string AllocateIntermediatePath(int pass, int index)
        => Path.Combine(Workspace, $"pass-{pass:00}-run-{index:000000}.run");

    /// <summary>
    /// Disposal is the single cleanup entry point, so callers never need a redundant
    /// explicit cleanup call in a finally block.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (Directory.Exists(Workspace))
            {
                Directory.Delete(Workspace, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort: a locked or already-removed temp directory must never mask the
            // outcome of the sort itself, but it is surfaced so leaks remain diagnosable.
            _logger.LogWarning(ex, "Failed to remove temporary workspace {Workspace}.", Workspace);
        }
    }
}
