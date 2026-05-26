namespace Sharpy.Cli.Tests;

/// <summary>
/// Creates a temporary directory for test artifacts and deletes it on dispose.
/// </summary>
internal sealed class TempWorkspace : IDisposable
{
    public string Root { get; }

    public TempWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "sharpy_cli_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    /// <summary>Writes a file under the workspace and returns its absolute path.</summary>
    public string WriteFile(string relativeName, string content)
    {
        var path = Path.Combine(Root, relativeName);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Writes a .spy source file and returns its absolute path.</summary>
    public string WriteSpy(string content, string name = "main.spy") => WriteFile(name, content);

    /// <summary>Absolute path under the workspace (file need not exist).</summary>
    public string PathFor(string relativeName) => Path.Combine(Root, relativeName);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; ignore failures (e.g. files still locked).
        }
    }
}
