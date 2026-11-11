using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>
/// A2's standing positive control (plan-638e8b, #2476; the model of <see cref="DoorScanSyntheticTests"/>):
/// a synthetic source with each forbidden shape — a subclass creation, an unlisted plain
/// <c>OSError</c>, a throwing non-helper catch, an unlisted swallow — is reported, and one with the
/// helper-translate shape is not. One row per arm, so an arm never implemented is a red row and not a
/// silence. The synthetic sources bind against the REAL <c>Sharpy.Core</c> (the family and
/// <see cref="PathErrors"/> are read from it) and an external door assembly emitted to metadata, and are
/// judged through the same <see cref="MappingScan.Run"/> / <see cref="MappingScan.Judge"/> as the real
/// sources, with the reach <see cref="DoorScan.Run"/> derives.
/// </summary>
public class MappingSpelledOnceSyntheticTests
{
    private const string ExternalSource = """
        namespace Ext.IO
        {
            public class Door
            {
                public Door() { }
                public Door(string path) { }
                public static void Open(string path) { }
                public string WorkingDirectory { get; set; } = "";
            }
        }
        namespace Ext.Text
        {
            public static class Helper
            {
                public static string Use(string text) => text;
            }
        }
        """;

    private static readonly Lazy<MetadataReference[]> References = new(() =>
    {
        var framework = DoorScan.FrameworkReferences().ToList();
        var external = CSharpCompilation.Create("Ext",
            new[] { CSharpSyntaxTree.ParseText(ExternalSource, DoorScan.ParseOptions) }, framework, DoorScan.CompilationOptions);
        using var stream = new MemoryStream();
        var emitted = external.Emit(stream);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));

        var all = framework.Append(MetadataReference.CreateFromImage(stream.ToArray())).ToList();
        var core = typeof(Sharpy.OSError).Assembly;
        all.Add(MetadataReference.CreateFromFile(core.Location));
        var frameworkDir = RuntimeEnvironment.GetRuntimeDirectory();
        foreach (var name in core.GetReferencedAssemblies())
        {
            var location = Assembly.Load(name).Location;
            if (!location.StartsWith(frameworkDir, StringComparison.Ordinal))
                all.Add(MetadataReference.CreateFromFile(location));
        }
        return all.ToArray();
    });

    private static DoorMap Map() => new DoorMap()
        .Door("Ext.IO.Door", "synthetic door")
        .Path("Ext.IO.Door.Door(string)", "path")
        .NoPath("Ext.IO.Door.Door()", "no path")
        .Path("Ext.IO.Door.Open(string)", "path")
        .Path("Ext.IO.Door.WorkingDirectory", "value")
        .NotADoor("Ext.Text.Helper", "synthetic negative row");

    /// <summary>The upstream arm: a Core member is a door here exactly when it is among the upstream doors.</summary>
    private static readonly DoorScanOptions Upstream = new("Sharpy.Core",
        new HashSet<string>(StringComparer.Ordinal) { "Sharpy.PathErrors.Rename(string, string)" });

    private static MappingScanResult Scan(string members, string path = "Synthetic.cs")
    {
        var source = $$"""
            using System;
            using System.IO;
            using Ext.IO;
            using Ext.Text;
            namespace Synthetic
            {
                {{members}}
            }
            """;
        var compilation = CSharpCompilation.Create("Synthetic",
            new[] { CSharpSyntaxTree.ParseText(source, DoorScan.ParseOptions, path: path) },
            References.Value, DoorScan.CompilationOptions);
        var map = Map();
        var reach = DoorScan.Run(compilation, map, Upstream);
        var (plain, subclasses) = MappingScan.Family();
        var context = new MappingScanContext(map, Upstream, new HashSet<string>(reach.Reaching.Keys, StringComparer.Ordinal), plain, subclasses);
        var result = MappingScan.Run(compilation, context);
        Assert.True(result.Errors.Count == 0, string.Join("\n", result.Errors));
        Assert.Empty(result.Unbound);
        return result;
    }

    private static string Try(string attempt, string handler, string extra = "")
        => $$"""public static class E { public static void Entry(string p) { try { {{attempt}} } {{handler}} } {{extra}} }""";

    private const string Translate = "catch (Exception e) { throw Sharpy.PathErrors.TranslateNeedsAny(e, p); }";

    /// <summary>arm, source, a substring of the ONE expected problem (null: none), the judged catch shape ("none": no catch judged).</summary>
    public static IEnumerable<object?[]> Arms() => new[]
    {
        // ---- raise: a subclass creation is red with no roster ----
        new object?[] { "subclass creation", "public static class E { public static void Entry(string p) => throw new Sharpy.FileNotFoundError(p); }", "creates Sharpy.FileNotFoundError", "none" },
        new object?[] { "target-typed subclass creation", "public static class E { public static void Entry(string p) { Sharpy.IsADirectoryError e = new(p); throw e; } }", "creates Sharpy.IsADirectoryError", "none" },
        new object?[] { "a family member the plan added (by reflection)", "public static class E { public static void Entry(string p) => throw new Sharpy.NotADirectoryError(p); }", "creates Sharpy.NotADirectoryError", "none" },
        new object?[] { "a type derived from a subclass", "internal sealed class Mine : Sharpy.PermissionError { public Mine(string m) : base(m) { } } public static class E { public static void Entry(string p) => throw new Mine(p); }", "creates Synthetic.Mine", "none" },
        // ---- raise: plain OSError is a roster row ----
        new object?[] { "unlisted plain OSError", "public static class E { public static void Entry(string p) => throw new Sharpy.OSError(p); }", "raise: plain OSError: unlisted Synthetic.cs:E.Entry", "none" },
        new object?[] { "unlisted plain OSError, target-typed", "public static class E { public static void Entry(string p) { Sharpy.OSError e = new(p); throw e; } }", "raise: plain OSError: unlisted Synthetic.cs:E.Entry", "none" },
        // ---- catch: a throwing non-helper catch is red with no roster ----
        new object?[] { "throwing non-helper catch", Try("Door.Open(p);", "catch (IOException e) { throw new Sharpy.ValueError(e.Message); }"), "throws something other", "Throws" },
        new object?[] { "rethrow", Try("Door.Open(p);", "catch (Exception) { throw; }"), "throws something other", "Throws" },
        new object?[] { "a conditional with one non-translate arm", Try("Door.Open(p);", "catch (Exception ex) { throw p.Length > 0 ? Sharpy.PathErrors.TranslateNeedsAny(ex, p) : new IOException(p); }"), "throws something other", "Throws" },
        new object?[] { "re-raise of a CLR exception caught as a family BASE", Try("Door.Open(p);", "catch (IOException e) { throw e; }"), "throws something other", "Throws" },
        new object?[] { "translate of another value", Try("Door.Open(p);", "catch (Exception e) { throw Sharpy.PathErrors.TranslateNeedsAny(new IOException(), p); }"), "throws something other", "Throws" },
        // ---- catch: an unlisted swallow ----
        new object?[] { "general catch swallow", Try("Door.Open(p);", "catch { }"), "catch: swallow: unlisted Synthetic.cs:E.Entry", "Swallow" },
        new object?[] { "OSError swallow (IOException-derived)", Try("Door.Open(p);", "catch (Sharpy.OSError) { }"), "catch: swallow: unlisted", "Swallow" },
        new object?[] { "UnauthorizedAccessException swallow", Try("Door.Open(p);", "catch (UnauthorizedAccessException) { }"), "catch: swallow: unlisted", "Swallow" },
        new object?[] { "a base of the anchors (SystemException)", Try("Door.Open(p);", "catch (SystemException) { }"), "catch: swallow: unlisted", "Swallow" },
        new object?[] { "a fallback swallow", Try("Door.Open(p);", "catch (IOException) { Helper.Use(p); }"), "catch: swallow: unlisted", "Swallow" },
        new object?[] { "a partial translate (falls through)", Try("Door.Open(p);", "catch (Exception e) { if (p.Length > 0) throw Sharpy.PathErrors.TranslateNeedsAny(e, p); }"), "catch: swallow: unlisted", "Swallow" },
        new object?[] { "a re-raise on one path, a fallback on the other (shutil.move)", Try("Door.Open(p);", "catch (Sharpy.OSError) { if (p.Length == 0) throw; Helper.Use(p); }"), "catch: swallow: unlisted", "Swallow" },
        new object?[] { "a translate with an early return", Try("Door.Open(p);", "catch (Exception e) { if (p.Length == 0) return; throw Sharpy.PathErrors.TranslateNeedsAny(e, p); }"), "catch: swallow: unlisted", "Swallow" },
        // ---- catch: each reach edge of the try block ----
        new object?[] { "reach: creation", Try("var d = new Door(p);", "catch { }"), "catch: swallow: unlisted", "Swallow" },
        new object?[] { "reach: property write", Try("new Door().WorkingDirectory = p;", "catch { }"), "catch: swallow: unlisted", "Swallow" },
        new object?[] { "reach: private helper", Try("H(p);", "catch { }", "private static void H(string p) => Door.Open(p);"), "catch: swallow: unlisted", "Swallow" },
        new object?[] { "reach: lambda", Try("Action a = () => Door.Open(p); a();", "catch { }"), "catch: swallow: unlisted", "Swallow" },
        new object?[] { "reach: local function", "public static class E { public static void Entry(string p) { void L() => Door.Open(p); try { L(); } catch { } } }", "catch: swallow: unlisted", "Swallow" },
        new object?[] { "reach: virtual override", "public abstract class B { public abstract void Run(string p); } internal sealed class D : B { public override void Run(string p) => Door.Open(p); } " + Try("((B)new D()).Run(p);", "catch { }"), "catch: swallow: unlisted", "Swallow" },
        new object?[] { "reach: interface implementation", "public interface I { void Run(string p); } internal sealed class D : I { public void Run(string p) => Door.Open(p); } " + Try("((I)new D()).Run(p);", "catch { }"), "catch: swallow: unlisted", "Swallow" },
        new object?[] { "reach: an upstream (Core) door", Try("Sharpy.PathErrors.Rename(p, p);", "catch { }"), "catch: swallow: unlisted", "Swallow" },
        // ---- not reported ----
        new object?[] { "helper shape", Try("Door.Open(p);", Translate), null, "Translate" },
        new object?[] { "helper shape, dispatched on a mode (open()'s ladder)", Try("Door.Open(p);", "catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { switch (p) { case \"r\": throw Sharpy.PathErrors.TranslateNeedsFileToRead(ex, p); case \"x\": throw Sharpy.PathErrors.TranslateCreatesExclusively(ex, p); default: throw Sharpy.PathErrors.TranslateNeedsFileToWrite(ex, p); } }"), null, "Translate" },
        new object?[] { "helper shape, a conditional of two translates (Path.touch)", Try("Door.Open(p);", "catch (Exception ex) { throw p.Length > 0 ? Sharpy.PathErrors.TranslateNeedsFileToWrite(ex, p) : Sharpy.PathErrors.TranslateCreatesExclusively(ex, p); }"), null, "Translate" },
        new object?[] { "re-raise of an already-classified OSError", Try("Door.Open(p);", "catch (Sharpy.OSError) { throw; }"), null, "Translate" },
        new object?[] { "re-raise of an already-classified subclass by name", Try("Door.Open(p);", "catch (Sharpy.FileNotFoundError e) { throw e; }"), null, "Translate" },
        new object?[] { "the spy-generated helper shape", Try("global::System.IO.Directory.Exists(p); Door.Open(p);", "catch (global::System.Exception e) { throw global::Sharpy.PathErrors.TranslateRemovesDir(e, p); }"), null, "Translate" },
        new object?[] { "outside by the type predicate: InvalidDataException", Try("Door.Open(p);", "catch (InvalidDataException) { }"), null, "none" },
        new object?[] { "outside by the type predicate: FormatException", Try("Door.Open(p);", "catch (FormatException) { throw new Sharpy.ValueError(p); }"), null, "none" },
        new object?[] { "outside by reach: the try block reaches no door", Try("Helper.Use(p); new Door();", "catch (Exception) { throw new Sharpy.ValueError(p); }"), null, "none" },
        new object?[] { "a type derived from plain OSError outside Core", "internal sealed class SockError : Sharpy.OSError { public SockError(string m) : base(m) { } } public static class E { public static void Entry(string p) => throw new SockError(p); }", null, "none" },
    };

    [Theory]
    [MemberData(nameof(Arms))]
    public void EachArm_IsJudged(string arm, string source, string? expected, string shape)
    {
        var result = Scan(source);
        var problems = MappingScan.Judge(new[] { result }, Array.Empty<MappingRow>(), Array.Empty<MappingRow>());
        if (expected == null)
        {
            Assert.True(problems.Count == 0, $"{arm}: expected no problem, got:\n  " + string.Join("\n  ", problems));
        }
        else
        {
            Assert.True(problems.Count == 1 && problems[0].Contains(expected, StringComparison.Ordinal),
                $"{arm}: expected one problem containing `{expected}`, got:\n  " + string.Join("\n  ", problems));
        }

        var shapes = result.Catches.Select(c => c.Shape.ToString()).ToList();
        Assert.True(shape == "none" ? shapes.Count == 0 : shapes.SequenceEqual(new[] { shape }),
            $"{arm}: judged catch shapes [{string.Join(", ", shapes)}], want {shape}");
    }

    /// <summary>The helper's own file is where the mapping IS spelled.</summary>
    [Fact]
    public void TheHelpersFile_IsTheOneSpelling()
    {
        var source = "public static class E { public static void Entry(string p) => throw new Sharpy.FileNotFoundError(p); }";
        Assert.Single(Scan(source).SubclassCreations);
        var inHelper = Scan(source, MappingScan.HelperFile);
        Assert.True(inHelper.HelperFileSeen);
        Assert.Empty(inHelper.SubclassCreations);
    }

    private const string PlainSite = "public static class E { public static void Entry(string p) => throw new Sharpy.OSError(p); }";
    private const string Swallow = "public static class E { public static void Entry(string p) { try { Door.Open(p); } catch { } } }";

    /// <summary>Both rosters, both directions: a listed site is green, a wrong count, a stale row and an empty reason are red.</summary>
    [Fact]
    public void Rosters_AreEqualToTheScan_InBothDirections()
    {
        var plain = new[] { Scan(PlainSite) };
        var swallow = new[] { Scan(Swallow) };
        var none = Array.Empty<MappingRow>();
        const string Key = "Synthetic.cs:E.Entry";

        Assert.Empty(MappingScan.Judge(plain, new[] { new MappingRow(Key, 1, "not path state: synthetic") }, none));
        Assert.Empty(MappingScan.Judge(swallow, none, new[] { new MappingRow(Key, 1, "synthetic best-effort cleanup") }));

        Assert.Contains(MappingScan.Judge(plain, new[] { new MappingRow(Key, 2, "x") }, none), p => p.Contains("the roster row says 2", StringComparison.Ordinal));
        Assert.Contains(MappingScan.Judge(swallow, none, new[] { new MappingRow(Key, 2, "x") }), p => p.Contains("the roster row says 2", StringComparison.Ordinal));
        Assert.Contains(MappingScan.Judge(swallow, new[] { new MappingRow(Key, 1, "x") }, none), p => p.Contains("raise: plain OSError: stale roster row " + Key, StringComparison.Ordinal));
        Assert.Contains(MappingScan.Judge(plain, none, new[] { new MappingRow(Key, 1, "x") }), p => p.Contains("catch: swallow: stale roster row " + Key, StringComparison.Ordinal));
        Assert.Contains(MappingScan.Judge(plain, new[] { new MappingRow(Key, 1, " ") }, none), p => p.Contains("needs its reason", StringComparison.Ordinal));
    }

    /// <summary>No roster reaches the two red-with-no-roster arms: a row for their key excuses nothing.</summary>
    [Fact]
    public void SubclassCreations_AndThrowingCatches_HaveNoRoster()
    {
        const string Key = "Synthetic.cs:E.Entry";
        var row = new[] { new MappingRow(Key, 1, "an attempt to list it") };
        var subclass = new[] { Scan("public static class E { public static void Entry(string p) => throw new Sharpy.FileExistsError(p); }") };
        var throwing = new[] { Scan(Try("Door.Open(p);", "catch (IOException) { throw; }")) };
        Assert.Contains(MappingScan.Judge(subclass, row, row), p => p.Contains("creates Sharpy.FileExistsError", StringComparison.Ordinal));
        Assert.Contains(MappingScan.Judge(throwing, row, row), p => p.Contains("throws something other", StringComparison.Ordinal));
    }

    /// <summary>A translation site rewritten to spell plain <c>OSError</c> itself is red twice: as a throwing catch and as an unlisted creation.</summary>
    [Fact]
    public void ACatchThatThrowsPlainOSError_IsRedAsBoth()
    {
        var problems = MappingScan.Judge(new[] { Scan(Try("Door.Open(p);", "catch (IOException e) { throw new Sharpy.OSError(e.Message); }")) },
            Array.Empty<MappingRow>(), Array.Empty<MappingRow>());
        Assert.Contains(problems, p => p.Contains("throws something other", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("raise: plain OSError: unlisted Synthetic.cs:E.Entry", StringComparison.Ordinal));
    }

    /// <summary>The family is read by reflection, so a Core subclass added later joins unlisted.</summary>
    [Fact]
    public void Family_IsReadByReflection()
    {
        var (plain, subclasses) = MappingScan.Family();
        Assert.Equal("Sharpy.OSError", plain);
        Assert.Contains("Sharpy.FileNotFoundError", subclasses);
        Assert.Contains("Sharpy.NotADirectoryError", subclasses);
        Assert.DoesNotContain(plain, subclasses);
        Assert.True(subclasses.Count >= 5, $"family [{string.Join(", ", subclasses)}]");
    }
}
