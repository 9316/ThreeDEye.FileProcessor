using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Parsing;

namespace ThreeDEye.FileProcessor.Sorter.UnitTests.Features.SortFile.Parsing;

public sealed class FileRecordParserTests
{
    private readonly FileRecordParser _parser = new();

    [Fact]
    public void Parse_ValidLine_ReturnsNumberAndText()
    {
        // Arrange
        const string line = "415. Apple";

        // Act
        var record = _parser.Parse(line, 1);

        // Assert
        Assert.Equal(415, record.Number);
        Assert.Equal("Apple", record.Text);
    }

    [Fact]
    public void Parse_TextWithSpaces_KeepsFullText()
    {
        // Arrange
        const string line = "30432. Something something something";

        // Act
        var record = _parser.Parse(line, 1);

        // Assert
        Assert.Equal(30432, record.Number);
        Assert.Equal("Something something something", record.Text);
    }

    [Fact]
    public void Parse_TextContainingPeriod_SplitsOnFirstSeparatorOnly()
    {
        // Arrange
        const string line = "7. Apple. Banana";

        // Act
        var record = _parser.Parse(line, 1);

        // Assert
        Assert.Equal(7, record.Number);
        Assert.Equal("Apple. Banana", record.Text);
    }

    [Fact]
    public void Parse_TextContainingMultiplePeriods_KeepsAllPeriodsInText()
    {
        // Arrange
        const string line = "7. a. b. c. d";

        // Act
        var record = _parser.Parse(line, 1);

        // Assert
        Assert.Equal(7, record.Number);
        Assert.Equal("a. b. c. d", record.Text);
    }

    [Fact]
    public void Parse_MaxLongNumber_ParsesExactly()
    {
        // Arrange
        var line = $"{long.MaxValue}. Apple";

        // Act
        var record = _parser.Parse(line, 1);

        // Assert
        Assert.Equal(long.MaxValue, record.Number);
    }

    [Fact]
    public void Parse_ZeroNumber_IsAccepted()
    {
        // Arrange
        const string line = "0. Apple";

        // Act
        var record = _parser.Parse(line, 1);

        // Assert
        Assert.Equal(0, record.Number);
    }

    [Fact]
    public void Parse_NegativeNumber_IsRejectedBecauseContractAllowsDigitsOnly()
    {
        // Arrange
        const string line = "-1. Apple";

        // Act
        // NumberStyles.None forbids a leading sign, so '-1' is not a valid number part.
        var ex = Assert.Throws<InvalidDataException>(() => _parser.Parse(line, 9));

        // Assert
        Assert.Contains("Invalid number", ex.Message, StringComparison.Ordinal);
        Assert.Contains("line 9", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("415 Apple")]
    [InlineData("415.Apple")]
    [InlineData("Apple")]
    public void Parse_MissingSeparator_ThrowsWithLineContext(string line)
    {
        // Arrange
        // The malformed line arrives through InlineData.

        // Act
        var ex = Assert.Throws<InvalidDataException>(() => _parser.Parse(line, 42));

        // Assert
        Assert.Contains("line 42", ex.Message, StringComparison.Ordinal);
        Assert.Contains(line, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_LineStartingWithSeparator_ThrowsBecauseNumberPartIsEmpty()
    {
        // Arrange
        const string line = ". Apple";

        // Act
        var ex = Assert.Throws<InvalidDataException>(() => _parser.Parse(line, 3));

        // Assert
        Assert.Contains("line 3", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("abc. Apple")]
    [InlineData("12a. Apple")]
    [InlineData("1 2. Apple")]
    [InlineData("99999999999999999999. Apple")]
    public void Parse_InvalidNumericPart_ThrowsWithNumberContext(string line)
    {
        // Arrange
        // The malformed line arrives through InlineData.

        // Act
        var ex = Assert.Throws<InvalidDataException>(() => _parser.Parse(line, 5));

        // Assert
        Assert.Contains("Invalid number", ex.Message, StringComparison.Ordinal);
        Assert.Contains("line 5", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Parse_EmptyOrWhitespaceLine_ThrowsWithLineNumber(string line)
    {
        // Arrange
        // The blank line arrives through InlineData.

        // Act
        var ex = Assert.Throws<InvalidDataException>(() => _parser.Parse(line, 11));

        // Assert
        Assert.Contains("11", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_EmptyText_ThrowsWithLineContext()
    {
        // Arrange
        const string line = "415. ";

        // Act
        var ex = Assert.Throws<InvalidDataException>(() => _parser.Parse(line, 6));

        // Assert
        Assert.Contains("6", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_TextWithTrailingWhitespace_PreservesWhitespace()
    {
        // Arrange
        const string line = "415. Apple  ";

        // Act
        var record = _parser.Parse(line, 1);

        // Assert
        Assert.Equal("Apple  ", record.Text);
    }

    [Fact]
    public void Parse_NullLine_ThrowsArgumentNullException()
    {
        // Arrange
        // No arrangement required: the null line is supplied inline.

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _parser.Parse(null!, 1));
    }

    [Fact]
    public void Parse_RoundTripsThroughToString()
    {
        // Arrange
        const string line = "30432. Something something something";

        // Act
        var record = _parser.Parse(line, 1);

        // Assert
        Assert.Equal(line, record.ToString());
    }
}
