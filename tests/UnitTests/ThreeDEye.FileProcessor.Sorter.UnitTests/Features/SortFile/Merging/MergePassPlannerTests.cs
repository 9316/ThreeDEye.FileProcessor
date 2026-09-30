using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Merging;

namespace ThreeDEye.FileProcessor.Sorter.UnitTests.Features.SortFile.Merging;

public sealed class MergePassPlannerTests
{
    private readonly MergePassPlanner _planner = new();

    private static IReadOnlyList<string> Runs(int count) =>
        Enumerable.Range(0, count).Select(i => $"run-{i}.run").ToList();

    [Fact]
    public void Plan_ZeroRuns_ReturnsNoGroups()
    {
        // Arrange
        var runs = Runs(0);

        // Act
        var groups = _planner.Plan(runs, mergeFanIn: 4);

        // Assert
        Assert.Empty(groups);
    }

    [Fact]
    public void Plan_SingleRun_ReturnsOneGroupContainingThatRun()
    {
        // Arrange
        var runs = Runs(1);

        // Act
        var groups = _planner.Plan(runs, mergeFanIn: 4).ToList();

        // Assert
        var group = Assert.Single(groups);
        Assert.Equal(new[] { "run-0.run" }, group.ToArray());
    }

    [Fact]
    public void Plan_RunCountBelowFanIn_ReturnsSingleGroup()
    {
        // Arrange
        var runs = Runs(3);

        // Act
        var groups = _planner.Plan(runs, mergeFanIn: 4).ToList();

        // Assert
        var group = Assert.Single(groups);
        Assert.Equal(3, group.Count());
    }

    [Fact]
    public void Plan_RunCountEqualToFanIn_ReturnsSingleGroup()
    {
        // Arrange
        var runs = Runs(4);

        // Act
        var groups = _planner.Plan(runs, mergeFanIn: 4).ToList();

        // Assert
        var group = Assert.Single(groups);
        Assert.Equal(4, group.Count());
    }

    [Fact]
    public void Plan_RunCountAboveFanIn_PartitionsIntoBoundedGroups()
    {
        // Arrange
        var runs = Runs(10);

        // Act
        var groups = _planner.Plan(runs, mergeFanIn: 4).ToList();

        // Assert
        Assert.Equal(3, groups.Count);
        Assert.Equal(new[] { 4, 4, 2 }, groups.Select(g => g.Count()).ToArray());
    }

    [Fact]
    public void Plan_NeverExceedsFanInPerGroup()
    {
        // Arrange
        const int fanIn = 3;
        var runs = Runs(17);

        // Act
        var groups = _planner.Plan(runs, fanIn).ToList();

        // Assert
        Assert.All(groups, g => Assert.True(g.Count() <= fanIn));
    }

    [Fact]
    public void Plan_PreservesAllRunsExactlyOnce()
    {
        // Arrange
        var input = Runs(17);

        // Act
        var flattened = _planner.Plan(input, mergeFanIn: 5).SelectMany(g => g).ToList();

        // Assert
        Assert.Equal(input.Count, flattened.Count);
        Assert.Equal(input, flattened);
    }

    [Fact]
    public void Plan_FanInOfTwo_ProducesPairs()
    {
        // Arrange
        var runs = Runs(5);

        // Act
        var groups = _planner.Plan(runs, mergeFanIn: 2).ToList();

        // Assert
        Assert.Equal(3, groups.Count);
        Assert.Equal(new[] { 2, 2, 1 }, groups.Select(g => g.Count()).ToArray());
    }

    [Fact]
    public void Plan_AppliedRepeatedly_ConvergesToSingleRunRequiringMultiplePasses()
    {
        // Arrange
        var current = Runs(9).ToList();
        var passes = 0;

        // Act
        while (current.Count > 1)
        {
            passes++;
            current = _planner.Plan(current, mergeFanIn: 2)
                .Select(g => g.Count() == 1 ? g.Single() : $"merged-{passes}-{g.First()}")
                .ToList();

            Assert.True(passes < 10, "Planner failed to converge; possible infinite merge loop.");
        }

        // Assert
        // 9 runs with fan-in 2 cannot collapse in a single pass.
        Assert.True(passes > 1, "Expected more than one merge pass for 9 runs at fan-in 2.");
        Assert.Single(current);
    }
}
