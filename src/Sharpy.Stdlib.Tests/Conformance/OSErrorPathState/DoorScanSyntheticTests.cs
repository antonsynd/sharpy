using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>
/// A1 (g) — the scan itself, as a STANDING theory with one row per arm (the model of
/// <c>ProcessWideResolutionScanTests.Synthetic_EachSiteKind_IsFound</c>), so an arm never implemented
/// is a red row and not a silence: each reference kind, each reach edge, each failure. The synthetic
/// sources run through the same <see cref="DoorScan.Run"/> as the real census, against an external
/// assembly emitted to metadata (so its members are "declared outside the scanned sources").
/// </summary>
public class DoorScanSyntheticTests
{
    private const string ExternalSource = """
        namespace Ext.IO
        {
            public class Door
            {
                public Door() { }
                public Door(string path) { }
                public static void Open(string path) { }
                public static void Touch(string path, int mode) { }
                public static int Count { get; set; }
                public string WorkingDirectory { get; set; } = "";
                public void Close() { }
            }
        }
        namespace Ext.Text
        {
            public static class Helper
            {
                public static string Use(string text) => text;
                public static int Length(int n) => n;
            }
            public class Untyped
            {
                public static int Size => 1;
            }
        }
        """;

    private static readonly Lazy<MetadataReference> External = new(() => Emit("Ext", ExternalSource));

    private static MetadataReference Emit(string name, string source, params MetadataReference[] extra)
    {
        var compilation = CSharpCompilation.Create(name,
            new[] { CSharpSyntaxTree.ParseText(source, DoorScan.ParseOptions) },
            DoorScan.FrameworkReferences().Concat(extra), DoorScan.CompilationOptions);
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static DoorMap Map() => new DoorMap()
        .Door("Ext.IO.Door", "synthetic door")
        .Path("Ext.IO.Door.Door(string)", "path")
        .NoPath("Ext.IO.Door.Door()", "no path")
        .Path("Ext.IO.Door.Open(string)", "path")
        .Path("Ext.IO.Door.Touch(string, int)", "path")
        .NoPath("Ext.IO.Door.Count", "a number")
        .Path("Ext.IO.Door.WorkingDirectory", "value")
        .NoPath("Ext.IO.Door.Close()", "acts on the open door")
        .NotADoor("Ext.Text.Helper", "synthetic negative row");

    private static DoorScanResult Scan(string members, DoorMap? map = null, DoorScanOptions? options = null, params MetadataReference[] extra)
    {
        var source = $$"""
            using System;
            using Ext.IO;
            using Ext.Text;
            namespace Synthetic
            {
                {{members}}
            }
            """;
        var compilation = CSharpCompilation.Create("Synthetic",
            new[] { CSharpSyntaxTree.ParseText(source, DoorScan.ParseOptions, path: "Synthetic.cs") },
            DoorScan.FrameworkReferences().Append(External.Value).Concat(extra), DoorScan.CompilationOptions);
        return DoorScan.Run(compilation, map ?? Map(), options ?? DoorScanOptions.None);
    }

    public static IEnumerable<object[]> ReferenceKinds() => new[]
    {
        new object[] { DoorScan.Invocation, "Ext.IO.Door.Open(string)", "public static class E { public static void Entry(string p) => Door.Open(p); }" },
        new object[] { DoorScan.Creation, "Ext.IO.Door.Door(string)", "public static class E { public static object Entry(string p) => new Door(p); }" },
        new object[] { DoorScan.Creation, "Ext.IO.Door.Door(string)", "public static class E { public static object Entry(string p) { Door d = new(p); return d; } }" },
        new object[] { DoorScan.PropertyRead, "Ext.IO.Door.Count", "public static class E { public static int Entry(string p) => Door.Count; }" },
        new object[] { DoorScan.PropertyWrite, "Ext.IO.Door.WorkingDirectory", "public static class E { public static object Entry(string p) => new Door { WorkingDirectory = p }; }" },
        new object[] { DoorScan.MethodGroup, "Ext.IO.Door.Open(string)", "public static class E { public static Action<string> Entry(string p) => Door.Open; }" },
    };

    /// <summary>Each reference kind is found and keyed, bound, with its kind.</summary>
    [Theory]
    [MemberData(nameof(ReferenceKinds))]
    public void EachReferenceKind_IsFound(string kind, string member, string source)
    {
        var result = Scan(source);
        Assert.True(result.Errors.Count == 0, string.Join("\n", result.Errors));
        Assert.Empty(result.Unbound);
        Assert.Contains(result.External, r => r.Kind == kind && DoorScan.MemberKey(r.Member) == member);
    }

    public static IEnumerable<object[]> ReachEdges() => new[]
    {
        new object[] { "direct", "public static class E { public static void Entry(string p) => Door.Open(p); }" },
        new object[] { "creation", "public static class E { public static void Entry(string p) { var d = new Door(p); } }" },
        new object[] { "property write", "public static class E { public static void Entry(string p) { var d = new Door(); d.WorkingDirectory = p; } }" },
        new object[] { "method group", "public static class E { public static void Entry(string p) { Action<string> a = Door.Open; a(p); } }" },
        new object[] { "private helper", "public static class E { public static void Entry(string p) => Helper2(p); private static void Helper2(string p) => Door.Open(p); }" },
        new object[] { "lambda", "public static class E { public static void Entry(string p) { Action a = () => Door.Open(p); a(); } }" },
        new object[] { "local function", "public static class E { public static void Entry(string p) { void L() => Door.Open(p); L(); } }" },
        new object[] { "virtual override", "public abstract class B { public abstract void Run(string p); } internal sealed class D : B { public override void Run(string p) => Door.Open(p); } public static class E { public static void Entry(B b, string p) => b.Run(p); }" },
        new object[] { "interface implementation", "public interface I { void Run(string p); } internal sealed class D : I { public void Run(string p) => Door.Open(p); } public static class E { public static void Entry(I i, string p) => i.Run(p); }" },
        new object[] { "constructor to field initializer", "public sealed class E { private readonly Door _d = new Door(\"x\"); public E(string p) { } }" },
        new object[] { "property accessor", "public static class E { public static string Entry { set => Door.Open(value); } }" },
    };

    /// <summary>Each reach edge makes the public member an entry point; the same shape with no door call does not.</summary>
    [Theory]
    [MemberData(nameof(ReachEdges))]
    public void EachReachEdge_MakesAnEntryPoint(string edge, string source)
    {
        var result = Scan(source);
        Assert.True(result.Errors.Count == 0, edge + ": " + string.Join("\n", result.Errors));
        Assert.Empty(result.Unbound);
        var entry = edge == "constructor to field initializer" ? "Synthetic.E.E(string)" : edge == "property accessor" ? "Synthetic.E.Entry" : null;
        Assert.True(entry != null ? result.EntryPoints.ContainsKey(entry) : result.EntryPoints.Keys.Any(k => k.StartsWith("Synthetic.E.Entry(", StringComparison.Ordinal)),
            $"{edge}: no entry point; found [{string.Join(", ", result.EntryPoints.Keys)}]");

        // Control: the same shape with every door call swapped for a negative-row or takes-no-path
        // member reaches nothing — the entry point above is the door's doing, not the shape's.
        var control = Scan(source.Replace("Action<string> a = Door.Open;", "Action<string> a = s => Helper.Use(s);")
            .Replace("Door.Open(p)", "Helper.Use(p)").Replace("Door.Open(value)", "Helper.Use(value)")
            .Replace("new Door(p)", "new Door()").Replace("new Door(\"x\")", "new Door()")
            .Replace("d.WorkingDirectory = p", "d.Close()"));
        Assert.True(control.Errors.Count == 0, edge + " control: " + string.Join("\n", control.Errors));
        Assert.Empty(control.EntryPoints);
    }

    /// <summary>A Core-like upstream member is a door in the downstream pass exactly when the upstream pass found it reaching one.</summary>
    [Fact]
    public void UpstreamMember_IsADoor_ExactlyWhenTheUpstreamPassReachedOne()
    {
        var upstreamSource = """
            namespace Up
            {
                public static class Builtins
                {
                    public static void Open(string p) => Ext.IO.Door.Open(p);
                    public static string Echo(string p) => p;
                }
            }
            """;
        var upstreamCompilation = CSharpCompilation.Create("Upstream",
            new[] { CSharpSyntaxTree.ParseText(upstreamSource, DoorScan.ParseOptions, path: "Up.cs") },
            DoorScan.FrameworkReferences().Append(External.Value), DoorScan.CompilationOptions);
        var upstream = DoorScan.Run(upstreamCompilation, Map(), DoorScanOptions.None);
        Assert.Equal(new[] { "Up.Builtins.Open(string)" }, upstream.Reaching.Keys);

        var reference = Emit("Upstream", upstreamSource, External.Value);
        var options = new DoorScanOptions("Upstream", new HashSet<string>(upstream.Reaching.Keys));
        var opens = Scan("public static class E { public static void Entry(string p) => Up.Builtins.Open(p); }", Map(), options, reference);
        var echoes = Scan("public static class E { public static void Entry(string p) => Up.Builtins.Echo(p); }", Map(), options, reference);
        Assert.Empty(opens.Errors);
        Assert.Contains("Synthetic.E.Entry(string)", opens.EntryPoints.Keys);
        Assert.DoesNotContain("Synthetic.E.Entry(string)", echoes.EntryPoints.Keys);
        Assert.Empty(echoes.Unclassified); // an upstream type is never classified by hand
    }

    /// <summary>Failure arm: an external type no row covers is reported (T1), naming the type.</summary>
    [Fact]
    public void Failure_UnclassifiedExternalType_IsReported()
    {
        var result = Scan("public static class E { public static int Entry() => Untyped.Size; }");
        Assert.Contains("Ext.Text.Untyped", result.UnclassifiedTypes);
    }

    /// <summary>Failure arm: a reference that does not bind is reported, never skipped.</summary>
    [Fact]
    public void Failure_AReferenceThatDoesNotBind_IsReported()
    {
        var result = Scan("public static class E { public static void Entry(string p) => Door.Open(p, p, p); }");
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Unbound, u => u.Contains("Door.Open", StringComparison.Ordinal));
    }

    /// <summary>Failure arm: a referenced member of a door type with no member row is reported (T2).</summary>
    [Fact]
    public void Failure_ADoorMemberWithNoRow_IsReported()
    {
        var map = Map();
        map.Members.Remove("Ext.IO.Door.Touch(string, int)");
        var result = Scan("public static class E { public static void Entry(string p) => Door.Touch(p, 1); }", map);
        Assert.Contains(result.MissingMemberRows, m => m.StartsWith("Ext.IO.Door.Touch(string, int)", StringComparison.Ordinal));
    }

    /// <summary>Failure arm: a new path-capable member under a negative row lands in the census under that row (T2's diff).</summary>
    [Fact]
    public void Failure_ANewPathCapableMemberUnderANegativeRow_IsCensused()
    {
        var without = Scan("public static class E { public static int Entry(string p) => Helper.Length(1); }");
        var with = Scan("public static class E { public static string Entry(string p) => Helper.Use(p); }");
        Assert.DoesNotContain(without.Census, c => c.Member.StartsWith("Ext.Text.Helper.", StringComparison.Ordinal));
        Assert.Contains(("type Ext.Text.Helper", "Ext.Text.Helper.Use(string)"), with.Census);
    }
}
