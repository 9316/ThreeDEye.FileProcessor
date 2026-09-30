using System.Globalization;

namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.Parsing;

internal sealed class FileRecordParser
{
    private const string Separator = FileRecord.Separator;

    public FileRecord Parse(string line, long lineNumber)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (string.IsNullOrWhiteSpace(line))
            throw new InvalidDataException($"Empty line at {lineNumber}.");

        var separatorIndex = line.IndexOf(Separator, StringComparison.Ordinal);

        if (separatorIndex < 1)
            throw new InvalidDataException(
                $"Missing separator '{Separator}' at line {lineNumber}: '{line}'.");

        var numberSpan = line.AsSpan(0, separatorIndex);
        var text = line[(separatorIndex + Separator.Length)..];

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException(
                $"Empty text at line {lineNumber}: '{line}'.");

        if (!long.TryParse(
                numberSpan,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number))
        {
            throw new InvalidDataException(
                $"Invalid number at line {lineNumber}: '{numberSpan.ToString()}'.");
        }

        return new FileRecord(number, text);
    }
}