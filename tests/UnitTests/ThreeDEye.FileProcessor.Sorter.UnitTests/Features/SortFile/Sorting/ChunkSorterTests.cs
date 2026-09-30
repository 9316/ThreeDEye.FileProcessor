using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Parsing;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Sorting;

namespace ThreeDEye.FileProcessor.Sorter.UnitTests.Features.SortFile.Sorting;

public sealed class ChunkSorterTests
{
    private readonly ChunkSorter _sorter = new();
    private readonly FileRecordComparer _comparer = new();

    [Fact]
    public void SortChunk_EmptyChunk_ReturnsEmpty()
    {
        // Arrange
        var chunk = new List<FileRecord>();

        // Act
        var result = _sorter.SortChunk(chunk);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void SortChunk_SingleRecord_ReturnsThatRecord()
    {
        // Arrange
        var chunk = new List<FileRecord> { new(415, "Apple") };

        // Act
        var result = _sorter.SortChunk(chunk);

        // Assert
        Assert.Equal(new FileRecord(415, "Apple"), Assert.Single(result));
    }

    [Fact]
    public void SortChunk_UnsortedRecords_ReturnsComparerOrder()
    {
        // Arrange
        var chunk = new List<FileRecord>
        {
            new(30432, "Something"),
            new(415, "Apple"),
            new(2, "Banana"),
            new(1, "Apple")
        };

        // Act
        var result = _sorter.SortChunk(chunk);

        // Assert
        Assert.Equal(
            new[] { "1. Apple", "415. Apple", "2. Banana", "30432. Something" },
            result.Select(r => r.ToString()).ToArray());
    }

    [Fact]
    public void SortChunk_AlreadySorted_RemainsSorted()
    {
        // Arrange
        var chunk = new List<FileRecord>
        {
            new(1, "Apple"),
            new(415, "Apple"),
            new(2, "Banana")
        };

        // Act
        var result = _sorter.SortChunk(chunk);

        // Assert
        AssertSortedByComparer(result);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void SortChunk_DuplicateTextValues_GroupsThemAndOrdersByNumber()
    {
        // Arrange
        var chunk = new List<FileRecord>
        {
            new(300, "Apple"),
            new(100, "Apple"),
            new(200, "Apple")
        };

        // Act
        var result = _sorter.SortChunk(chunk);

        // Assert
        Assert.Equal(new long[] { 100, 200, 300 }, result.Select(r => r.Number).ToArray());
    }

    [Fact]
    public void SortChunk_DuplicateCompleteRecords_PreservesAllOfThem()
    {
        // Arrange
        var chunk = new List<FileRecord>
        {
            new(1, "Apple"),
            new(1, "Apple"),
            new(1, "Apple")
        };

        // Act
        var result = _sorter.SortChunk(chunk);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.Equal(new FileRecord(1, "Apple"), r));
    }

    [Fact]
    public void SortChunk_PreservesRecordCountAndMultiset()
    {
        // Arrange
        var chunk = new List<FileRecord>
        {
            new(5, "b"),
            new(3, "a"),
            new(5, "b"),
            new(1, "c"),
            new(3, "a")
        };
        var expected = chunk.Select(r => r.ToString()).OrderBy(s => s, System.StringComparer.Ordinal).ToArray();

        // Act
        var result = _sorter.SortChunk(chunk);

        // Assert
        Assert.Equal(5, result.Count);
        Assert.Equal(expected, result.Select(r => r.ToString()).OrderBy(s => s, System.StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void SortChunk_MatchesFileRecordComparerSemanticsExactly()
    {
        // Arrange
        var chunk = new List<FileRecord>
        {
            new(2, "apple"),
            new(1, "Apple"),
            new(10, "Apple"),
            new(9, "Apple"),
            new(1, "Banana")
        };
        var expected = chunk.OrderBy(r => r, _comparer).Select(r => r.ToString()).ToArray();

        // Act
        var result = _sorter.SortChunk(chunk);

        // Assert
        Assert.Equal(expected, result.Select(r => r.ToString()).ToArray());
        AssertSortedByComparer(result);
    }

    private void AssertSortedByComparer(IReadOnlyList<FileRecord> records)
    {
        for (var i = 1; i < records.Count; i++)
        {
            Assert.True(
                _comparer.Compare(records[i - 1], records[i]) <= 0,
                $"Records out of order at index {i}: '{records[i - 1]}' then '{records[i]}'.");
        }
    }
}
