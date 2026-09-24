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
    {
        if (string.IsNullOrEmpty(sourceRoot) || string.IsNullOrEmpty(filePath))
            return new List<string>();

        var relativePath = Path.GetRelativePath(sourceRoot, filePath);
        var relativeDir = Path.GetDirectoryName(relativePath) ?? "";
        if (string.IsNullOrEmpty(relativeDir) || relativeDir == ".")
            return new List<string>();

        var segments = relativeDir
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NameMangler.ToNamespacePart)
            .ToList();

        if (Path.GetFileNameWithoutExtension(filePath) == DunderNames.Init && segments.Count > 0)
            segments.RemoveAt(segments.Count - 1);

        return segments;
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
