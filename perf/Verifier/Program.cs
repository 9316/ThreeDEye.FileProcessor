// Bounded-memory streaming validator for large generated/sorted files.
//
// Holds only the previous record and a few fixed-size accumulators, so peak memory is
// independent of file size. Validates that every line parses as "<Number>. <Text>",
// optionally that the file is globally ordered by Text (ordinal) then Number ascending,
// and computes an order-independent fingerprint of the record multiset.
//
// The fingerprint sums a per-line FNV-1a hash. Addition is commutative, so running the
// verifier over the unsorted input and the sorted output must yield the same value:
// that proves no record was lost, duplicated, or altered by the sort.

using System.Diagnostics;
using System.Globalization;
using System.Text;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: Verifier <path> [--check-order]");
    return 2;
}

var path = args[0];
var checkOrder = args.Contains("--check-order", StringComparer.OrdinalIgnoreCase);

const int BufferBytes = 1 << 20;

var stopwatch = Stopwatch.StartNew();

await using var stream = new FileStream(
    path,
    FileMode.Open,
    FileAccess.Read,
    FileShare.Read,
    BufferBytes,
    FileOptions.SequentialScan | FileOptions.Asynchronous);

using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, BufferBytes);

long records = 0;
long charTotal = 0;
ulong fingerprint = 0;
ulong numberSum = 0;

string? previousText = null;
long previousNumber = long.MinValue;

var ordered = true;
string? firstIssue = null;

while (await reader.ReadLineAsync() is { } line)
{
    records++;

    var separator = line.IndexOf(". ", StringComparison.Ordinal);
    if (separator < 1)
    {
        firstIssue ??= $"line {records}: missing separator: '{Truncate(line)}'";
        continue;
    }

    var numberSpan = line.AsSpan(0, separator);
    var textSpan = line.AsSpan(separator + 2);

    if (!long.TryParse(numberSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || textSpan.IsEmpty)
    {
        firstIssue ??= $"line {records}: unparsable record: '{Truncate(line)}'";
        continue;
    }

    fingerprint += Fnv1a(line);
    numberSum += (ulong)number;
    charTotal += line.Length;

    if (checkOrder && previousText is not null)
    {
        var comparison = string.CompareOrdinal(previousText, textSpan.ToString());
        if (comparison > 0 || (comparison == 0 && previousNumber > number))
        {
            ordered = false;
            firstIssue ??= $"line {records}: out of order after '{Truncate(previousText)}'/{previousNumber}";
        }
    }

    if (checkOrder)
    {
        previousText = textSpan.ToString();
        previousNumber = number;
    }
}

stopwatch.Stop();

var bytes = new FileInfo(path).Length;

Console.WriteLine($"Path           : {path}");
Console.WriteLine($"Bytes          : {bytes:N0}");
Console.WriteLine($"Records        : {records:N0}");
Console.WriteLine($"Fingerprint    : {fingerprint}");
Console.WriteLine($"NumberSum      : {numberSum}");
Console.WriteLine($"CharTotal      : {charTotal:N0}");
Console.WriteLine($"Ordered        : {(checkOrder ? ordered.ToString() : "not checked")}");
Console.WriteLine($"FirstIssue     : {firstIssue ?? "none"}");
Console.WriteLine($"VerifyElapsed  : {stopwatch.Elapsed.TotalSeconds:N2} s");
Console.WriteLine($"PeakWorkingSet : {Process.GetCurrentProcess().PeakWorkingSet64 / 1024d / 1024d:N2} MB");

return firstIssue is null && (!checkOrder || ordered) ? 0 : 1;

static ulong Fnv1a(ReadOnlySpan<char> value)
{
    var hash = 14695981039346656037UL;
    foreach (var c in value)
    {
        hash ^= c;
        hash *= 1099511628211UL;
    }

    return hash;
}

static string Truncate(string value) => value.Length <= 80 ? value : value[..80] + "...";
