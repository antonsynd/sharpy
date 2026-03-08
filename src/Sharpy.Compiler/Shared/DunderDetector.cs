using System.Collections.Generic;

namespace Sharpy.Compiler.Shared;

internal static class DunderDetector
{
    private static readonly HashSet<string> DunderProperties = new()
    {
        "__name__",
        "__doc__",
        "__module__",
        "__class__",
        "__dict__",
        "__bases__",
        "__mro__",
        "__qualname__",
        "__annotations__",
        "__slots__",
    };

    public static bool IsDunderMethod(string name)
        => name.Length > 5 && name.StartsWith("__") && name.EndsWith("__");

    public static bool IsDunderProperty(string name)
        => DunderProperties.Contains(name);
}
