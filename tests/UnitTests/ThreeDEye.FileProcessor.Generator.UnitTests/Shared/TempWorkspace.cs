namespace ThreeDEye.FileProcessor.Generator.UnitTests.Shared;

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

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
