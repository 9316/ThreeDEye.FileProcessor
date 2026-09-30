using System.Text;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Defaults;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Parsing;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Sorting;

namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.Merging;

internal sealed class ChunkMerger
{
    // Encoding.UTF8 emits a byte-order mark through StreamWriter. Merged output (including
    // the empty-input case) must contain only the records themselves.
    private static readonly UTF8Encoding NoBomEncoding = new(encoderShouldEmitUTF8Identifier: false);

    // Records are terminated with '\n' rather than Environment.NewLine so merged output is
    // byte-identical across platforms and round-trips through ChunkReader unchanged.
    private const string LineTerminator = "\n";

    private readonly FileRecordParser _parser = new();

    private sealed class RunState
    {
        public required StreamReader Reader { get; init; }
        public required string Path { get; init; }
        public FileRecord? Current { get; set; }
        public long LineNumber { get; set; }
    }

    public async Task MergeAsync(IEnumerable<string> sortedChunkPaths, string outputPath, int bufferSizeKb = SorterDefaults.BufferSizeKb, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sortedChunkPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var paths = sortedChunkPaths.ToList();
        var bufferBytes = Math.Max(SorterDefaults.MinimumBufferBytes, bufferSizeKb * SorterDefaults.BytesPerKilobyte);

        if (paths.Count == 0)
        {
            // No runs still needs a valid (empty) output file so the caller can publish it.
            await using var empty = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            return;
        }

        var runs = new List<RunState>(paths.Count);
        try
        {
            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferBytes,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                var run = new RunState { Reader = new StreamReader(stream, NoBomEncoding, detectEncodingFromByteOrderMarks: false, bufferBytes), Path = path };
                runs.Add(run);

                await AdvanceAsync(run).ConfigureAwait(false);
            }

            await using var outputStream = new FileStream(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferBytes,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await using var writer = new StreamWriter(outputStream, NoBomEncoding, bufferBytes)
            {
                NewLine = LineTerminator
            };

            // Ordering comes from the same comparer used to produce the runs, so the merge
            // can never disagree with the sort that created its inputs.
            var queue = new PriorityQueue<RunState, FileRecord>(runs.Count, FileRecordComparer.Instance);
            foreach (var run in runs)
            {
                if (run.Current is { } record)
                {
                    queue.Enqueue(run, record);
                }
            }

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var run = queue.Dequeue();
                await writer.WriteLineAsync(run.Current!.Value.ToString()).ConfigureAwait(false);

                await AdvanceAsync(run).ConfigureAwait(false);
                if (run.Current is { } next)
                {
                    queue.Enqueue(run, next);
                }
            }

            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            foreach (var run in runs)
            {
                run.Reader.Dispose();
            }
        }
    }

    private async Task AdvanceAsync(RunState run)
    {
        var line = await run.Reader.ReadLineAsync().ConfigureAwait(false);
        if (line is null)
        {
            run.Current = null;
            return;
        }

        run.LineNumber++;
        run.Current = _parser.Parse(line, run.LineNumber);
    }
}
