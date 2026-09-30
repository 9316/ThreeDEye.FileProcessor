using System.Text;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Defaults;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Parsing;

namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.Writing;

/// <summary>
/// Writes a sorted chunk to a run path supplied by the caller. Run-path naming is owned by
/// <c>TemporaryFileManager</c>; this type only writes to the location it is given.
/// </summary>
internal sealed class SortedChunkWriter
{
    // Encoding.UTF8 emits a byte-order mark through StreamWriter, which would corrupt the
    // first record of every run. Sorted runs must be plain UTF-8 with no preamble.
    private static readonly UTF8Encoding NoBomEncoding = new(encoderShouldEmitUTF8Identifier: false);

    // Records are terminated with '\n' rather than Environment.NewLine so run files are
    // byte-identical across platforms and match the byte accounting ChunkReader uses to
    // size chunks.
    private const string LineTerminator = "\n";

    public async Task WriteChunkAsync(
        string runPath,
        List<FileRecord> records,
        int bufferSizeKb = SorterDefaults.BufferSizeKb,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runPath);
        ArgumentNullException.ThrowIfNull(records);

        var bufferBytes = Math.Max(SorterDefaults.MinimumBufferBytes, bufferSizeKb * SorterDefaults.BytesPerKilobyte);

        await using var stream = new FileStream(
            runPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferBytes,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await using var writer = new StreamWriter(stream, NoBomEncoding, bufferBytes)
        {
            NewLine = LineTerminator
        };

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(record.ToString()).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
