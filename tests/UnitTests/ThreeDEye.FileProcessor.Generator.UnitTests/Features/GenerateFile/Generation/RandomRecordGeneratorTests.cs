using ThreeDEye.FileProcessor.Generator.Features.GenerateFile.Generation;

namespace ThreeDEye.FileProcessor.Generator.UnitTests.Features.GenerateFile.Generation;

public sealed class RandomRecordGeneratorTests
{
    [Fact]
    public void Next_ReturnsNonEmptyText()
    {
        // Arrange
        var generator = new RandomRecordGenerator(poolSize: 10);

        // Act & Assert
        for (var i = 0; i < 50; i++)
        {
            Assert.False(string.IsNullOrWhiteSpace(generator.Next()));
        }
    }

    [Fact]
    public void Next_AlwaysReturnsValueFromTheConfiguredPool()
    {
        // Arrange
        const int poolSize = 8;
        var generator = new RandomRecordGenerator(poolSize);

        // Act
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 500; i++)
        {
            distinct.Add(generator.Next());
        }

        // Assert
        // The pool bounds the set of possible values, so no more than poolSize distinct results.
        Assert.True(
            distinct.Count <= poolSize,
            $"Expected at most {poolSize} distinct values but saw {distinct.Count}.");
    }

    [Fact]
    public void Next_ProducesDuplicatesByDesignWhenCallsExceedPoolSize()
    {
        // Arrange
        const int poolSize = 4;
        var generator = new RandomRecordGenerator(poolSize);

        // Act
        // Pigeonhole principle: poolSize + 1 draws from a pool of poolSize must repeat.
        var values = Enumerable.Range(0, poolSize + 1).Select(_ => generator.Next()).ToList();

        // Assert
        Assert.True(values.Distinct(StringComparer.Ordinal).Count() < values.Count);
    }

    [Fact]
    public void Next_TextNeverContainsNewlineSoRecordsStayOnOneLine()
    {
        // Arrange
        var generator = new RandomRecordGenerator(poolSize: 20);

        // Act & Assert
        for (var i = 0; i < 100; i++)
        {
            var text = generator.Next();
            Assert.DoesNotContain('\n', text);
            Assert.DoesNotContain('\r', text);
        }
    }

    [Fact]
    public void Next_WithSingleEntryPool_AlwaysReturnsSameValue()
    {
        // Arrange
        var generator = new RandomRecordGenerator(poolSize: 1);

        // Act
        var first = generator.Next();

        // Assert
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(first, generator.Next());
        }
    }
}
