using System.Runtime.CompilerServices;
using System.Text;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Defaults;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Parsing;

namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.Reading;

internal sealed class ChunkReader
{
    private static readonly Encoding Encoding = Encoding.UTF8;

    // Line terminators are accounted for deterministically ('\n') so chunk sizing
    // does not depend on the platform specific Environment.NewLine.
    private static readonly int LineTerminatorByteCount = Encoding.GetByteCount("\n");

    private readonly FileRecordParser _parser = new();

    public async IAsyncEnumerable<List<FileRecord>> ReadChunksAsync(
        string path,
        long chunkSizeBytes,
        int bufferSizeKb,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSizeBytes);

        var bufferBytes = Math.Max(SorterDefaults.MinimumBufferBytes, bufferSizeKb * SorterDefaults.BytesPerKilobyte);

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferBytes,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        // BOM detection stays enabled: input is produced externally and may legitimately
        // carry a preamble. Run files written by this pipeline never contain one.
        using var reader = new StreamReader(stream, Encoding, detectEncodingFromByteOrderMarks: true, bufferBytes);

        var currentChunk = new List<FileRecord>();
        long currentBytes = 0;
        long lineNumber = 0;

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lineNumber++;

            currentChunk.Add(_parser.Parse(line, lineNumber));
            currentBytes += Encoding.GetByteCount(line) + LineTerminatorByteCount;

            if (currentBytes >= chunkSizeBytes)
            {
                yield return currentChunk;
                currentChunk = [];
                currentBytes = 0;
            }
        }

        if (currentChunk.Count > 0)
        {
            yield return currentChunk;
        }
    }
}
