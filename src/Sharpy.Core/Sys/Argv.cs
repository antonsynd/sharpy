namespace Sharpy.Sys;

public sealed partial class Exports
{
    private static readonly string[] _argv = Environment.GetCommandLineArgs();
    private static readonly string _platform = GetPlatform();
    private static readonly string[] _path = new[] { Environment.CurrentDirectory };

    /// <summary>
    /// The list of command line arguments passed to the program.
    /// argv[0] is the program name (or empty string).
    /// </summary>
    public static string[] Argv => (string[])_argv.Clone();

    /// <summary>
    /// Exit the program with the given status code.
    /// </summary>
    /// <param name="code">The exit code (default is 0)</param>
    public static void Exit(int code = 0)
    {
        Environment.Exit(code);
    }

    /// <summary>
    /// A string containing the version number of the Python interpreter.
    /// This is a simplified version for Sharpy.
    /// </summary>
    public static string Version => "Sharpy 0.1.0 (Python-like for .NET)";

    /// <summary>
    /// This string contains a platform identifier.
    /// </summary>
    public static string Platform => _platform;

    private static string GetPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return "win32";
        }
        else if (OperatingSystem.IsLinux())
        {
            return "linux";
        }
        else if (OperatingSystem.IsMacOS())
        {
            return "darwin";
        }
        else
        {
            return "unknown";
        }
    }

    /// <summary>
    /// The standard input stream.
    /// </summary>
    public static TextReader Stdin => Console.In;

    /// <summary>
    /// The absolute path of the executable binary for the Python interpreter.
    /// In Sharpy, this returns the path to the current executable.
    /// </summary>
    public static string Executable => Environment.ProcessPath ?? "";

    /// <summary>
    /// A list of strings that specifies the search path for modules.
    /// In Sharpy, this is simplified to just return the current directory.
    /// </summary>
    public static string[] Path => (string[])_path.Clone();
}
