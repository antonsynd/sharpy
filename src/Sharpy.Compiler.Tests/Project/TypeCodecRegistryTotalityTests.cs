using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Reflection-driven totality guard: every concrete <see cref="SemanticType"/> subclass must
/// round-trip through <c>TypeCodecRegistry.Serialize</c>/<c>Deserialize</c> or be in the
/// exclusion set. The exclusion set is asserted equal to exactly <c>{ConstructorReferenceType}</c>
/// — adding a new entry without updating this test is a build failure, not a silent miss (#1751).
///
/// <para>The exclusion's reason is itself asserted by a positive control: exporting
/// <c>MK = C</c> from <c>lib.spy</c> is refused SPY0342 at the cold build, so
/// <c>ConstructorReferenceType</c> cannot reach the serializer.</para>
/// </summary>
public class TypeCodecRegistryTotalityTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public TypeCodecRegistryTotalityTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_codec_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        GC.SuppressFinalize(this);
    }

    private static readonly HashSet<string> ExclusionSet = new() { "ConstructorReferenceType" };

    [Fact]
    public void ExclusionSet_IsExactlyOneEntry()
    {
        ExclusionSet.Should().BeEquivalentTo(new[] { "ConstructorReferenceType" },
            "the exclusion set must be exactly {ConstructorReferenceType} — never grows silently");
    }

    [Fact]
    public void EveryConcreteSemanticType_EitherRoundTripsOrIsExcluded()
    {
        var concreteTypes = typeof(SemanticType).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(SemanticType).IsAssignableFrom(t))
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToList();

        concreteTypes.Should().NotBeEmpty(
            "reflection must find the sealed SemanticType subclasses");

        var roundTripFailures = new List<string>();
        var roundTripped = new List<string>();

        foreach (var typeName in concreteTypes)
        {
            if (ExclusionSet.Contains(typeName))
                continue;

            var specimen = CreateSpecimen(typeName);
            if (specimen == null)
            {
                roundTripFailures.Add(
                    $"{typeName}: no specimen in CreateSpecimen — add an arm for it");
                continue;
            }

            var symbol = new FunctionSymbol
            {
                Name = "probe",
                Kind = SymbolKind.Function,
                Parameters = new List<ParameterSymbol>(),
                ReturnType = specimen,
            };

            try
            {
                var cached = SymbolSerializer.Serialize(symbol, "/test.spy");
                var restored = (FunctionSymbol)SymbolSerializer.Deserialize(
                    cached, new Dictionary<string, Symbol>(StringComparer.Ordinal));

                if (restored.ReturnType is UnknownType && specimen is not UnknownType)
                {
                    roundTripFailures.Add(
                        $"{typeName}: decoded to UnknownType (lossy round-trip — "
                        + "the codec is registered but the decoder lost the payload)");
                }
                else
                {
                    roundTripped.Add(typeName);
                }
            }
            catch (NotSupportedException ex)
            {
                roundTripFailures.Add(
                    $"{typeName}: threw NotSupportedException (not registered): {ex.Message}");
            }
            catch (Exception ex)
            {
                roundTripFailures.Add(
                    $"{typeName}: threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        _output.WriteLine($"Round-tripped: {roundTripped.Count}");
        foreach (var name in roundTripped)
            _output.WriteLine($"  {name}");
        _output.WriteLine($"Excluded: {ExclusionSet.Count}");
        _output.WriteLine($"Failures: {roundTripFailures.Count}");

        roundTripFailures.Should().BeEmpty(
            "every non-excluded concrete SemanticType must survive a Serialize/Deserialize "
            + "round trip. Failures:\n  " + string.Join("\n  ", roundTripFailures));

        var uncovered = concreteTypes
            .Where(n => !roundTripped.Contains(n) && !ExclusionSet.Contains(n))
            .ToList();
        uncovered.Should().BeEmpty(
            "every concrete type must either round-trip or be excluded");
    }

    [Fact]
    public void ExclusionSet_HasNoGhosts()
    {
        var concreteTypes = typeof(SemanticType).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(SemanticType).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToHashSet();

        var ghosts = ExclusionSet.Where(n => !concreteTypes.Contains(n)).ToList();
        ghosts.Should().BeEmpty(
            "every exclusion entry must name a real concrete SemanticType — ghosts indicate "
            + "a deleted type still in the set");
    }

    /// <summary>
    /// Positive control for the <c>ConstructorReferenceType</c> exclusion: exporting
    /// <c>MK = C</c> from a module is refused SPY0342 at the cold build, so the kind
    /// cannot reach the serializer.
    /// </summary>
    [Fact]
    public void ConstructorReferenceType_CannotReachSerializer_BecauseSPY0342RefusesExport()
    {
        var libSource = @"class MyClass:
    val: int
    def __init__(self, val: int) -> None:
        self.val = val

MK = MyClass
";
        var mainSource = @"from lib import MK

def main() -> None:
    print(""hello"")
";
        var libPath = WriteTempFile("spy0342", "lib.spy", libSource);
        var mainPath = WriteTempFile("spy0342", "main.spy", mainSource);

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(Path.GetDirectoryName(libPath)!, "test.spyproj"),
            ProjectDirectory = Path.GetDirectoryName(libPath)!,
            RootNamespace = "CodecTest",
            SourceFiles = new List<string> { libPath, mainPath },
            Configuration = "Debug",
        };

        var result = new Compiler(
            new CompilerOptions { Incremental = false },
            NullLogger.Instance).CompileProject(config);

        var allDiagnostics = result.Diagnostics.GetAll().ToList();
        var messages = allDiagnostics.Select(d => $"{d.Code}: {d.Message}").ToList();

        _output.WriteLine("Diagnostics:");
        foreach (var m in messages)
            _output.WriteLine($"  {m}");

        messages.Should().Contain(m => m.Contains("SPY0342") || m.Contains("SPY0301"),
            "a module-level `MK = MyClass` export must be refused — "
            + "the constructor reference kind cannot reach the serializer");
    }

    private string WriteTempFile(string area, string name, string content)
    {
        var dir = Path.Combine(_tempDir, area);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static SemanticType? CreateSpecimen(string typeName) => typeName switch
    {
        "UnknownType" => SemanticType.Unknown,
        "VoidType" => SemanticType.Void,
        "BuiltinType" => SemanticType.Int,
        "GenericType" => new GenericType { Name = "list", TypeArguments = { SemanticType.Int } },
        "UserDefinedType" => new UserDefinedType { Name = "MyType" },
        "UnmappedClrType" => new UnmappedClrType
        {
            ClrTypeName = "System.Text.StringBuilder",
            ClrType = typeof(System.Text.StringBuilder)
        },
        "NullableType" => new NullableType { UnderlyingType = SemanticType.Int },
        "OptionalType" => new OptionalType { UnderlyingType = SemanticType.Int },
        "ResultType" => new ResultType { OkType = SemanticType.Int, ErrorType = SemanticType.Str },
        "FunctionType" => new FunctionType
        {
            ParameterTypes = { SemanticType.Int },
            ReturnType = SemanticType.Str
        },
        "TupleType" => new TupleType { ElementTypes = { SemanticType.Int, SemanticType.Str } },
        "TaskType" => new TaskType { ResultType = SemanticType.Int },
        "TemplateType" => TemplateType.Instance,
        "TypeParameterType" => new TypeParameterType { Name = "T" },
        "GenericFunctionType" => new GenericFunctionType
        {
            FunctionSymbol = new FunctionSymbol
            {
                Name = "identity",
                Kind = SymbolKind.Function,
                Parameters = new List<ParameterSymbol>
                {
                    new() { Name = "x", Type = SemanticType.Int }
                },
                ReturnType = SemanticType.Int,
            },
            TypeArguments = { SemanticType.Int }
        },
        "ModuleType" => new ModuleType
        {
            Symbol = new ModuleSymbol { Name = "testmod" }
        },
        "UnionType" => new UnionType
        {
            Name = "Result",
            CaseTypes = { SemanticType.Int, SemanticType.Str }
        },
        "LiteralStringType" => LiteralStringType.Instance,
        "SelfType" => new SelfType(),
        _ => null,
    };
}
