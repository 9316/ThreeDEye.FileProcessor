using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Options;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Merging;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Parsing;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Reading;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Sorting;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.TemporaryFiles;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Writing;

namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.Pipeline;

/// <summary>
/// High-level coordinator for the external merge sort. Owns the top-level flow only;
/// reading, sorting, writing and merging live in their dedicated collaborators.
/// </summary>
internal sealed class ExternalFileSorter
{
    // Path equality follows the host filesystem: Linux is case-sensitive while Windows and
    // macOS are not, so a single hard-coded comparison would be wrong on some platform.
    private static readonly StringComparison PathComparison =
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

    private readonly ChunkReader _chunkReader;
    private readonly ChunkSorter _chunkSorter;
    private readonly SortedChunkWriter _chunkWriter;
    private readonly ChunkMerger _merger;
    private readonly MergePassPlanner _planner;
    private readonly ILogger<ExternalFileSorter> _logger;

    public ExternalFileSorter(
        ILogger<ExternalFileSorter>? logger = null,
        ChunkReader? chunkReader = null,
        ChunkSorter? chunkSorter = null,
        SortedChunkWriter? chunkWriter = null,
        ChunkMerger? merger = null,
        MergePassPlanner? planner = null)
    {
        _logger = logger ?? NullLogger<ExternalFileSorter>.Instance;
        _chunkReader = chunkReader ?? new ChunkReader();
        _chunkSorter = chunkSorter ?? new ChunkSorter();
        _chunkWriter = chunkWriter ?? new SortedChunkWriter();
        _merger = merger ?? new ChunkMerger();
        _planner = planner ?? new MergePassPlanner();
    }

    public async Task SortAsync(string inputPath, string outputPath, SortFileOptions options, CancellationToken cancellationToken = default)
    {
        ValidateArguments(inputPath, outputPath, options);

        // One immutable snapshot per run: a configuration reload must only affect the next
        // execution, never one already in flight.
        var settings = options.Snapshot();

        var inputSizeBytes = new FileInfo(inputPath).Length;
        _logger.LogInformation(
            "Sort started. Input={InputPath} InputBytes={InputBytes} ChunkSizeMb={ChunkSizeMb} Workers={Workers} ChannelCapacity={ChannelCapacity} MergeFanIn={MergeFanIn}",
            inputPath, inputSizeBytes, settings.ChunkSizeMb, settings.MaxDegreeOfParallelism, settings.ChannelCapacity, settings.MergeFanIn);

        try
        {
            using var workspace = new TemporaryFileManager(settings.TemporaryDirectory, _logger);

            var runPaths = await CreateSortedRunsAsync(inputPath, workspace, settings, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Chunking completed. Runs={RunCount} BytesProcessed={BytesProcessed}", runPaths.Count, inputSizeBytes);

            var mergedRuns = await MergeRunsAsync(runPaths, workspace, settings, cancellationToken).ConfigureAwait(false);

            await FinalizeOutputAsync(mergedRuns, outputPath, settings, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Sort completed. Input={InputPath} Output={OutputPath}", inputPath, outputPath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Sort cancelled. Input={InputPath}", inputPath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sort failed. Input={InputPath}", inputPath);
            throw;
        }
    }

    private static void ValidateArguments(string inputPath, string outputPath, SortFileOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        // Options arrive already validated at the configuration boundary, so a silent
        // default here would mask a composition bug rather than surface it.
        ArgumentNullException.ThrowIfNull(options);

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Input file not found", inputPath);
        }

        if (string.Equals(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath), PathComparison))
        {
            throw new ArgumentException("Input and output paths must differ.");
        }
    }

    /// <summary>
    /// Runs the bounded producer/consumer pipeline: one reader produces chunks while N
    /// workers sort them and spill them as sorted runs.
    /// </summary>
    private async Task<List<string>> CreateSortedRunsAsync(
        string inputPath,
        TemporaryFileManager workspace,
        SortFileOptions settings,
        CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<List<FileRecord>>(new BoundedChannelOptions(settings.ChannelCapacity)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        // Linked source so a failure on either side tears the whole pipeline down instead of
        // leaving its counterpart blocked on a bounded channel forever.
        using var pipelineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var runPaths = new ConcurrentBag<(int Index, string Path)>();
        var nextRunIndex = -1;

        var producer = ProduceChunksAsync(inputPath, channel.Writer, settings, pipelineCts);

        var consumers = Enumerable
            .Range(0, settings.MaxDegreeOfParallelism)
            .Select(_ => ConsumeChunksAsync(channel.Reader, workspace, settings, runPaths, pipelineCts, () => Interlocked.Increment(ref nextRunIndex)))
            .ToArray();

        var pipelineTasks = new List<Task>(consumers.Length + 1) { producer };
        pipelineTasks.AddRange(consumers);

        try
        {
            await Task.WhenAll(pipelineTasks).ConfigureAwait(false);
        }
        catch
        {
            // Nothing may still be running once this method returns, otherwise the caller
            // would dispose the workspace while a worker is still writing into it.
            await pipelineCts.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(pipelineTasks.Select(ObserveAsync)).ConfigureAwait(false);

            ThrowMostMeaningful(pipelineTasks, cancellationToken);
            throw;
        }

        // Deterministic ordering before merge keeps behaviour reproducible regardless of the
        // order in which workers happened to finish.
        return runPaths.OrderBy(run => run.Index).Select(run => run.Path).ToList();
    }

    private async Task ProduceChunksAsync(
        string inputPath,
        ChannelWriter<List<FileRecord>> writer,
        SortFileOptions settings,
        CancellationTokenSource pipelineCts)
    {
        Exception? failure = null;
        try
        {
            await foreach (var chunk in _chunkReader
                .ReadChunksAsync(inputPath, settings.ChunkSizeBytes, settings.BufferSizeKb, pipelineCts.Token)
                .ConfigureAwait(false))
            {
                await writer.WriteAsync(chunk, pipelineCts.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            failure = ex;

            // Cancel first so consumers blocked on ReadAllAsync wake immediately.
            await pipelineCts.CancelAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            // Completing with the fault propagates the real reason to consumers instead of
            // letting them observe a normal completion after a failed read.
            writer.TryComplete(failure);
        }
    }

    private async Task ConsumeChunksAsync(
        ChannelReader<List<FileRecord>> reader,
        TemporaryFileManager workspace,
        SortFileOptions settings,
        ConcurrentBag<(int Index, string Path)> runPaths,
        CancellationTokenSource pipelineCts,
        Func<int> allocateRunIndex)
    {
        try
        {
            await foreach (var chunk in reader.ReadAllAsync(pipelineCts.Token).ConfigureAwait(false))
            {
                // Sorting is CPU-bound and stays synchronous; wrapping it in Task.Run would
                // add a thread hop without making it any more asynchronous.
                var sorted = _chunkSorter.SortChunk(chunk);

                var index = allocateRunIndex();
                var runPath = workspace.AllocateRunPath(index);

                await _chunkWriter.WriteChunkAsync(runPath, sorted, settings.BufferSizeKb, pipelineCts.Token).ConfigureAwait(false);
                runPaths.Add((index, runPath));

                _logger.LogDebug("Run created. RunIndex={RunIndex} Records={Records} RunPath={RunPath}", index, sorted.Count, runPath);
            }
        }
        catch
        {
            // A failed consumer must not let the producer keep filling a bounded channel
            // that nobody will drain, which would block the producer forever.
            await pipelineCts.CancelAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task<List<string>> MergeRunsAsync(
        List<string> runPaths,
        TemporaryFileManager workspace,
        SortFileOptions settings,
        CancellationToken cancellationToken)
    {
        var currentRuns = runPaths;
        var pass = 0;

        while (currentRuns.Count > 1)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pass++;

            var groups = _planner.Plan(currentRuns, settings.MergeFanIn);
            _logger.LogInformation("Merge pass {Pass} started. InputRuns={InputRuns} Groups={Groups}", pass, currentRuns.Count, groups.Count);

            var nextRuns = new List<string>(groups.Count);
            var groupIndex = 0;

            foreach (var inputs in groups)
            {
                if (inputs.Count == 1)
                {
                    // Nothing to merge; carry the run forward rather than rewriting it.
                    nextRuns.Add(inputs[0]);
                    continue;
                }

                var mergedPath = workspace.AllocateIntermediatePath(pass, groupIndex++);
                await _merger.MergeAsync(inputs, mergedPath, settings.BufferSizeKb, cancellationToken).ConfigureAwait(false);
                nextRuns.Add(mergedPath);
            }

            DeleteConsumedRuns(currentRuns, nextRuns);

            currentRuns = nextRuns;
            _logger.LogInformation("Merge pass {Pass} completed. RemainingRuns={RemainingRuns}", pass, currentRuns.Count);
        }

        return currentRuns;
    }

    /// <summary>
    /// Publishes the result atomically: the final merge writes to a run-unique processing
    /// file and only replaces the real output once it has completed, so a partial file is
    /// never visible and an existing valid output survives a failure.
    /// </summary>
    private async Task FinalizeOutputAsync(
        List<string> mergedRuns,
        string outputPath,
        SortFileOptions settings,
        CancellationToken cancellationToken)
    {
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // Unique per execution so two concurrent sorts targeting the same output cannot
        // corrupt each other's processing file.
        var processingPath = $"{outputPath}.{Guid.NewGuid():N}.processing";

        try
        {
            // An empty run set still flows through the merger, which writes a valid empty
            // file, so empty input shares this single publication path.
            await _merger.MergeAsync(mergedRuns, processingPath, settings.BufferSizeKb, cancellationToken).ConfigureAwait(false);

            File.Move(processingPath, outputPath, overwrite: true);

            _logger.LogInformation("Finalization completed. Output={OutputPath} OutputBytes={OutputBytes}", outputPath, new FileInfo(outputPath).Length);
        }
        catch
        {
            DeleteProcessingFile(processingPath);
            throw;
        }
    }

    private void DeleteConsumedRuns(List<string> previousRuns, List<string> nextRuns)
    {
        foreach (var path in previousRuns)
        {
            if (nextRuns.Contains(path))
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Intermediates live inside the workspace, which is removed on dispose, so a
                // failure here costs temporary disk space but never correctness.
                _logger.LogWarning(ex, "Failed to delete intermediate run {RunPath}.", path);
            }
        }
    }

    private void DeleteProcessingFile(string processingPath)
    {
        try
        {
            if (File.Exists(processingPath))
            {
                File.Delete(processingPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Cleanup trouble must never replace the original failure being propagated.
            _logger.LogWarning(ex, "Failed to remove partial output {ProcessingPath}.", processingPath);
        }
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // Observed deliberately so no task faults go unhandled; the representative
            // exception is selected separately by ThrowMostMeaningful.
        }
    }

    /// <summary>
    /// Prefers a genuine pipeline failure over the cancellation that the failure itself
    /// triggered, so callers see the root cause rather than its side effect.
    /// </summary>
    private static void ThrowMostMeaningful(List<Task> tasks, CancellationToken cancellationToken)
    {
        var faults = tasks
            .Where(task => task.IsFaulted && task.Exception is not null)
            .SelectMany(task => task.Exception!.InnerExceptions)
            .ToList();

        var meaningful = faults.Find(error => error is not OperationCanceledException);
        if (meaningful is not null)
        {
            ExceptionDispatchInfo.Capture(meaningful).Throw();
        }

        // Only the caller's own cancellation remains, so surface it as cancellation.
        cancellationToken.ThrowIfCancellationRequested();
    }
}
