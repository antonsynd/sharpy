using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// The C# identifiers a source file's position in a project emits (#1932): the directory WRAPPER
/// classes the module class nests in, and the module class itself. One authority, read by the
/// emitter (<c>RoslynEmitter.ComputeWrapperClasses</c> / <c>GetModuleClassName</c>), by the
/// project's pre-emission check (<c>ProjectCompiler.ReportPackageModuleNameCollisions</c>, SPY0526),
/// by the function/module-class collision check (<c>CodeGenInfoComputer</c>, SPY0523) and by the
/// CLI's self-contained publish entry type, so no two of them can disagree on a spelling (#2013).
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
    /// (avoids CS0542 <c>Main.Main()</c>). <paramref name="willGenerateMainMethod"/> is
    /// <see cref="DeclaresEntryMain"/> of the file's body — every caller computes it with that one
    /// predicate (#2013).
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

    /// <summary>
    /// The C# path of the class a source file's module-level members live in, relative to the project
    /// namespace: its wrapper segments followed by its module class (<c>pkg/lib.spy</c> →
    /// <c>Pkg.Lib</c>, <c>pkg/__init__.spy</c> → <c>Pkg</c>). THE path authority every cross-file
    /// reference reads (#1948): the type-naming seam, the module-access emitter and the from-import
    /// member qualifier all spell a module's container through this one function, so they cannot drift
    /// from what <see cref="WrapperSegments"/> and <see cref="ModuleClassName"/> declare. A non-entry
    /// spelling: an entry <c>main.spy</c>'s members are never referenced from another file.
    /// </summary>
    public static string ModuleClassPath(string? sourceRoot, string filePath)
    {
        var segments = WrapperSegments(sourceRoot, filePath);
        segments.Add(ModuleClassName(filePath, willGenerateMainMethod: false));
        return string.Join(".", segments);
    }

    /// <summary>
    /// The C# path a DOTTED module name spells (<c>my_pkg.sub_mod</c> → <c>MyPkg.SubMod</c>), each
    /// segment through <see cref="NameMangler.ToNamespacePart"/> — the spelling
    /// <see cref="WrapperSegments"/> gives a directory. For a module known only by its dotted name (an
    /// import with no resolved file, a stdlib module, a CLR namespace).
    /// </summary>
    public static string DottedModulePath(string dottedModuleName)
        => string.Join(".", dottedModuleName
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(NameMangler.ToNamespacePart));

    /// <summary>
    /// The python dotted module name a source file declares (<c>pkg/lib.spy</c> → <c>pkg.lib</c>,
    /// <c>pkg/__init__.spy</c> → <c>pkg</c>) — the <c>[SharpyModule]</c> name. A root-level or
    /// single-file module is its stem; a root-level or single-file <c>__init__.spy</c> is
    /// <c>module</c>.
    /// </summary>
    public static string SharpyModuleName(string? sourceRoot, string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return "module";

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var parts = string.IsNullOrEmpty(sourceRoot) ? new List<string>() : SourceDirectories(sourceRoot, filePath);
        if (fileName != DunderNames.Init)
            parts.Add(fileName);

        return parts.Count > 0 ? string.Join(".", parts) : "module";
    }

    /// <summary>
    /// Refusal 1 of SPY0526 (#1948): a module file and a package directory of one name side by side
    /// (<c>pkg.spy</c> + <c>pkg/x.spy</c>, with or without <c>pkg/__init__.spy</c>). Python imports only
    /// one of them (the package with <c>__init__</c>, the module without), so <c>pkg.x</c> is never
    /// importable beside <c>pkg.spy</c>; in C# the module class and the directory spell one identifier
    /// in one scope. Compared by EMITTED identifier (<c>my_pkg.spy</c> + <c>myPkg/</c> collide too).
    /// Returns (file, directory, identifier) for every such file.
    /// </summary>
    public static List<(string File, string Directory, string Identifier)> FindModuleBesideSameNamedPackage(
        string? sourceRoot, IReadOnlyCollection<string> sourceFiles, Func<string, bool> willGenerateMainMethod)
    {
        var result = new List<(string, string, string)>();
        if (string.IsNullOrEmpty(sourceRoot))
            return result;

        // (parent directory, emitted identifier) → the child directory's source spelling
        var childDirectories = new Dictionary<(string Parent, string Identifier), string>();
        foreach (var file in sourceFiles)
        {
            var dirs = SourceDirectories(sourceRoot, file);
            for (var i = 0; i < dirs.Count; i++)
            {
                var parent = string.Join("/", dirs.Take(i));
                childDirectories.TryAdd((parent, NameMangler.ToNamespacePart(dirs[i])), dirs[i]);
            }
        }

        foreach (var file in sourceFiles)
        {
            if (Path.GetFileNameWithoutExtension(file) == DunderNames.Init)
                continue;
            var parent = string.Join("/", SourceDirectories(sourceRoot, file));
            var identifier = ModuleClassName(file, willGenerateMainMethod(file));
            if (childDirectories.TryGetValue((parent, identifier), out var directory))
                result.Add((file, directory, identifier));
        }

        return result;
    }

    /// <summary>
    /// The emitted identifiers of a package's children: every module file directly in
    /// <paramref name="packageDirectory"/> (other than its <c>__init__.spy</c>) and every subdirectory
    /// that holds a source file, each mapped to its source spelling (#1948).
    /// </summary>
    public static Dictionary<string, string> PackageChildIdentifiers(
        string? sourceRoot, string packageDirectory, IReadOnlyCollection<string> sourceFiles)
    {
        var children = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(sourceRoot))
            return children;

        var packageDirs = SourceDirectories(sourceRoot, Path.Combine(packageDirectory, DunderNames.Init + ".spy"));
        foreach (var file in sourceFiles)
        {
            var dirs = SourceDirectories(sourceRoot, file);
            if (dirs.Count < packageDirs.Count || !dirs.Take(packageDirs.Count).SequenceEqual(packageDirs))
                continue;

            if (dirs.Count == packageDirs.Count)
            {
                var stem = Path.GetFileNameWithoutExtension(file);
                if (stem != DunderNames.Init)
                    children.TryAdd(NameMangler.ToNamespacePart(stem), Path.GetFileName(file));
            }
            else
            {
                var sub = dirs[packageDirs.Count];
                children.TryAdd(NameMangler.ToNamespacePart(sub), sub + "/");
            }
        }

        return children;
    }

    /// <summary>
    /// The C# identifier each top-level declaration of a module body emits as a member of its
    /// module class — functions, types, variables and constants — spelled through the same
    /// <see cref="NameCasing"/> rules <c>CodeGenInfoComputer</c> materializes (a variable in
    /// SCREAMING_SNAKE_CASE is a constant-cased field, any other a PascalCase field). Read by
    /// refusal 2 of SPY0526 (#1948), which runs before analysis, so it cannot read the materialized
    /// names; <c>ModulePathAuthorityTests</c> pins it against emitted C#. Deliberately partial: a
    /// statement that declares no member of the module class (an import, an expression) yields nothing.
    /// </summary>
    public static IEnumerable<(string Name, string Identifier, Statement Declaration)> TopLevelMemberIdentifiers(
        IEnumerable<Statement> body)
    {
        foreach (var stmt in body)
        {
            switch (stmt.UnwrapDecorated())
            {
                case FunctionDef f:
                    yield return (f.Name, NameCasing.ResolveMethod(f.Name, f.IsNameBacktickEscaped), stmt);
                    break;
                case VariableDeclaration v:
                    yield return (v.Name, v.IsConst || NameFormDetector.IsConstantCaseName(v.Name)
                        ? NameCasing.ResolveConstant(v.Name, v.IsNameBacktickEscaped)
                        : NameCasing.ResolveField(v.Name, v.IsNameBacktickEscaped), stmt);
                    break;
                case ClassDef c:
                    yield return (c.Name, NameCasing.ResolveType(c.Name, c.IsNameBacktickEscaped), stmt);
                    break;
                case StructDef st:
                    yield return (st.Name, NameCasing.ResolveType(st.Name, st.IsNameBacktickEscaped), stmt);
                    break;
                case EnumDef e:
                    yield return (e.Name, NameCasing.ResolveType(e.Name, e.IsNameBacktickEscaped), stmt);
                    break;
                case UnionDef u:
                    yield return (u.Name, NameCasing.ResolveType(u.Name, u.IsNameBacktickEscaped), stmt);
                    break;
                case DelegateDef d:
                    yield return (d.Name, NameCasing.ResolveType(d.Name, d.IsNameBacktickEscaped), stmt);
                    break;
                case InterfaceDef i:
                    yield return (i.Name, NameCasing.ResolveInterface(i.Name, i.IsNameBacktickEscaped), stmt);
                    break;
            }
        }
    }

    /// <summary>The source spellings of every directory between the root and the file.</summary>
    private static List<string> SourceDirectories(string sourceRoot, string filePath)
    {
        var relativeDir = Path.GetDirectoryName(Path.GetRelativePath(sourceRoot, filePath)) ?? "";
        if (string.IsNullOrEmpty(relativeDir) || relativeDir == ".")
            return new List<string>();
        return relativeDir.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    /// <summary>
    /// Whether <paramref name="function"/> is the entry-point <c>main</c>: named <c>main</c> and NOT
    /// backtick-escaped. The backticks mean "this spelling, literally", so <c>def `main`</c> is an
    /// ordinary function emitted verbatim as <c>main()</c> in every mode — never the C# entry point,
    /// never the non-entry <c>MainFunc</c> rename (owner ruling 2026-09-24, #2013).
    /// </summary>
    public static bool IsEntryMain(FunctionDef function)
        => function.Name == "main" && !function.IsNameBacktickEscaped;

    /// <summary>
    /// Whether a module body declares the entry-point <c>main</c> (<see cref="IsEntryMain"/>) at top
    /// level. The ONE predicate behind the module class name (<see cref="ModuleClassName"/>), the
    /// SPY0403 entry-point requirement and the incremental cache's recorded entry bit (#2013).
    /// </summary>
    public static bool DeclaresEntryMain(IEnumerable<Statement> body)
        => body.Any(s => s.UnwrapDecorated() is FunctionDef f && IsEntryMain(f));
}

/// <summary>
/// Two emitted classes of one name, one directly inside the other: <paramref name="Outer"/> is the
/// outer directory's source spelling, <paramref name="Inner"/> the inner directory's or the module
/// file's name (<paramref name="InnerIsModule"/>).
/// </summary>
internal sealed record NestedNameCollision(string Outer, string Inner, string Identifier, bool InnerIsModule);
