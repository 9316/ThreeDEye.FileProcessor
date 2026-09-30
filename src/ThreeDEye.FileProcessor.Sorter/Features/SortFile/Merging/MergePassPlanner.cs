using ThreeDEye.FileProcessor.Sorter.Features.SortFile.Configuration.Defaults;

namespace ThreeDEye.FileProcessor.Sorter.Features.SortFile.Merging;

internal sealed class MergePassPlanner
{
    /// <summary>
    /// Partitions runs into groups of at most <paramref name="mergeFanIn"/>. Groups are
    /// materialized so the caller can enumerate them repeatedly without re-running the split.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> Plan(IEnumerable<string> chunkPaths, int mergeFanIn)
    {
        ArgumentNullException.ThrowIfNull(chunkPaths);
        ArgumentOutOfRangeException.ThrowIfLessThan(mergeFanIn, SorterDefaults.MinimumMergeFanIn);

        var paths = chunkPaths as IReadOnlyList<string> ?? chunkPaths.ToList();
        if (paths.Count == 0)
        {
            return [];
        }

        var groups = new List<IReadOnlyList<string>>((paths.Count + mergeFanIn - 1) / mergeFanIn);

        for (var i = 0; i < paths.Count; i += mergeFanIn)
        {
            var size = Math.Min(mergeFanIn, paths.Count - i);
            var group = new List<string>(size);

            for (var offset = 0; offset < size; offset++)
            {
                group.Add(paths[i + offset]);
            }

            groups.Add(group);
        }

        return groups;
    }
}
