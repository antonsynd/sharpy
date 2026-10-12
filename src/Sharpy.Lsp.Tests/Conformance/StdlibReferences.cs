namespace Sharpy.Lsp.Tests.Conformance;

/// <summary>
/// The Core and Stdlib assemblies the test project copies beside itself — the references a corpus sweep
/// hands its <see cref="Sharpy.Compiler.CompilerApi"/> so stdlib imports resolve the way they do in the
/// server (which resolves the same pair through <c>DefaultReferenceSet</c>). Lifted from
/// <c>FrontEndParityTests</c> (P47) so the code-action sweep and the front-end parity sweep read one path.
/// </summary>
internal static class StdlibReferences
{
    /// <summary>The absolute paths of <c>Sharpy.Core.dll</c> and <c>Sharpy.Stdlib.dll</c> in the test output directory.</summary>
    internal static (string CorePath, string StdlibPath) Resolve()
    {
        var baseDir = Path.GetDirectoryName(typeof(StdlibReferences).Assembly.Location)!;
        return (Path.Combine(baseDir, "Sharpy.Core.dll"), Path.Combine(baseDir, "Sharpy.Stdlib.dll"));
    }

    /// <summary><see cref="Resolve"/> as a reference array, each path checked to exist.</summary>
    internal static string[] Paths()
    {
        var (core, stdlib) = Resolve();
        foreach (var path in new[] { core, stdlib })
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"{Path.GetFileName(path)} not found beside the test assembly", path);
        }

        return new[] { core, stdlib };
    }
}
