using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// The C# identifiers a source file's position in a project emits (#1932): the directory WRAPPER
/// classes the module class nests in, and the module class itself. One authority, read by the
/// emitter (<c>RoslynEmitter.ComputeWrapperClasses</c> / <c>GetModuleClassName</c>) and by the
/// project's pre-emission check (<c>ProjectCompiler.RefusePackageModuleNameCollisions</c>, SPY0526),
/// so the refusal and the emission cannot disagree on a spelling.
/// </summary>
internal static class ModuleIdentifiers
{
    /// <summary>
    /// The wrapper class names for <paramref name="filePath"/>, relative to
    /// <paramref name="sourceRoot"/> (the project's common source directory — NOT the project
    /// directory: sources under <c>src/</c> put the root at <c>src/</c>). Every directory segment
    /// mangled through <see cref="NameMangler.ToNamespacePart"/>; for <c>__init__.spy</c> the last
    /// directory is the module class itself, not a wrapper (<c>pkg/sub/__init__.spy</c> → [Pkg]).
    /// Empty for a root-level file or when either path is unknown (single-file compilation).
    /// </summary>
    public static List<string> WrapperSegments(string? sourceRoot, string? filePath)
        => WrapperDirectories(sourceRoot, filePath).Select(NameMangler.ToNamespacePart).ToList();

    /// <summary>The source spellings of the directories <see cref="WrapperSegments"/> mangles, in order.</summary>
    private static List<string> WrapperDirectories(string? sourceRoot, string? filePath)
    {
        if (string.IsNullOrEmpty(sourceRoot) || string.IsNullOrEmpty(filePath))
            return new List<string>();

        var relativePath = Path.GetRelativePath(sourceRoot, filePath);
        var relativeDir = Path.GetDirectoryName(relativePath) ?? "";
        if (string.IsNullOrEmpty(relativeDir) || relativeDir == ".")
            return new List<string>();

        var directories = relativeDir
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (Path.GetFileNameWithoutExtension(filePath) == DunderNames.Init && directories.Count > 0)
            directories.RemoveAt(directories.Count - 1);

        return directories;
    }

    /// <summary>
    /// A class nested in a class of the same emitted name along <paramref name="filePath"/>'s
    /// wrapper chain (#1932): two ADJACENT directories that mangle alike (<c>a/a/x.spy</c> → wrapper
    /// <c>A</c> in wrapper <c>A</c>), or the innermost directory and the module class
    /// (<c>lib/lib.spy</c>). Either is CS0542. Non-adjacent repeats (<c>a/b/a/x.spy</c>) are legal C#
    /// — a nested type may share a name with a non-enclosing ancestor. Null when there is none.
    /// </summary>
    public static NestedNameCollision? FindNestedNameCollision(
        string? sourceRoot, string filePath, bool willGenerateMainMethod)
    {
        var directories = WrapperDirectories(sourceRoot, filePath);
        if (directories.Count == 0)
            return null;

        var wrappers = directories.Select(NameMangler.ToNamespacePart).ToList();
        for (var i = 0; i + 1 < wrappers.Count; i++)
        {
            if (wrappers[i] == wrappers[i + 1])
                return new NestedNameCollision(directories[i], directories[i + 1], wrappers[i], InnerIsModule: false);
        }

        var moduleClass = ModuleClassName(filePath, willGenerateMainMethod);
        return wrappers[^1] == moduleClass
            ? new NestedNameCollision(directories[^1], Path.GetFileName(filePath), moduleClass, InnerIsModule: true)
            : null;
    }

    /// <summary>
    /// The module class name a source file emits: the mangled file stem; the directory name for
    /// <c>__init__.spy</c>; <c>Program</c> for a <c>main.spy</c> that generates the entry point
    /// (avoids CS0542 <c>Main.Main()</c>).
    /// </summary>
    public static string ModuleClassName(string filePath, bool willGenerateMainMethod)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (fileName == DunderNames.Init)
        {
            var dirName = Path.GetFileName(Path.GetDirectoryName(filePath));
            return NameMangler.ToNamespacePart(dirName ?? "Module");
        }

        if (willGenerateMainMethod && fileName.Equals("main", StringComparison.OrdinalIgnoreCase))
            return "Program";

        return NameMangler.ToNamespacePart(fileName);
    }
}

/// <summary>
/// Two emitted classes of one name, one directly inside the other: <paramref name="Outer"/> is the
/// outer directory's source spelling, <paramref name="Inner"/> the inner directory's or the module
/// file's name (<paramref name="InnerIsModule"/>).
/// </summary>
internal sealed record NestedNameCollision(string Outer, string Inner, string Identifier, bool InnerIsModule);
