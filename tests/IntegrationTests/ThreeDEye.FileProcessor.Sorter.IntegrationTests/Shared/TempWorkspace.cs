namespace ThreeDEye.FileProcessor.Sorter.IntegrationTests.TestSupport;

/// <summary>
/// Creates an isolated temporary directory for a single test and removes it on dispose.
/// Safe for parallel test execution because every instance gets a unique root.
/// </summary>
internal sealed class TempWorkspace : IDisposable
{
    public TempWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "3deye-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string GetPath(string fileName) => Path.Combine(Root, fileName);

    public string WriteFile(string fileName, params string[] lines)
    {
        var path = GetPath(fileName);
        File.WriteAllLines(path, lines);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
