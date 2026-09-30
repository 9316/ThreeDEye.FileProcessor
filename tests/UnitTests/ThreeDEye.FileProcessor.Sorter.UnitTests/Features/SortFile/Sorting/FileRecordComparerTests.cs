using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Parsing;
using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Sorting;

namespace ThreeDEye.FileProcessor.Sorter.UnitTests.Features.SortFile.Sorting;

public sealed class FileRecordComparerTests
{
    private readonly FileRecordComparer _comparer = new();

    [Fact]
    public void Compare_DifferentText_OrdersByTextAscending()
    {
        // Arrange
        var apple = new FileRecord(999, "Apple");
        var banana = new FileRecord(1, "Banana");

        // Act & Assert
        Assert.True(_comparer.Compare(apple, banana) < 0);
        Assert.True(_comparer.Compare(banana, apple) > 0);
    }

    [Fact]
    public void Compare_SameText_OrdersByNumberAscending()
    {
        // Arrange
        var first = new FileRecord(1, "Apple");
        var second = new FileRecord(415, "Apple");

        // Act & Assert
        Assert.True(_comparer.Compare(first, second) < 0);
        Assert.True(_comparer.Compare(second, first) > 0);
    }

    [Fact]
    public void Compare_EqualRecords_ReturnsZero()
    {
        // Arrange
        var left = new FileRecord(415, "Apple");
        var right = new FileRecord(415, "Apple");

        // Act & Assert
        Assert.Equal(0, _comparer.Compare(left, right));
    }

    [Fact]
    public void Compare_IsCaseSensitiveWithUppercaseBeforeLowercaseUnderOrdinal()
    {
        // Arrange
        var upper = new FileRecord(1, "Apple");
        var lower = new FileRecord(1, "apple");

        // Act & Assert
        // Ordinal compares UTF-16 code units: 'A' (65) < 'a' (97).
        Assert.True(_comparer.Compare(upper, lower) < 0);
    }

    [Fact]
    public void Compare_UsesOrdinalNotCultureAwareOrdering()
    {
        // Arrange
        // Under many cultures "coop" sorts after "co-op"; ordinal puts '-' (45) before 'o' (111).
        var hyphen = new FileRecord(1, "co-op");
        var plain = new FileRecord(1, "coop");

        // Act & Assert
        Assert.True(_comparer.Compare(hyphen, plain) < 0);
    }

    [Fact]
    public void Compare_ShorterTextThatIsAPrefix_SortsFirst()
    {
        // Arrange
        var shorter = new FileRecord(1, "Apple");
        var longer = new FileRecord(1, "Apple pie");

        // Act & Assert
        Assert.True(_comparer.Compare(shorter, longer) < 0);
    }

    [Fact]
    public void Compare_LargeNumbersWithSameText_CompareNumericallyNotLexicographically()
    {
        // Arrange
        var small = new FileRecord(9, "Apple");
        var large = new FileRecord(10, "Apple");

        // Act & Assert
        // Lexicographic ordering would place "10" before "9"; numeric ordering must not.
        Assert.True(_comparer.Compare(small, large) < 0);
    }

    [Fact]
    public void Sort_MixedSample_ProducesTextThenNumberOrdering()
    {
        // Arrange
        var records = new List<FileRecord>
        {
            new(415, "Apple"),
            new(30432, "Something something something"),
            new(1, "Apple"),
            new(32, "Cherry is the best"),
            new(2, "Banana is yellow")
        };

        // Act
        records.Sort(_comparer);

        // Assert
        Assert.Equal(
            new[]
            {
                "1. Apple",
                "415. Apple",
                "2. Banana is yellow",
                "32. Cherry is the best",
                "30432. Something something something"
            },
            records.Select(r => r.ToString()).ToArray());
    }
}
