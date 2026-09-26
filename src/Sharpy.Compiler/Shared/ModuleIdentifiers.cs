using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// The C# identifiers a source file's position in a project emits (#1932, #1948, #2039): the module
/// namespace its directories and stem become, and its members class <c>&lt;X&gt;</c>. One authority,
/// read by the layout recorder (<c>CodeGenInfoComputer</c>, whose recorded <c>ModuleLayout</c> every
/// emitter family and the CLI's self-contained publish entry type read), by the emitter's AST-only
/// and unrecorded-submodule fallbacks, and by the project's pre-emission checks
/// (<c>ProjectCompiler.ReportPackageModuleNameCollisions</c>, SPY0526/SPY0523), so no two of them can
/// disagree on a spelling (#2013).
/// </summary>
internal static class ModuleIdentifiers
{
    /// <summary>
    /// The namespace segments <paramref name="filePath"/>'s directories emit, relative to
    /// <paramref name="sourceRoot"/> (the project's common source directory — NOT the project
    /// directory: sources under <c>src/</c> put the root at <c>src/</c>), each mangled through
    /// <see cref="NameMangler.ToNamespacePart"/> (#1948): <c>pkg/sub/lib.spy</c> → [Pkg, Sub], and a
    /// package's <c>pkg/sub/__init__.spy</c> → [Pkg, Sub] too — the package's module class lives
    /// INSIDE its own namespace. Empty for a root-level file or when either path is unknown
    /// (single-file compilation).
    /// </summary>
    public static List<string> ModuleNamespaceSegments(string? sourceRoot, string? filePath)
        => string.IsNullOrEmpty(sourceRoot) || string.IsNullOrEmpty(filePath)
            ? new List<string>()
            : SourceDirectories(sourceRoot, filePath).Select(NameMangler.ToNamespacePart).ToList();

    /// <summary>
    /// <c>&lt;X&gt;</c>, the module-members class a stem names (Decision 28 (b), ruling X3): the
    /// stem's namespace segment (<see cref="NameMangler.ToNamespacePart"/>) + <c>Module</c> —
    /// <c>pkg</c> → <c>PkgModule</c>, <c>thing</c> → <c>ThingModule</c>, <c>20260118_x</c> →
    /// <c>_20260118XModule</c>, the in-memory <c>&lt;source&gt;</c> → <c>SourceModule</c>
    /// (<see cref="LayoutMembersClassName"/>). Spelled FROM the segment, not by a second sanitizer, so
    /// a module's members class is its namespace's name plus <c>Module</c> for every stem (#2039).
    /// </summary>
    public static string MembersClassName(string stem)
        => NameMangler.ToNamespacePart(stem) + "Module";

    /// <summary>
    /// The namespace a module's members class and its sibling types live in under the
    /// module-as-namespace layout (#2039, Decision 28 (a)): the directory segments
    /// (<see cref="ModuleNamespaceSegments"/>) followed by the module's own stem — <c>pkg/thing.spy</c>
    /// → [Pkg, Thing]; a package's <c>pkg/__init__.spy</c> is the package itself → [Pkg]. With no
    /// source root (single-file compilation) the file is its own namespace: <c>thing.spy</c> → [Thing]
    /// (ruling 12, uniform). Relative to the project/root namespace, which is not included.
    /// </summary>
    public static List<string> LayoutNamespaceSegments(string? sourceRoot, string filePath)
    {
        var segments = ModuleNamespaceSegments(sourceRoot, filePath);
        var stem = Path.GetFileNameWithoutExtension(filePath);
        if (stem != DunderNames.Init)
            segments.Add(NameMangler.ToNamespacePart(stem));
        return segments;
    }

    /// <summary>
    /// <c>&lt;X&gt;</c> of a source file under the module-as-namespace layout (#2039): the members
    /// class of <c>thing.spy</c> is <c>ThingModule</c>, of <c>pkg/__init__.spy</c> <c>PkgModule</c>
    /// (<see cref="MembersClassName"/> of the stem, or of the package directory). Uniform in every mode
    /// — an entry <c>main.spy</c> is <c>MainModule</c> (its <c>Main()</c> is no longer its own class's
    /// name, so no <c>Program</c> special case).
    /// </summary>
    public static string LayoutMembersClassName(string filePath)
    {
        var stem = Path.GetFileNameWithoutExtension(filePath);
        if (stem != DunderNames.Init)
            return MembersClassName(stem);
        var dirName = Path.GetFileName(Path.GetDirectoryName(filePath));
        return string.IsNullOrEmpty(dirName) ? "Module" : MembersClassName(dirName);
    }

    /// <summary>
    /// The C# path a DOTTED module name spells (<c>my_pkg.sub_mod</c> → <c>MyPkg.SubMod</c>), each
    /// segment through <see cref="NameMangler.ToNamespacePart"/> — the spelling
    /// <see cref="ModuleNamespaceSegments"/> gives a directory. For a module known only by its dotted name (an
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
    /// importable beside <c>pkg.spy</c>. Compared by EMITTED namespace segment (<c>my_pkg.spy</c> +
    /// <c>myPkg/</c> collide too) — the module's own namespace segment, which does not depend on
    /// whether it declares the entry point (#2039: the entry module is no longer the class
    /// <c>Program</c>, so <c>main.spy</c> beside <c>program/</c> is two names, as in python).
    /// Returns (file, directory, identifier) for every such file.
    /// </summary>
    public static List<(string File, string Directory, string Identifier)> FindModuleBesideSameNamedPackage(
        string? sourceRoot, IReadOnlyCollection<string> sourceFiles)
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
            var identifier = NameMangler.ToNamespacePart(Path.GetFileNameWithoutExtension(file));
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
    /// level. The ONE predicate behind the SPY0403 entry-point requirement, the emitter's entry
    /// <c>Main()</c> and the incremental cache's recorded entry bit (#2013).
    /// </summary>
    public static bool DeclaresEntryMain(IEnumerable<Statement> body)
        => body.Any(s => s.UnwrapDecorated() is FunctionDef f && IsEntryMain(f));
}
