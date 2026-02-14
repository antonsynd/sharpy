namespace Sharpy.Compiler.Shared;

internal static class DunderDetector
{
    public static bool IsDunderMethod(string name)
        => name.Length > 5 && name.StartsWith("__") && name.EndsWith("__");
}
