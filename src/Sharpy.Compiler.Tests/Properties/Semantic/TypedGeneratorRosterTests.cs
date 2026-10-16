using System.Reflection;
using System.Text.RegularExpressions;
using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using Module = Sharpy.Compiler.Parser.Ast.Module;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

/// <summary>
/// The typed-generator feature roster (#2288 items 1 and 4): every construct the language spec
/// defines has a row stating, per lane, what the typed generator under
/// <c>Properties/Generators/Typed/</c> proves about it.
/// <list type="bullet">
/// <item><b>Universe</b> — every <c>*.md</c> directly in <c>docs/language_specification/</c> except
/// <c>README.md</c>, enumerated at test time (a directory scan, never the README's links). A file with no
/// row is red; a row whose file is gone is red; fewer than 100 files is red (a degraded path would
/// pass vacuously).</item>
/// <item><b>Anchor</b> — each row names the <c>## </c> heading (or, for a heading-less file, the
/// <c># </c> title) that defines its construct, verbatim; a heading outside a code fence must carry it
/// or the row is stale.</item>
/// <item><b>Lanes</b> — <see cref="Y"/> (generated: a typed generator member emits the construct;
/// the member label must name a public static method on a type in the <c>Generators.Typed</c>
/// namespace, and the lambda calls that member with its consumer's arguments so a rename is a compile
/// error), <see cref="YInjected"/> (injected: the ErrorInjector method, or the invalid generator
/// member whose property judges the code, and the code it draws today — a literal string, never a
/// <c>DiagnosticCodes</c> constant), <see cref="KnownRedRow"/> (N or pinned: the lane is not proven,
/// and the cited issue owns the gap — one record for both so <c>build_tools/allowlist_issue_state.py</c>'s
/// <c>KNOWNRED_CELL_RE</c> sees every cite), <see cref="ByDesign"/> (the lane has nothing to prove by
/// spec), and <see cref="NoConstruct"/> (a whole file defines no construct).</item>
/// <item><b>Cite rules</b> (owner, 2026-10-15): a construct no typed generator emits cites #2287 (P62's
/// grammar-driven generator); a generator member that emits the construct but has no consuming
/// property cites #2288 ("wired in-plan"); an injection Phase 4 of P63 adds cites #2288
/// ("Phase 4 adds the injection"); a weak refusal lane Phase 2 re-judges cites #2288; a construct that is
/// generated, has a refusal, and has no injection outside Phase 4's eight lanes cites #2324 (the
/// injection-reach residue tracker) with the verbatim Observed
/// <c>"generated; no injection breaks it — outside Phase 4's eight lanes"</c>; compiler cells pinned
/// by an injection cite their tracker (#2143). A construct with no refusal surface is <c>ByDesign("no typing surface")</c>; same-scope
/// re-<c>let</c> is <see cref="ByDesign"/> by <c>variable_scoping.md</c> §Variable Shadowing.</item>
/// <item><b>NoConstruct exemption</b> — allowed only for a file whose fenced Sharpy blocks
/// (<c>```python</c>/<c>spy</c>/<c>sharpy</c>, the fences <c>build_tools/spec_blocks.py</c> reads) are all
/// marked <c>spec-sweep: fragment</c> or <c>spec-sweep: error</c> on the line above, or absent: a file
/// with a compiled example defines something, so the exemption cannot be its own subject.</item>
/// <item><b>RAW positive control</b> — every Y-generated row draws 40 samples at a fixed seed from the
/// UNFILTERED member, unparses <see cref="Node"/> output (each <see cref="Node"/>/<c>string</c> item of a
/// tuple), re-parses with the real parser, and requires its predicate to hit at least one node; a throw
/// or a parse error anywhere is red with the sample's source. Presence only: acceptance of the raw
/// sample is the generated lane's soundness property (P63 Phase 3).</item>
/// <item><b>Totality</b> — the counts are literal <c>const int</c>s bumped with the row that moves them,
/// and the coverage line is printed per run.</item>
/// </list>
/// </summary>
public class TypedGeneratorRosterTests
{
    private readonly ITestOutputHelper _output;

    public TypedGeneratorRosterTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---- Row model ---------------------------------------------------------------------------

    public abstract record Lane;

    /// <summary>Generated: <paramref name="Member"/> emits the construct; <paramref name="Predicate"/> finds it in the re-parsed AST.
    /// <paramref name="Samples"/> is the raw-corpus size at the fixed seed: 40, raised (never re-seeded) for a
    /// construct the member draws too rarely for 40 samples to hold a margin.</summary>
    public sealed record Y(string Member, Func<object> Generator, Func<Node, bool> Predicate, int Samples = RawSamples) : Lane;

    /// <summary>Injected: <paramref name="Member"/> breaks the construct and the program draws exactly <paramref name="Code"/>.</summary>
    public sealed record YInjected(string Member, string Code, CompilerDiagnosticSeverity Severity) : Lane;

    /// <summary>N or pinned: the lane is not proven today and <paramref name="Issue"/> owns it.</summary>
    public sealed record KnownRedRow(string Issue, string Observed) : Lane;

    /// <summary>The lane has nothing to prove, by spec.</summary>
    public sealed record ByDesign(string Reason) : Lane;

    /// <summary>The whole file defines no construct (both lanes of its single row).</summary>
    public sealed record NoConstruct(string Reason) : Lane;

    public sealed record RosterRow(string File, string Anchor, string Construct, Lane Generated, Lane Injected);

    private static RosterRow NoConstructRow(string file, string title, string reason) =>
        new(file, title, "(no construct)", new NoConstruct(reason), new NoConstruct(reason));

    private const string GeneratorsNamespace = "Sharpy.Compiler.Tests.Properties.Generators.Typed";

    /// <summary>The default raw-corpus size per Y-generated row (<see cref="Y.Samples"/>).</summary>
    private const int RawSamples = 40;

    // ---- Consumer-argument generators (each mirrors the property that samples the member) -----

    /// <summary><c>TypeSoundnessPropertyTests.TypedProgram_ExecutesWithoutRuntimeTypeErrors</c>.</summary>
    private static Gen<Module> TypedStatements() =>
        Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2, withStatements: true));

    /// <summary><c>TypeSoundnessPropertyTests</c>' collections fact.</summary>
    private static Gen<Module> TypedCollections() =>
        Gen.OneOfConst("int", "str", "list[int]").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.WithCollections, type, fuel: 2, withStatements: true));

    /// <summary><c>TypeSoundnessPropertyTests</c>' optionals fact.</summary>
    private static Gen<Module> TypedOptionals() =>
        Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.WithOptionals, type, fuel: 2));

    /// <summary><c>ErrorCompletenessPropertyTests.NarrowedProgram_AnalysesClean</c> — the first caller of
    /// <c>GenTyped.NarrowedBlock</c> (statements, an env with Optionals).</summary>
    private static Gen<Module> TypedOptionalStatements() =>
        Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.WithOptionals, type, fuel: 1, withStatements: true));

    /// <summary><c>ClassInheritancePropertyTests</c>.</summary>
    private static Gen<Module> Classes() => GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2);

    /// <summary><c>FunctionSemanticsPropertyTests</c> / <c>ErrorCompletenessPropertyTests</c>.</summary>
    private static Gen<Module> Functions() => GenFunctions.ModuleWithFunctions(TypeEnv.Default, "int", fuel: 2);

    // ---- Predicate helpers -------------------------------------------------------------------

    private static bool CallTo(Node n, params string[] names) =>
        n is FunctionCall { Function: Identifier id } && names.Contains(id.Name);

    private static bool DefNamed(Node n, params string[] names) =>
        n is FunctionDef f && names.Contains(f.Name);

    private static bool Decorated(Node n, string decorator) =>
        (n is FunctionDef f && f.Decorators.Any(d => d.Name == decorator))
        || (n is ClassDef c && c.Decorators.Any(d => d.Name == decorator));

    private static bool BinOp(Node n, params BinaryOperator[] ops) =>
        n is BinaryOp b && ops.Contains(b.Operator);

    private static bool IsNoneTest(Node n) =>
        n is BinaryOp { Operator: BinaryOperator.Is or BinaryOperator.IsNot, Right: NoneLiteral };

    // ---- The roster @ 8e0791f85 (tree of f8c37f986) -------------------------------------------

    private static readonly RosterRow[] Rows =
    {
        // arithmetic_operators.md
        new("arithmetic_operators.md", "Arithmetic Operators", "+ - * on int/float operands",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => BinOp(n, BinaryOperator.Subtract, BinaryOperator.Multiply), Samples: 100),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("arithmetic_operators.md", "Division Operator `/`", "true division /",
            new KnownRedRow("#2287", "no typed generator emits /"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),
        new("arithmetic_operators.md", "Floor Division Operator `//`", "floor division //",
            new KnownRedRow("#2287", "no typed generator emits //"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),
        new("arithmetic_operators.md", "Modulo Operator `%`", "% on numeric operands",
            new KnownRedRow("#2287", "no typed generator emits % on a numeric operand (GenOperators' % is a user __mod__)"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),
        new("arithmetic_operators.md", "Exponentiation Operator `**`", "exponentiation **",
            new KnownRedRow("#2287", "no typed generator emits **"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // assert_statement.md
        new("assert_statement.md", "Assert Statement", "assert statement",
            new KnownRedRow("#2287", "no typed generator emits assert"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // assignment_operators.md
        new("assignment_operators.md", "Assignment Operators", "augmented assignment (+= and friends)",
            new KnownRedRow("#2287", "no typed generator emits an augmented assignment"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // async_programming.md
        new("async_programming.md", "Async Functions", "async def awaited from an async caller",
            new Y("GenAsync.ModuleWithAsyncFunction", () => GenAsync.ModuleWithAsyncFunction(valid: true), n => n is AwaitExpression),
            new YInjected("GenAsync.ModuleWithAwaitOutsideAsync", "SPY0273", CompilerDiagnosticSeverity.Error)),
        new("async_programming.md", "Async Iteration", "async for / async generators",
            new KnownRedRow("#2287", "no typed generator emits async for or an async generator"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // bitwise_operators.md
        new("bitwise_operators.md", "Bitwise Operators", "& | ^ << >> on integer operands",
            new KnownRedRow("#2287", "no typed generator emits a bitwise operator on an integer operand (GenOperators' ~ is a user __invert__)"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // boolean_literals.md
        new("boolean_literals.md", "Boolean Literals", "True / False",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is BooleanLiteral),
            new ByDesign("no typing surface")),

        // break_continue.md
        new("break_continue.md", "Break and Continue", "break / continue",
            new KnownRedRow("#2287", "no typed generator emits break or continue"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // builtin_functions.md
        new("builtin_functions.md", "I/O Functions", "print()",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => CallTo(n, "print")),
            new ByDesign("no typing surface")),
        new("builtin_functions.md", "Collection Functions", "len() on a non-sized argument",
            new Y("GenTyped.TypedProgram", () => TypedCollections(), n => CallTo(n, "len"), Samples: 100),
            new YInjected("ErrorInjector.InjectWrongArgumentType", "SPY0320", CompilerDiagnosticSeverity.Error)),
        new("builtin_functions.md", "Collection Functions", "len() arity",
            new Y("GenTyped.TypedProgram", () => TypedCollections(), n => n is FunctionCall { Function: Identifier { Name: "len" }, Arguments.Length: 1 }, Samples: 100),
            new YInjected("ErrorInjector.InjectMissingArgument", "SPY0224", CompilerDiagnosticSeverity.Error)),
        new("builtin_functions.md", "Collection Functions", "range()",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => CallTo(n, "range")),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("builtin_functions.md", "Mathematical Functions", "abs()",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => CallTo(n, "abs")),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("builtin_functions.md", "Type Conversion", "str() / int() conversion",
            new Y("GenFStrings.FStringWithNestedCalls", () => GenFStrings.FStringWithNestedCalls(), n => CallTo(n, "str", "int")),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("builtin_functions.md", "Type Checking", "isinstance()",
            new KnownRedRow("#2287", "no typed generator emits isinstance"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // class_methods.md
        NoConstructRow("class_methods.md", "Class Methods",
            "states that @classmethod does not exist in Sharpy; no example block"),

        // classes.md
        new("classes.md", "Basic Class Definition", "class with typed fields, __init__ and methods",
            new Y("GenClasses.ModuleWithClasses", () => Classes(), n => n is ClassDef c && c.Body.OfType<VariableDeclaration>().Any(v => v.Type != null)),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("classes.md", "Field Initializer Expressions", "field with an initializer in the class body",
            new KnownRedRow("#2287", "no typed generator emits a field initializer"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // collection_types.md
        new("collection_types.md", "Collection Types", "list literal",
            new Y("GenTyped.TypedProgram", () => TypedCollections(), n => n is ListLiteral),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("collection_types.md", "Collection Types", "dict and set literals",
            new KnownRedRow("#2287", "no typed generator emits a dict or set literal"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),
        new("collection_types.md", "Collection Types", "tuple literal and tuple[...] annotation",
            new Y("GenMatchPatterns.MatchWithNestedPatterns", () => GenMatchPatterns.MatchWithNestedPatterns(), n => n is TupleLiteral),
            new ByDesign("no typing surface")),
        new("collection_types.md", "Slicing", "slices",
            new KnownRedRow("#2287", "no typed generator emits a slice"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // comments.md
        new("comments.md", "Comments", "# comments",
            new KnownRedRow("#2287", "no typed generator emits a comment (the Unparser prints none)"),
            new ByDesign("no typing surface")),

        // comparison_chaining.md
        new("comparison_chaining.md", "Comparison Chaining", "a < b < c",
            new KnownRedRow("#2287", "no typed generator emits a comparison chain"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // comparison_operators.md
        new("comparison_operators.md", "Comparison Operators", "== != < <= > >= on numeric operands",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => BinOp(n, BinaryOperator.Equal, BinaryOperator.NotEqual,
                BinaryOperator.LessThan, BinaryOperator.LessThanOrEqual, BinaryOperator.GreaterThan, BinaryOperator.GreaterThanOrEqual), Samples: 100),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // comprehensions.md
        new("comprehensions.md", "List Comprehensions", "comprehensions",
            new KnownRedRow("#2287", "no typed generator emits a comprehension"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // constructors.md
        new("constructors.md", "Constructors", "__init__ with typed parameters assigning fields",
            new Y("GenClasses.ModuleWithClasses", () => Classes(), n => n is FunctionDef { Name: "__init__", Parameters.Length: > 1 }),
            new YInjected("ErrorInjector.InjectWrongConstructorArgs", "SPY0224", CompilerDiagnosticSeverity.Error)),
        new("constructors.md", "Constructor Overloading", "overloaded __init__",
            new KnownRedRow("#2287", "no typed generator emits an overloaded constructor"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // context_managers.md
        new("context_managers.md", "Context Managers", "with over a class defining __enter__/__exit__",
            new Y("GenContextManagers.ValidContextManagerProgram", () => GenContextManagers.ValidContextManagerProgram(), n => n is WithStatement { IsAsync: false }),
            new YInjected("GenContextManagers.MissingEnterOrExitProgram", "SPY0324", CompilerDiagnosticSeverity.Error)),
        new("context_managers.md", "`as` Targets", "with ... as name, the name used in the body",
            new Y("GenContextManagers.ContextManagerWithAsBinding", () => GenContextManagers.ContextManagerWithAsBinding(), n => n is WithStatement w && w.Items.Any(i => i.Target != null)),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("context_managers.md", "Async Context Managers", "async with over __aenter__/__aexit__",
            new Y("GenContextManagers.AsyncContextManagerProgram", () => GenContextManagers.AsyncContextManagerProgram(), n => n is WithStatement { IsAsync: true }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("context_managers.md", "`__exit__` Signature Variants", "zero-argument __exit__(self)",
            new Y("GenContextManagers.ValidContextManagerProgram", () => GenContextManagers.ValidContextManagerProgram(), n => n is FunctionDef { Name: "__exit__", Parameters.Length: 1 }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // conventions.md
        new("conventions.md", "Function Return Type Annotations", "omitted return annotation (implicit -> None)",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is FunctionDef { Name: "main", ReturnType: null }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // conversion_operators.md
        new("conversion_operators.md", "Conversion Operators", "__implicit__ / __explicit__",
            new KnownRedRow("#2287", "no typed generator emits a conversion operator"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // dataclass.md
        new("dataclass.md", "Basic Usage", "@dataclass class constructed positionally",
            new Y("GenDecorators.ModuleWithDataclass", () => GenDecorators.ModuleWithDataclass(), n => Decorated(n, "dataclass")),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("dataclass.md", "Field Defaults", "dataclass field defaults",
            new KnownRedRow("#2287", "no typed generator emits a dataclass field default"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // decorators.md
        new("decorators.md", "Method Modifiers", "@virtual base method and @override in the derived class",
            new Y("GenClasses.ModuleWithClasses", () => Classes(), n => Decorated(n, "override")),
            new KnownRedRow("#2143", "analyses clean; `run` is SPY0908 CS0508 — no override signature check for a Sharpy base")),
        new("decorators.md", "Method Modifiers", "@override @final",
            new Y("GenDecorators.ModuleWithDecoratedFunction", () => GenDecorators.ModuleWithDecoratedFunction(validDecorator: true), n => Decorated(n, "final")),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("decorators.md", "Method Modifiers", "@abstract method in an @abstract class",
            new KnownRedRow("#2287", "no typed generator emits @abstract (only ErrorInjector.InjectMissingAbstractImplementation writes it)"),
            new KnownRedRow("#2143", "analyses clean; `run` is SPY0908 CS0534 — no missing-abstract-member check for a Sharpy base")),
        new("decorators.md", "Access Modifiers", "access-modifier decorators",
            new KnownRedRow("#2287", "no typed generator emits a valid access modifier (GenDecorators uses them only in conflicting pairs)"),
            new YInjected("GenDecorators.ModuleWithDecoratorStack", "SPY0430", CompilerDiagnosticSeverity.Error)),
        new("decorators.md", "Memoization (`@lru_cache`, `@cache`)", "memoization decorators",
            new KnownRedRow("#2287", "no typed generator emits @lru_cache or @cache"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // deferred_features.md
        NoConstructRow("deferred_features.md", "Deferred Features",
            "lists features deferred to newer C#/.NET targets; no example block"),

        // del_statement.md
        new("del_statement.md", "The `del` Statement", "del statement",
            new KnownRedRow("#2287", "no typed generator emits del"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // delegates.md
        new("delegates.md", "Delegate Type Declarations", "delegate declarations",
            new KnownRedRow("#2287", "no typed generator emits a delegate"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // diagnostic_suppression.md
        new("diagnostic_suppression.md", "Diagnostic Suppression (`@suppress`)", "@suppress",
            new KnownRedRow("#2287", "no typed generator emits @suppress"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // dotnet_interop.md
        new("dotnet_interop.md", "Importing .NET Types", "CLR import and a CLR method call",
            new KnownRedRow("#2287", "no typed generator emits a CLR import (GenImports is Sharpy modules only); `Math.abs(\"x\")` draws SPY0220 @ f8c37f986"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // dstrings.md
        new("dstrings.md", "D-Strings (Dedented String Literals)", "d-strings",
            new KnownRedRow("#2287", "no typed generator emits a d-string"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // dunder_invocation_rules.md
        new("dunder_invocation_rules.md", "Dunders Are Definition-Only", "a dunder reached through its operator, never called by name",
            new Y("GenOperators.BinaryOperatorProgram", () => GenOperators.BinaryOperatorProgram(), n => BinOp(n, BinaryOperator.Add, BinaryOperator.Subtract, BinaryOperator.Multiply, BinaryOperator.Modulo)),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // dunder_methods.md
        new("dunder_methods.md", "Arithmetic Operators", "arithmetic and unary dunder definitions",
            new Y("GenOperators.BinaryOperatorProgram", () => GenOperators.BinaryOperatorProgram(), n => DefNamed(n, "__add__", "__sub__", "__mul__", "__mod__")),
            new YInjected("GenOperators.WrongArityOperatorDunderProgram", "SPY0321", CompilerDiagnosticSeverity.Error)),
        new("dunder_methods.md", "Comparison Operators", "comparison dunder definitions",
            new Y("GenOperators.ComparisonOperatorProgram", () => GenOperators.ComparisonOperatorProgram(), n => DefNamed(n, "__lt__", "__le__", "__gt__", "__ge__", "__eq__", "__ne__")),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("dunder_methods.md", "Special Methods", "__len__ / __bool__ protocol synthesis",
            new Y("GenInterfaces.ModuleWithProtocolDunder", () => Gen.OneOfConst("__len__", "__bool__").SelectMany(GenInterfaces.ModuleWithProtocolDunder), n => DefNamed(n, "__len__", "__bool__")),
            new KnownRedRow("#2288", "Phase 4 adds the injection")),
        new("dunder_methods.md", "Special Methods", "__iter__ / __next__ iterator protocol",
            new Y("GenIterators.ModuleWithIterProtocol", () => GenIterators.ModuleWithIterProtocol(complete: true), n => DefNamed(n, "__next__")),
            new YInjected("GenIterators.ModuleWithIterProtocol", "SPY0320", CompilerDiagnosticSeverity.Error)),

        // dunder_methods_recommendations.md
        new("dunder_methods_recommendations.md", "1. Equality Contract (`__eq__` / `__hash__`)", "user __eq__",
            new Y("GenOperators.ComparisonOperatorProgram", () => GenOperators.ComparisonOperatorProgram(), n => DefNamed(n, "__eq__")),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // ellipsis_literal.md
        new("ellipsis_literal.md", "Ellipsis Literal", "... as a placeholder body",
            new Y("GenInterfaces.ModuleWithInterface", () => GenInterfaces.ModuleWithInterface(methodCount: 0, completeImpl: true), n => n is EllipsisLiteral),
            new ByDesign("no typing surface")),

        // enums.md
        new("enums.md", "Simple Enums", "enums",
            new KnownRedRow("#2287", "no typed generator emits an enum"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // events.md
        new("events.md", "Events", "events",
            new KnownRedRow("#2287", "no typed generator emits an event"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // exception_handling.md
        new("exception_handling.md", "Try/Except/Finally", "try/except with a typed, named handler",
            new Y("GenExceptions.ModuleWithTryExcept", () => GenExceptions.ModuleWithTryExcept(handlerCount: 1), n => n is TryStatement t && t.Handlers.Any(h => h.ExceptionType != null && h.Name != null)),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("exception_handling.md", "Try/Except/Finally", "nested try",
            new Y("GenExceptions.ModuleWithNestedTry", () => GenExceptions.ModuleWithNestedTry(), n => n is TryStatement t && t.Body.Any(s => s is TryStatement)),
            new ByDesign("no typing surface")),
        new("exception_handling.md", "Raise Statement", "raise of a constructed exception",
            new Y("GenExceptions.ModuleWithRaiseExpression", () => GenExceptions.ModuleWithRaiseExpression(), n => n is RaiseStatement { Exception: not null }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("exception_handling.md", "Raise Statement", "bare raise inside an except block",
            new Y("GenExceptions.ModuleWithBareRaise", () => GenExceptions.ModuleWithBareRaise(insideExcept: true), n => n is RaiseStatement { Exception: null }),
            new YInjected("GenExceptions.ModuleWithBareRaise", "SPY0242", CompilerDiagnosticSeverity.Error)),
        new("exception_handling.md", "Exception Type Hierarchy", "subclass handler before its base",
            new Y("GenExceptions.ModuleWithExceptionHierarchy", () => GenExceptions.ModuleWithExceptionHierarchy(), n => n is TryStatement { Handlers.Length: >= 2 }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("exception_handling.md", "Exception Filters (`when`)", "except ... when",
            new KnownRedRow("#2287", "no typed generator emits an exception filter"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),
        new("exception_handling.md", "Control Transfer in `finally` (SPY0702)", "finally clause",
            new KnownRedRow("#2287", "no typed generator emits a finally clause"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // expression_blocks.md
        new("expression_blocks.md", "Expression Blocks", "expression blocks",
            new KnownRedRow("#2287", "no typed generator emits an expression block"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // expressions.md
        new("expressions.md", "Conditional Expression (Ternary)", "a if c else b",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is ConditionalExpression),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("expressions.md", "Member Access", "obj.member",
            new Y("GenClasses.ModuleWithClasses", () => Classes(), n => n is MemberAccess),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("expressions.md", "Function Calls", "call to a user function: argument count",
            new Y("GenFunctions.ModuleWithFunctions", () => Functions(), n => CallTo(n, "compute", "process", "transform")),
            new YInjected("ErrorInjector.InjectWrongFunctionArgCount", "SPY0224", CompilerDiagnosticSeverity.Error)),
        new("expressions.md", "Function Calls", "call to an undefined function",
            new Y("GenFunctions.ModuleWithFunctions", () => Functions(), n => CallTo(n, "compute", "process", "transform")),
            new YInjected("ErrorInjector.InjectUndefinedFunctionCall", "SPY0200", CompilerDiagnosticSeverity.Error)),
        new("expressions.md", "Index Access", "list[index]",
            new Y("GenTyped.ListSubscript", () => GenTyped.ListSubscript(TypeEnv.WithCollections, "int"), n => n is IndexAccess),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // extended_numeric_literals.md
        new("extended_numeric_literals.md", "Extended Numeric Literals", "suffixed / based numeric literals",
            new KnownRedRow("#2287", "no typed generator emits a suffixed or based numeric literal"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // feature_roadmap.md
        new("feature_roadmap.md", "Feature Roadmap", "the roadmap's example programs",
            new KnownRedRow("#2287", "no typed generator emits the roadmap's tiered constructs"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // flexible_arguments.md
        new("flexible_arguments.md", "Tier 0: Positional-Only and Keyword-Only Parameters", "positional-only / keyword-only parameters",
            new KnownRedRow("#2287", "no typed generator emits / or * in a parameter list"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // float_literals.md
        new("float_literals.md", "Float Literals", "float literals",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is FloatLiteral, Samples: 100),
            new ByDesign("no typing surface")),

        // for_statement.md
        new("for_statement.md", "For Loop", "for i in range(n)",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is ForStatement),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // fstrings.md
        new("fstrings.md", "F-Strings (Formatted String Literals)", "f-string with an expression hole",
            new Y("GenFStrings.FStringWithArithmetic", () => GenFStrings.FStringWithArithmetic(), n => n is FStringLiteral f && f.Parts.Any(p => p.Expression is BinaryOp)),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("fstrings.md", "Format Spec Grammar", "format spec in a hole",
            new Y("GenFStrings.FStringWithFormatSpecs", () => GenFStrings.FStringWithFormatSpecs(), n => n is FStringLiteral f && f.Parts.Any(p => p.Spec != null)),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("fstrings.md", "Interpolation Hole Type", "method call in a hole",
            new Y("GenFStrings.FStringWithMethodCalls", () => GenFStrings.FStringWithMethodCalls(), n => n is FStringLiteral f && f.Parts.Any(p => p.Expression is FunctionCall)),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // function_default_parameters.md
        new("function_default_parameters.md", "Function Default Parameters", "default parameter values",
            new KnownRedRow("#2287", "no typed generator emits a default parameter value"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // function_definition.md
        new("function_definition.md", "Function Definition", "def with typed parameters and a return annotation",
            new Y("GenFunctions.ModuleWithFunctions", () => Functions(), n => n is FunctionDef { ReturnType: not null, Name: "compute" or "process" or "transform" }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // function_parameters.md
        new("function_parameters.md", "Function Parameters", "typed positional parameters bound at a call",
            new Y("GenFunctions.ModuleWithFunctions", () => Functions(), n => n is FunctionDef f && f.Parameters.Any(p => p.Type != null)),
            new YInjected("ErrorInjector.InjectWrongFunctionArgType", "SPY0220", CompilerDiagnosticSeverity.Error)),
        new("function_parameters.md", "Named (Keyword) Arguments", "keyword arguments",
            new KnownRedRow("#2287", "no typed generator emits a keyword argument"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // function_types.md
        new("function_types.md", "Function Types", "function type annotations",
            new KnownRedRow("#2287", "no typed generator emits a function type"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // function_variadic_arguments.md
        new("function_variadic_arguments.md", "Function Variadic Arguments (`*args`)", "*args",
            new KnownRedRow("#2287", "no typed generator emits *args"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // generators.md
        new("generators.md", "Syntax", "generator function with yield",
            new Y("GenIterators.ModuleWithGenerator", () => GenIterators.ModuleWithGenerator(valid: true), n => n is YieldStatement { IsFrom: false }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("generators.md", "Syntax", "yield from",
            new Y("GenIterators.ModuleWithYieldFrom", () => GenIterators.ModuleWithYieldFrom(), n => n is YieldStatement { IsFrom: true }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("generators.md", "Return Type Annotation", "element-type annotation on a generator",
            new Y("GenIterators.ModuleWithGenerator", () => GenIterators.ModuleWithGenerator(valid: true), n => n is FunctionDef { ReturnType: not null } f && f.Body.OfType<YieldStatement>().Any()),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // generic_variance.md
        new("generic_variance.md", "Covariance (`out T`)", "out T on an interface",
            new Y("GenVariance.CovariantInterfaceProgram", () => GenVariance.CovariantInterfaceProgram(), n => n is InterfaceDef i && i.TypeParameters.Any(t => t.Variance == TypeParameterVariance.Covariant)),
            new YInjected("GenVariance.CovariantInInputPositionProgram", "SPY0418", CompilerDiagnosticSeverity.Error)),
        new("generic_variance.md", "Contravariance (`in T`)", "in T on an interface",
            new Y("GenVariance.ContravariantInterfaceProgram", () => GenVariance.ContravariantInterfaceProgram(), n => n is InterfaceDef i && i.TypeParameters.Any(t => t.Variance == TypeParameterVariance.Contravariant)),
            new YInjected("GenVariance.ContravariantInOutputPositionProgram", "SPY0419", CompilerDiagnosticSeverity.Error)),
        new("generic_variance.md", "Restrictions", "variance only on interfaces and delegates",
            new ByDesign("a variance annotation on a class is refused by spec; the valid forms are the interface rows above"),
            new YInjected("GenVariance.VarianceOnClassProgram", "SPY0417", CompilerDiagnosticSeverity.Error)),

        // generics.md
        new("generics.md", "Generic Classes", "class Box[T]",
            new Y("GenGenerics.GenericClassProgram", () => GenGenerics.GenericClassProgram(TypeEnv.WithGenerics, fuel: 2), n => n is ClassDef { TypeParameters.Length: 1 }),
            new YInjected("GenGenerics.TypeMismatchOnGenericFieldProgram", "SPY0220", CompilerDiagnosticSeverity.Error)),
        new("generics.md", "Generic Classes", "multiple type parameters Pair[A, B]",
            new Y("GenGenerics.MultiTypeParamProgram", () => GenGenerics.MultiTypeParamProgram(TypeEnv.WithGenerics, fuel: 2), n => n is ClassDef { TypeParameters.Length: >= 2 }),
            new YInjected("GenGenerics.WrongTypeArgCountProgram", "SPY0224", CompilerDiagnosticSeverity.Error)),
        new("generics.md", "Generic Classes", "generic class deriving a generic base",
            new Y("GenGenerics.GenericWithInheritanceProgram", () => GenGenerics.GenericWithInheritanceProgram(TypeEnv.WithGenerics, fuel: 2), n => n is ClassDef c && c.TypeParameters.Length > 0 && c.BaseClasses.Any(b => b.TypeArguments.Length > 0)),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("generics.md", "Generic Functions", "def identity[T]",
            new Y("GenGenerics.GenericFunctionProgram", () => GenGenerics.GenericFunctionProgram(TypeEnv.WithGenerics, fuel: 2), n => n is FunctionDef { TypeParameters.Length: > 0 }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("generics.md", "Generic Function Instantiation", "identity[int](...)",
            new Y("GenGenerics.GenericFunctionProgram", () => GenGenerics.GenericFunctionProgram(TypeEnv.WithGenerics, fuel: 2), n => n is FunctionCall { Function: IndexAccess { Object: Identifier { Name: "identity" } } }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("generics.md", "Type Constraints", "type-parameter constraint",
            new Y("GenGenerics.GenericConstraintProgram", () => GenGenerics.GenericConstraintProgram(TypeEnv.WithGenerics, fuel: 1), n => n is ClassDef { TypeParameters: [{ Constraints.Length: > 0 }, ..] } || n is FunctionDef { TypeParameters: [{ Constraints.Length: > 0 }, ..] }),
            new YInjected("ErrorInjector.InjectConstraintViolationAtInferredCall", "SPY0237", CompilerDiagnosticSeverity.Error)),
        new("generics.md", "Multiple Type Constraints", "several constraints on one type parameter",
            new KnownRedRow("#2287", "no typed generator emits multiple constraints"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // identifiers.md
        new("identifiers.md", "Identifiers", "identifier reference",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is Identifier { Name: "x" or "y" or "s" or "flag" }),
            new YInjected("ErrorInjector.InjectUndefinedVariable", "SPY0200", CompilerDiagnosticSeverity.Error)),
        new("identifiers.md", "Literal Names (Backtick Escaping)", "backtick-escaped names",
            new KnownRedRow("#2287", "no typed generator emits a backtick-escaped name"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // identity_operators.md
        new("identity_operators.md", "Identity Operators", "x is None / x is not None",
            new Y("GenTyped.TypedProgram", () => TypedOptionals(), n => IsNoneTest(n), Samples: 100),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // if_statement.md
        new("if_statement.md", "If Statement", "if / else",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is IfStatement { ElseBody.Length: > 0 }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // import_statements.md
        new("import_statements.md", "From-Import Statement", "from lib import name",
            new Y("GenImports.ImportingModule", () => GenImports.ImportingModule(TypeEnv.Default, fuel: 1), n => n is FromImportStatement),
            new YInjected("GenImports.NameCollisionPair", "SPY0204", CompilerDiagnosticSeverity.Error)),
        new("import_statements.md", "From-Import Statement", "unused from-import",
            new Y("GenImports.MultiFileProgram", () => GenImports.MultiFileProgram(TypeEnv.Default, fuel: 1), n => n is FromImportStatement),
            new YInjected("GenImports.UnusedImportProgram", "SPY0452", CompilerDiagnosticSeverity.Warning)),
        new("import_statements.md", "Import Statement", "import module",
            new KnownRedRow("#2287", "no typed generator emits a plain import statement"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // indentation.md
        new("indentation.md", "Indentation", "nested indented blocks",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is IfStatement or ForStatement),
            new ByDesign("no typing surface")),

        // inheritance.md
        new("inheritance.md", "Single Class Inheritance", "class Derived(Base)",
            new Y("GenClasses.ModuleWithClasses", () => Classes(), n => n is ClassDef { BaseClasses.Length: 1 }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("inheritance.md", "The `super()` Function", "super().__init__(...)",
            new Y("GenClasses.ModuleWithClasses", () => Classes(), n => n is SuperExpression),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("inheritance.md", "Multiple Interface Implementation", "class implementing two interfaces",
            new Y("GenInterfaces.ModuleWithMultipleInterfaces", () => GenInterfaces.ModuleWithMultipleInterfaces(), n => n is ClassDef { BaseClasses.Length: >= 2 }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // integer_literals.md
        new("integer_literals.md", "Integer Literals", "integer literals",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is IntegerLiteral),
            new ByDesign("no typing surface")),

        // interface_default_methods.md
        new("interface_default_methods.md", "Interface Default Methods", "interface methods with a default body",
            new KnownRedRow("#2287", "no typed generator emits an interface default method"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // interfaces.md
        new("interfaces.md", "Interfaces", "interface implemented by a class",
            new Y("GenInterfaces.ModuleWithInterface", () => GenInterfaces.ModuleWithInterface(methodCount: 0, completeImpl: true), n => n is InterfaceDef),
            new KnownRedRow("#2288", "Phase 4 adds the injection")),
        new("interfaces.md", "Interface Inheritance", "interface deriving an interface",
            new Y("GenInterfaces.ModuleWithInterfaceHierarchy", () => GenInterfaces.ModuleWithInterfaceHierarchy(), n => n is InterfaceDef { BaseInterfaces.Length: > 0 }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("interfaces.md", "Generic Interfaces", "interface I[T]",
            new Y("GenVariance.CovariantInterfaceProgram", () => GenVariance.CovariantInterfaceProgram(), n => n is InterfaceDef { TypeParameters.Length: > 0 }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // introduction.md
        NoConstructRow("introduction.md", "Introduction",
            "goals and axioms; no example block"),

        // keywords.md
        new("keywords.md", "Keywords", "soft keywords and reserved words in name positions",
            new KnownRedRow("#2287", "no typed generator emits a soft keyword or reserved word in a name position"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // lambdas.md
        new("lambdas.md", "Lambda Expressions", "lambdas",
            new KnownRedRow("#2287", "no typed generator emits a lambda"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // lexer_implementation.md
        new("lexer_implementation.md", "Lexer Implementation Notes", "lexer edge cases (bracket depth, line joining)",
            new KnownRedRow("#2287", "no typed generator targets lexer edge cases"),
            new ByDesign("no typing surface")),

        // literal_string.md
        new("literal_string.md", "LiteralString Type", "LiteralString",
            new KnownRedRow("#2287", "no typed generator emits a LiteralString slot"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // logical_operators.md
        new("logical_operators.md", "Logical Operators", "and / or",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => BinOp(n, BinaryOperator.And, BinaryOperator.Or), Samples: 100),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("logical_operators.md", "Truthiness", "if on a non-bool operand",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is IfStatement
            {
                Test: IntegerLiteral or StringLiteral or ListLiteral or Identifier { Name: "x" or "y" or "s" }
                    or BinaryOp { Operator: BinaryOperator.Subtract or BinaryOperator.Multiply }
            }, Samples: 200),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // loop_else.md
        new("loop_else.md", "Loop Else Clause", "for/while else",
            new KnownRedRow("#2287", "no typed generator emits a loop else clause"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // match_statement.md
        new("match_statement.md", "Match Statement", "match statement with a wildcard arm",
            new Y("GenMatchPatterns.MatchWithOrPatterns", () => GenMatchPatterns.MatchWithOrPatterns(), n => n is MatchStatement m && m.Cases.Any(c => c.Pattern is WildcardPattern)),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("match_statement.md", "Guard Patterns", "case p if guard",
            new Y("GenMatchPatterns.MatchWithGuards", () => GenMatchPatterns.MatchWithGuards(), n => (n is MatchStatement m && m.Cases.Any(c => c.Guard != null)) || n is GuardPattern),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("match_statement.md", "Supported Patterns", "or-patterns",
            new Y("GenMatchPatterns.MatchWithOrPatterns", () => GenMatchPatterns.MatchWithOrPatterns(), n => n is OrPattern),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("match_statement.md", "Supported Patterns", "list patterns with a star capture",
            new Y("GenMatchPatterns.MatchWithListPatterns", () => GenMatchPatterns.MatchWithListPatterns(), n => n is ListPattern l && l.Elements.Any(e => e is StarPattern)),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("match_statement.md", "Supported Patterns", "and-patterns",
            new Y("GenMatchPatterns.MatchWithAndPatterns", () => GenMatchPatterns.MatchWithAndPatterns(), n => n is AndPattern),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("match_statement.md", "Positional Patterns", "class patterns case C()",
            new Y("GenMatchPatterns.MatchWithTypePatterns", () => GenMatchPatterns.MatchWithTypePatterns(), n => n is PositionalPattern { Type: not null } or TypePattern),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("match_statement.md", "Tuple Patterns", "tuple patterns",
            new Y("GenMatchPatterns.MatchWithNestedPatterns", () => GenMatchPatterns.MatchWithNestedPatterns(), n => n is TuplePattern),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("match_statement.md", "Constant Patterns", "literal constant patterns",
            new Y("GenMatchPatterns.MatchWithOrPatterns", () => GenMatchPatterns.MatchWithOrPatterns(), n => n is LiteralPattern),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("match_statement.md", "Exhaustiveness Checking", "non-exhaustive match statement (warning)",
            new Y("GenMatchPatterns.MatchWithOrPatterns", () => GenMatchPatterns.MatchWithOrPatterns(), n => n is MatchStatement m && m.Cases[^1].Pattern is WildcardPattern),
            new YInjected("GenMatchPatterns.MatchNonExhaustive", "SPY0463", CompilerDiagnosticSeverity.Warning)),
        new("match_statement.md", "Match Statement vs Match Expression", "match expression",
            new KnownRedRow("#2287", "no typed generator emits a match expression"),
            new KnownRedRow("#2288", "Phase 4 adds the injection")),
        new("match_statement.md", "Property Patterns", "property patterns",
            new KnownRedRow("#2287", "no typed generator emits a property pattern"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // maybe_expressions.md
        new("maybe_expressions.md", "Maybe Expressions", "maybe expressions",
            new KnownRedRow("#2287", "no typed generator emits maybe"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // membership_operators.md
        new("membership_operators.md", "Membership Operators", "in / not in",
            new KnownRedRow("#2287", "no typed generator emits in or not in"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // method_overloading.md
        new("method_overloading.md", "Function and Method Overloading", "overloaded functions and methods",
            new KnownRedRow("#2287", "no typed generator emits an overload set"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // module_resolution.md
        NoConstructRow("module_resolution.md", "Module Resolution",
            "module-name conversion to .NET namespaces; no example block — the import construct is import_statements.md's"),

        // module_system.md
        new("module_system.md", "Circular Import Handling", "circular import refusal",
            new ByDesign("a circular import is refused by spec; the valid import forms are import_statements.md's rows"),
            new YInjected("GenImports.CircularImportPair", "SPY0302", CompilerDiagnosticSeverity.Error)),

        // name_mangling.md
        new("name_mangling.md", "Name Mangling", "snake_case to PascalCase collisions and acronyms",
            new KnownRedRow("#2287", "no typed generator emits a mangling collision or acronym case"),
            new ByDesign("no typing surface")),

        // name_shadowing.md
        new("name_shadowing.md", "3. Builtin names in *value* position — permitted, warned (SPY0483)", "builtin name shadowed in value position",
            new KnownRedRow("#2287", "no typed generator shadows a builtin name"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // named_tuples.md
        new("named_tuples.md", "Named Tuples", "named tuples",
            new KnownRedRow("#2287", "no typed generator emits a named tuple"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // naming_conventions.md
        new("naming_conventions.md", "Compile-Time Enforcement", "naming-convention warning",
            new ByDesign("every generated identifier follows the convention; the rule is a property of names, not a construct"),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // nested_types.md
        new("nested_types.md", "Nested Types", "nested type declarations",
            new KnownRedRow("#2287", "no typed generator emits a nested type"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // none_literal.md
        new("none_literal.md", "None Literal", "None",
            new Y("GenTyped.TypedProgram", () => TypedOptionals(), n => n is NoneLiteral),
            new ByDesign("no typing surface")),

        // null_coalescing_assignment.md
        new("null_coalescing_assignment.md", "Null-Coalescing Assignment Operator", "??=",
            new KnownRedRow("#2287", "no typed generator emits ??="),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // null_coalescing_operator.md
        new("null_coalescing_operator.md", "Null-Coalescing Operator", "??",
            new KnownRedRow("#2287", "no typed generator emits ??"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // null_conditional_access.md
        new("null_conditional_access.md", "Null-Conditional Access", "?.",
            new KnownRedRow("#2287", "no typed generator emits ?."),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // nullable_types.md
        new("nullable_types.md", "Nullable Types (.NET Interop)", "T | None",
            new KnownRedRow("#2287", "no typed generator emits T | None"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // operator_overloading.md
        new("operator_overloading.md", "Arithmetic Operators", "binary operator on a user type",
            new Y("GenOperators.BinaryOperatorProgram", () => GenOperators.BinaryOperatorProgram(), n => n is BinaryOp { Left: Identifier { Name: "a" }, Right: Identifier { Name: "b" } }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("operator_overloading.md", "Arithmetic Operators", "unary operator on a user type",
            new Y("GenOperators.UnaryOperatorProgram", () => GenOperators.UnaryOperatorProgram(), n => n is UnaryOp { Operand: Identifier { Name: "v" } }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("operator_overloading.md", "Comparison Operators", "comparison operator on a user type",
            new Y("GenOperators.ComparisonOperatorProgram", () => GenOperators.ComparisonOperatorProgram(), n => n is BinaryOp { Left: Identifier { Name: "a" }, Right: Identifier { Name: "b" } }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // operator_precedence.md
        new("operator_precedence.md", "Operator Precedence", "additive over multiplicative",
            new Y("GenOperators.PrecedenceProgram", () => GenOperators.PrecedenceProgram(), n => n is BinaryOp
            {
                Operator: BinaryOperator.Add or BinaryOperator.Subtract,
                Right: BinaryOp { Operator: BinaryOperator.Multiply or BinaryOperator.Modulo }
            }),
            new ByDesign("no typing surface")),

        // overload_resolution.md
        new("overload_resolution.md", "Overload Resolution", "overload resolution",
            new KnownRedRow("#2287", "no typed generator emits an overload set"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // parameter_modifiers.md
        new("parameter_modifiers.md", "Parameter Modifiers (`ref`, `out`, `in`)", "ref / out / in parameters",
            new KnownRedRow("#2287", "no typed generator emits a parameter modifier"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // partial_application.md
        new("partial_application.md", "Partial Application", "partial application",
            new KnownRedRow("#2287", "no typed generator emits a placeholder call"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // pass_statement.md
        new("pass_statement.md", "Pass Statement", "pass",
            new Y("GenInterfaces.ModuleWithInterface", () => GenInterfaces.ModuleWithInterface(methodCount: 0, completeImpl: true), n => n is PassStatement),
            new ByDesign("no typing surface")),

        // pipe_operator.md
        new("pipe_operator.md", "Pipe Operator", "|>",
            new KnownRedRow("#2287", "no typed generator emits a pipe"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // primitive_types.md
        new("primitive_types.md", "Built-in Primitive Types", "int / str / bool annotations",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is VariableDeclaration { Type.Name: "int" or "str" or "bool" }),
            new ByDesign("no typing surface")),

        // program_entry_point.md
        new("program_entry_point.md", "Entry Point Function", "def main()",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => DefNamed(n, "main")),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // properties*.md
        new("properties.md", "Properties", "properties",
            new KnownRedRow("#2287", "no typed generator emits a property"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),
        new("properties_function_style.md", "Function-Style Properties", "function-style properties",
            new KnownRedRow("#2287", "no typed generator emits a function-style property"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),
        new("properties_inheritance.md", "Properties - Inheritance", "virtual / override properties",
            new KnownRedRow("#2287", "no typed generator emits an inherited property"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // question_mark_operator.md
        new("question_mark_operator.md", "Question-Mark Operator (`?`)", "postfix ?",
            new KnownRedRow("#2287", "no typed generator emits the ? operator"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // rejected_proposals.md
        new("rejected_proposals.md", "Rejected Proposals", "the accepted alternatives the file shows",
            new KnownRedRow("#2287", "no typed generator emits the alternatives (expression blocks, explicit kwargs) the file shows"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // return_statement.md
        new("return_statement.md", "Return Statement", "return of a typed value",
            new Y("GenFunctions.ModuleWithFunctions", () => Functions(), n => n is ReturnStatement { Value: not null }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // source_files.md
        new("source_files.md", "Source Files", "encoding, line joining and logical lines",
            new KnownRedRow("#2287", "no typed generator varies encoding, line joining or logical-line structure"),
            new ByDesign("no typing surface")),

        // source_generators.md
        new("source_generators.md", "Source Generators", "source generators",
            new KnownRedRow("#2287", "no typed generator emits a source-generator trigger"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // spread_operator.md
        new("spread_operator.md", "List Spreading", "spread in a list display",
            new KnownRedRow("#2287", "no typed generator emits a spread element"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // statements.md
        new("statements.md", "Expression Statement", "expression statement",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is ExpressionStatement),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("statements.md", "Declaration Keywords", "let declarations",
            new KnownRedRow("#2287", "no typed generator emits let"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),
        new("statements.md", "Constants", "const declaration and its reassignment",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is VariableDeclaration { IsConst: true }),
            new YInjected("ErrorInjector.InjectConstReassignment", "SPY0225", CompilerDiagnosticSeverity.Error)),
        new("statements.md", "Module-Level Declaration Rules", "module-level typed constant",
            new Y("GenImports.ImportingModule", () => GenImports.ImportingModule(TypeEnv.Default, fuel: 1), n => n is Module m && m.Body.OfType<VariableDeclaration>().Any()),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // static_methods.md
        new("static_methods.md", "Static Methods", "self-less (static) methods",
            new KnownRedRow("#2287", "no typed generator emits a static method"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // string_literals.md
        new("string_literals.md", "String Literals", "string literals",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is StringLiteral),
            new ByDesign("no typing surface")),

        // string_operators.md
        new("string_operators.md", "String Operators", "str + str",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is BinaryOp { Operator: BinaryOperator.Add } b
                && (b.Left is StringLiteral or Identifier { Name: "s" } || b.Right is StringLiteral or Identifier { Name: "s" }), Samples: 100),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // string_type.md
        new("string_type.md", "String Method Availability", "str.upper() / lower() / strip()",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is FunctionCall { Function: MemberAccess { Member: "upper" or "lower" or "strip" } }, Samples: 100),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),

        // structs.md
        new("structs.md", "Structs", "structs",
            new KnownRedRow("#2287", "no typed generator emits a struct"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // tagged_unions*.md
        new("tagged_unions.md", "Tagged Unions (Algebraic Data Types)", "user tagged unions",
            new KnownRedRow("#2287", "no typed generator emits a union"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),
        new("tagged_unions_optional.md", "Creating Optional Values", "None() into an Optional slot",
            new Y("GenTyped.TypedProgram", () => TypedOptionals(), n => n is FunctionCall { Function: NoneLiteral }),
            new YInjected("ErrorInjector.InjectOptionalOperandWithoutNarrowing", "SPY0222", CompilerDiagnosticSeverity.Error)),
        new("tagged_unions_optional.md", "Pattern Matching", "Some / None patterns",
            new KnownRedRow("#2287", "no typed generator matches on an Optional"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),
        new("tagged_unions_result.md", "Result Type", "Result[T, E]",
            new KnownRedRow("#2287", "no typed generator emits a Result"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // template_strings.md
        new("template_strings.md", "Template Strings (T-Strings)", "t-strings",
            new KnownRedRow("#2287", "no typed generator emits a t-string"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // try_expressions.md
        new("try_expressions.md", "Try expressions", "try expressions",
            new KnownRedRow("#2287", "no typed generator emits a try expression"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // tuple_unpacking.md
        new("tuple_unpacking.md", "Tuple Unpacking", "tuple unpacking",
            new KnownRedRow("#2287", "no typed generator emits an unpacking target"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // type_aliases.md
        new("type_aliases.md", "Type Aliases", "type aliases",
            new KnownRedRow("#2287", "no typed generator emits a type alias"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // type_annotation_shorthand.md
        new("type_annotation_shorthand.md", "List Shorthand", "[T] shorthand annotations",
            new KnownRedRow("#2287", "no typed generator emits a shorthand annotation"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // type_annotations.md
        new("type_annotations.md", "Type Annotations", "list[int] annotation",
            new Y("GenTyped.TypedProgram", () => TypedCollections(), n => n is VariableDeclaration { Type: { Name: "list", TypeArguments.Length: 1 } }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("type_annotations.md", "Nullability and Optional Type Syntax", "T? annotation",
            new Y("GenTyped.TypedProgram", () => TypedOptionals(), n => n is VariableDeclaration { Type.IsOptional: true }),
            new YInjected("ErrorInjector.InjectOptionalStoreWithoutNarrowing", "SPY0220", CompilerDiagnosticSeverity.Error)),

        // type_casting.md
        new("type_casting.md", "Type Casting (the `as!` / `as?` Operators)", "as! / as?",
            new KnownRedRow("#2287", "no typed generator emits a cast"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // type_hierarchy.md
        new("type_hierarchy.md", "Type Hierarchy and Object Model", "object as the universal base",
            new KnownRedRow("#2287", "no typed generator emits an object-typed slot"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // type_narrowing.md
        new("type_narrowing.md", "Narrowing Rules", "is-None narrowing",
            new Y("GenTyped.TypedProgram", () => TypedOptionalStatements(), n => n is IfStatement
            {
                Test: BinaryOp { Operator: BinaryOperator.IsNot, Left: Identifier, Right: NoneLiteral },
                ElseBody.Length: 0
            }),
            new YInjected("ErrorInjector.InjectUseInFailedNarrowingBranch", "SPY0220", CompilerDiagnosticSeverity.Error)),
        new("type_narrowing.md", "Narrowing Rules", "isinstance narrowing",
            new KnownRedRow("#2287", "no typed generator emits isinstance"),
            new YInjected("ErrorInjector.InjectIsinstanceElseBranchMember", "SPY0203", CompilerDiagnosticSeverity.Error)),

        // type_parameter_defaults.md
        new("type_parameter_defaults.md", "Type Parameter Defaults", "type-parameter defaults",
            new KnownRedRow("#2287", "no typed generator emits a type-parameter default"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // typing_equivalences.md
        new("typing_equivalences.md", "Typing Module Equivalences", "typing-module equivalents",
            new KnownRedRow("#2287", "no typed generator emits a typing-module form"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // unittest.md
        new("unittest.md", "`@test` Decorator", "@test functions",
            new KnownRedRow("#2287", "no typed generator emits @test"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // variable_declaration.md
        new("variable_declaration.md", "Variable Declaration and Assignment", "typed local x: T = value",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is VariableDeclaration { Type: not null, InitialValue: not null }),
            new YInjected("ErrorInjector.InjectTypeMismatchAssignment", "SPY0220", CompilerDiagnosticSeverity.Error)),

        // variable_scoping.md
        new("variable_scoping.md", "Assignment Statement", "reassignment of a declared local",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is Assignment { Target: Identifier }),
            new KnownRedRow("#2324", "generated; no injection breaks it — outside Phase 4's eight lanes")),
        new("variable_scoping.md", "Variable Shadowing", "same-scope re-let",
            new KnownRedRow("#2287", "no typed generator emits let"),
            new ByDesign("same-scope re-let is valid by spec (let always introduces a new variable); control: `def main(): let x = 1; let x = 2; print(x)` runs and prints 2 @ f8c37f986")),

        // walrus_operator.md
        new("walrus_operator.md", "Walrus Operator", ":=",
            new KnownRedRow("#2287", "no typed generator emits :="),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // while_statement.md
        new("while_statement.md", "While Loop", "while loop",
            new KnownRedRow("#2287", "no typed generator emits while"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),
    };

    // ---- The spec universe -------------------------------------------------------------------

    private static string SpecDir => Path.Combine(FixtureRoots.RepositoryRoot, "docs", "language_specification");

    private static IReadOnlyList<string> UniverseFiles()
    {
        var files = Directory.EnumerateFiles(SpecDir, "*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(f => f != "README.md")
            .Select(f => f!)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        Assert.True(files.Count >= 100,
            $"Only {files.Count} spec files under {SpecDir} — a degraded path would make the roster pass vacuously");
        return files;
    }

    private static readonly Regex SharpyFence = new(@"^```(python|spy|sharpy)\s*$");
    private static readonly Regex FenceClose = new(@"^```\s*$");
    private static readonly Regex ExemptMarker = new(@"<!--\s*spec-sweep:\s*(fragment|error\s+SPY\d{4})\s*-->");

    /// <summary>The <c># </c> and <c>## </c> heading texts of a spec file, outside code fences.</summary>
    private static HashSet<string> Headings(string file)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var inFence = false;
        foreach (var line in File.ReadLines(Path.Combine(SpecDir, file)))
        {
            if (inFence)
            {
                if (FenceClose.IsMatch(line))
                    inFence = false;
                continue;
            }
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = true;
                continue;
            }
            if (line.StartsWith("## ", StringComparison.Ordinal))
                result.Add(line.Substring(3).TrimEnd());
            else if (line.StartsWith("# ", StringComparison.Ordinal))
                result.Add(line.Substring(2).TrimEnd());
        }
        return result;
    }

    /// <summary>Line numbers of the fenced Sharpy blocks that build_tools/spec_blocks.py would compile
    /// (no fragment/error marker on the line above the fence).</summary>
    private static List<int> CompiledBlockLines(string file)
    {
        var lines = File.ReadAllLines(Path.Combine(SpecDir, file));
        var result = new List<int>();
        var inFence = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (inFence)
            {
                if (FenceClose.IsMatch(lines[i]))
                    inFence = false;
                continue;
            }
            if (!lines[i].StartsWith("```", StringComparison.Ordinal))
                continue;
            inFence = true;
            if (SharpyFence.IsMatch(lines[i]) && (i == 0 || !ExemptMarker.IsMatch(lines[i - 1])))
                result.Add(i + 1);
        }
        return result;
    }

    // ---- Facts -------------------------------------------------------------------------------

    [Fact]
    public void Universe_EverySpecFileHasARow_AndEveryRowNamesASpecFile()
    {
        var universe = UniverseFiles();
        var rowed = Rows.Select(r => r.File).ToHashSet(StringComparer.Ordinal);

        var unrowed = universe.Where(f => !rowed.Contains(f)).ToList();
        var stale = rowed.Where(f => !universe.Contains(f)).OrderBy(f => f, StringComparer.Ordinal).ToList();

        Assert.True(unrowed.Count == 0,
            $"{unrowed.Count} spec file(s) have no roster row — add a construct row (Y, N citing #2287, ByDesign) "
            + "or, if the file has no compiled example, a NoConstruct row:\n  " + string.Join("\n  ", unrowed));
        Assert.True(stale.Count == 0,
            $"{stale.Count} roster row file(s) are not in docs/language_specification/:\n  " + string.Join("\n  ", stale));
    }

    [Fact]
    public void Anchors_EveryRowAnchorIsAHeadingOfItsFile()
    {
        var universe = UniverseFiles().ToHashSet(StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (var group in Rows.Where(r => universe.Contains(r.File)).GroupBy(r => r.File))
        {
            var headings = Headings(group.Key);
            foreach (var row in group.Where(r => !headings.Contains(r.Anchor)))
                missing.Add($"{row.File}: no '# {row.Anchor}' or '## {row.Anchor}' heading (row '{row.Construct}')");
        }
        Assert.True(missing.Count == 0,
            $"{missing.Count} roster anchor(s) are stale:\n  " + string.Join("\n  ", missing));
    }

    private static readonly Regex IssueCite = new(@"^#\d+$");
    private static readonly Regex DiagnosticCode = new(@"^SPY\d{4}$");

    /// <summary>Every lane is well formed: members resolve by reflection to a public static method on a
    /// <c>Generators.Typed</c> type, codes are literal SPY codes, cites are <c>#N</c>, reasons and
    /// observations are non-empty, and a <see cref="NoConstruct"/> row is a file's only row and names a
    /// file with no compiled Sharpy block.</summary>
    [Fact]
    public void Rows_AreWellFormed()
    {
        var generatorTypes = typeof(GenTyped).Assembly.GetTypes()
            .Where(t => t.Namespace == GeneratorsNamespace && !t.IsNested && !t.Name.StartsWith("<", StringComparison.Ordinal))
            .ToDictionary(t => t.Name, StringComparer.Ordinal);
        var problems = new List<string>();

        void CheckMember(RosterRow row, string lane, string member)
        {
            var parts = member.Split('.');
            if (parts.Length != 2
                || !generatorTypes.TryGetValue(parts[0], out var type)
                || type.GetMethods(BindingFlags.Public | BindingFlags.Static).All(m => m.Name != parts[1]))
            {
                problems.Add($"{row.File} '{row.Construct}' {lane}: '{member}' is not a public static method on a type in {GeneratorsNamespace}");
            }
        }

        void CheckLane(RosterRow row, string laneName, Lane lane)
        {
            switch (lane)
            {
                case Y y:
                    CheckMember(row, laneName, y.Member);
                    if (laneName != "Generated")
                        problems.Add($"{row.File} '{row.Construct}': a Y lane belongs in Generated");
                    break;
                case YInjected yi:
                    CheckMember(row, laneName, yi.Member);
                    if (!DiagnosticCode.IsMatch(yi.Code))
                        problems.Add($"{row.File} '{row.Construct}': '{yi.Code}' is not a literal SPY code");
                    if (laneName != "Injected")
                        problems.Add($"{row.File} '{row.Construct}': a YInjected lane belongs in Injected");
                    break;
                case KnownRedRow kr:
                    if (!IssueCite.IsMatch(kr.Issue))
                        problems.Add($"{row.File} '{row.Construct}' {laneName}: issue '{kr.Issue}' is not #N");
                    if (string.IsNullOrWhiteSpace(kr.Observed))
                        problems.Add($"{row.File} '{row.Construct}' {laneName}: a KnownRedRow must say what is observed");
                    break;
                case ByDesign bd:
                    if (string.IsNullOrWhiteSpace(bd.Reason))
                        problems.Add($"{row.File} '{row.Construct}' {laneName}: a ByDesign lane must give its reason");
                    break;
                case NoConstruct nc:
                    if (string.IsNullOrWhiteSpace(nc.Reason))
                        problems.Add($"{row.File} {laneName}: a NoConstruct row must give its reason");
                    break;
                default:
                    problems.Add($"{row.File} '{row.Construct}' {laneName}: unknown lane {lane?.GetType().Name ?? "null"}");
                    break;
            }
        }

        foreach (var row in Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Construct))
                problems.Add($"{row.File} '{row.Anchor}': empty construct");
            CheckLane(row, "Generated", row.Generated);
            CheckLane(row, "Injected", row.Injected);
            if ((row.Generated is NoConstruct) != (row.Injected is NoConstruct))
                problems.Add($"{row.File}: NoConstruct must cover both lanes");
        }

        var universe = UniverseFiles().ToHashSet(StringComparer.Ordinal);
        foreach (var group in Rows.GroupBy(r => r.File))
        {
            if (!group.Any(r => r.Generated is NoConstruct))
                continue;
            if (group.Count() > 1)
                problems.Add($"{group.Key}: a NoConstruct row must be the file's only row");
            if (!universe.Contains(group.Key))
                continue;
            var compiled = CompiledBlockLines(group.Key);
            if (compiled.Count > 0)
                problems.Add($"{group.Key}: NoConstruct, but the file has {compiled.Count} compiled Sharpy block(s) "
                    + $"(fence line(s) {string.Join(", ", compiled)}) — a compiled example defines a construct; row it");
        }

        Assert.True(problems.Count == 0,
            $"{problems.Count} malformed roster lane(s):\n  " + string.Join("\n  ", problems));
    }

    // ---- RAW positive control (Task 3) -------------------------------------------------------

    private const ulong RawSeed = 2288;

    // The compiler-side defects the raw corpus draws are pinned ONCE, in KnownGeneratorPins (shared with
    // the generated lane's RejectionSink, #2288): a sample is tolerated (excluded from the corpus, counted,
    // printed) only when its PRE-unparse AST has the pinned shape AND every re-parse error carries the
    // pin's code; any other parse error stays red. ParsePins_StillFire requires every pin to tolerate at
    // least one sample of the fixed-seed corpus, so a pin drains with its fix instead of going stale.

    private sealed record RawControlResult(int RowsChecked, IReadOnlyList<string> Failures,
        IReadOnlyDictionary<KnownGeneratorPins.ParsePin, int> PinHits, IReadOnlyList<(string Row, int Hits)> RowHits, string CorpusHash, TimeSpan Elapsed);

    private static readonly Lazy<RawControlResult> RawControl = new(RunRawControl);

    private static RawControlResult RunRawControl()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var failures = new List<string>();
        var pinHits = KnownGeneratorPins.ParsePins.ToDictionary(p => p, _ => 0);
        using var corpusHash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        int rowsChecked = 0;
        var rowHits = new List<(string Row, int Hits)>();

        foreach (var row in Rows)
        {
            if (row.Generated is not Y y)
                continue;
            rowsChecked++;
            List<Module> corpus;
            try
            {
                corpus = RawCorpus(y.Generator(), y.Samples, pinHits, corpusHash);
            }
            catch (RawSampleException e)
            {
                failures.Add($"{row.File} '{row.Construct}' ({y.Member}): {e.Message}");
                continue;
            }
            catch (Exception e)
            {
                failures.Add($"{row.File} '{row.Construct}' ({y.Member}): generator threw {Unwrap(e)}");
                continue;
            }

            var hits = corpus.Sum(m => Infrastructure.AstDescendants.SelfAndDescendants(m).Count(y.Predicate));
            rowHits.Add(($"{row.File} '{row.Construct}'", hits));
            if (hits == 0)
            {
                failures.Add($"{row.File} '{row.Construct}': {y.Member} drew {y.Samples} raw samples and none "
                    + "contains the construct — a Y whose generator never emits it is not Y");
            }
        }

        clock.Stop();
        return new RawControlResult(rowsChecked, failures, pinHits, rowHits,
            Convert.ToHexString(corpusHash.GetHashAndReset())[..16], clock.Elapsed);
    }

    private void PrintRawControl(RawControlResult result)
    {
        _output.WriteLine($"RAW control: {result.RowsChecked} Y-generated rows x {RawSamples} samples, more where a row raises it (seed {RawSeed}) "
            + $"in {result.Elapsed.TotalSeconds:F1} s; corpus sha256 {result.CorpusHash} (equal across processes iff the generators are reproducible)");
        foreach (var (pin, hits) in result.PinHits)
            _output.WriteLine($"  parse pin {pin.Pin.Issue} ({pin.Code}): {hits} sample source(s) tolerated");
        // The corpus is a pure function of the seed (TypeEnv's maps are ordinal-sorted), so these counts
        // are stable across processes; the thinnest rows show which predicates sit closest to zero.
        foreach (var (row, hits) in result.RowHits.OrderBy(r => r.Hits).Take(8))
            _output.WriteLine($"  thinnest: {hits,4} hit(s)  {row}");
    }

    /// <summary>Every Y-generated row: 40 samples of the UNFILTERED member at a fixed seed, unparsed
    /// and re-parsed by the real parser, contain its construct. A throw in the generator, the unparser
    /// or the parser, or an unpinned parse error, is red with the sample's source.</summary>
    [Fact]
    public void YGenerated_RawCorpusContainsTheConstruct()
    {
        var result = RawControl.Value;
        PrintRawControl(result);
        Assert.True(result.RowsChecked > 0, "no Y-generated row was checked — the control is vacuous");
        Assert.True(result.Failures.Count == 0,
            $"{result.Failures.Count} Y-generated row(s) fail the raw control:\n  " + string.Join("\n  ", result.Failures));
    }

    /// <summary>Every parse pin still tolerates at least one sample: when the pinned defect is fixed the
    /// pin goes red here and is dropped, never left behind as a silent exemption.</summary>
    [Fact]
    public void ParsePins_StillFire()
    {
        var result = RawControl.Value;
        PrintRawControl(result);
        var stale = result.PinHits.Where(kv => kv.Value == 0)
            .Select(kv => $"{kv.Key.Pin.Issue} ({kv.Key.Code}): {kv.Key.Pin.Observed}")
            .ToList();
        Assert.True(stale.Count == 0,
            "parse pin(s) tolerated no sample of the fixed-seed corpus — the defect is fixed; drop the pin:\n  "
            + string.Join("\n  ", stale));
    }

    private sealed class RawSampleException(string message) : Exception(message);

    private static string Unwrap(Exception e) =>
        e is TargetInvocationException { InnerException: { } inner } ? Unwrap(inner) : $"{e.GetType().Name}: {e.Message}";

    /// <summary>Draws <see cref="RawSamples"/> values from a boxed <c>Gen&lt;T&gt;</c> and re-parses every
    /// <see cref="Node"/> / <c>string</c> it carries (a tuple's items each).</summary>
    private static List<Module> RawCorpus(object gen, int count, Dictionary<KnownGeneratorPins.ParsePin, int> pinHits,
        System.Security.Cryptography.IncrementalHash corpusHash)
    {
        var elementType = GenElementType(gen.GetType())
            ?? throw new RawSampleException($"generator is {gen.GetType().Name}, not a CsCheck Gen<T>");
        var draw = typeof(TypedGeneratorRosterTests)
            .GetMethod(nameof(Draw), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(elementType);
        var samples = (List<object>)draw.Invoke(null, new object[] { gen, count })!;

        var modules = new List<Module>();
        foreach (var sample in samples)
        {
            foreach (var (source, origin) in SourcesOf(sample))
            {
                corpusHash.AppendData(System.Text.Encoding.UTF8.GetBytes(source + "\u0000"));
                if (Reparse(source, origin, pinHits) is { } module)
                    modules.Add(module);
            }
        }
        return modules;
    }

    private static Type? GenElementType(Type? type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Gen<>))
                return t.GetGenericArguments()[0];
        }
        return null;
    }

    private static List<object> Draw<T>(Gen<T> gen, int count)
    {
        var pcg = new PCG(1u, RawSeed);
        var samples = new List<object>(count);
        for (int i = 0; i < count; i++)
            samples.Add(gen.Generate(pcg, null, out _)!);
        return samples;
    }

    /// <summary>The source texts a sample carries, each with the AST it was unparsed from (null for a
    /// source-text generator).</summary>
    private static IEnumerable<(string Source, Node? Origin)> SourcesOf(object sample)
    {
        switch (sample)
        {
            case string s:
                yield return (s, null);
                break;
            case Node node:
                string text;
                try
                {
                    text = node is Module m
                        ? Sharpy.Compiler.Pretty.Unparser.Unparse(m)
                        : Sharpy.Compiler.Pretty.Unparser.Unparse(node);
                }
                catch (Exception e)
                {
                    throw new RawSampleException($"Unparser threw {Unwrap(e)} on a {node.GetType().Name}");
                }
                yield return (text, node);
                break;
            case System.Runtime.CompilerServices.ITuple tuple:
                for (int i = 0; i < tuple.Length; i++)
                {
                    if (tuple[i] is string or Node)
                    {
                        foreach (var item in SourcesOf(tuple[i]!))
                            yield return item;
                    }
                }
                break;
            default:
                throw new RawSampleException($"sample of type {sample.GetType().Name} carries no Node or source text");
        }
    }

    private static Module? Reparse(string source, Node? origin, Dictionary<KnownGeneratorPins.ParsePin, int> pinHits)
    {
        try
        {
            var parser = new Sharpy.Compiler.Parser.Parser(new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll());
            var module = parser.ParseModule();
            if (parser.Diagnostics.HasErrors)
            {
                var errors = parser.Diagnostics.GetErrors().ToList();
                var pin = origin == null ? null : KnownGeneratorPins.Match(origin, errors);
                if (pin != null)
                {
                    pinHits[pin]++;
                    return null;
                }
                throw new RawSampleException("the generated source does not parse ("
                    + string.Join("; ", errors.Select(d => $"{d.Code}: {d.Message}"))
                    + "):\n" + source);
            }
            return module;
        }
        catch (RawSampleException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new RawSampleException($"the parser threw {Unwrap(e)} on:\n{source}");
        }
    }

    // ---- Totality (Task 4) -------------------------------------------------------------------

    // Counted at landing and bumped in the same commit as the row that moves them. N = lanes citing the
    // generator trackers (#2287 P62, #2288 this plan, #2324 injection reach); KnownRed = lanes pinning a
    // compiler cell to its tracker; ByDesign and the Y counts are lanes; NoConstruct counts rows.
    private const int ExpectedRows = 202;
    private const int ExpectedYGenerated = 102;
    private const int ExpectedYInjected = 29;
    private const int ExpectedN = 242;
    private const int ExpectedKnownRed = 2;
    private const int ExpectedByDesign = 21;
    private const int ExpectedNoConstruct = 4;

    private static readonly HashSet<string> GeneratorTrackers = new(StringComparer.Ordinal) { "#2287", "#2288", "#2324" };

    [Fact]
    public void Totality_CountsMatchTheLiteralAnchors()
    {
        var lanes = Rows.SelectMany(r => new[] { r.Generated, r.Injected }).ToList();
        int rows = Rows.Length;
        int yGenerated = Rows.Count(r => r.Generated is Y);
        int yInjected = Rows.Count(r => r.Injected is YInjected);
        int n = lanes.Count(l => l is KnownRedRow k && GeneratorTrackers.Contains(k.Issue));
        int knownRed = lanes.Count(l => l is KnownRedRow k && !GeneratorTrackers.Contains(k.Issue));
        int byDesign = lanes.Count(l => l is ByDesign);
        int noConstruct = Rows.Count(r => r.Generated is NoConstruct);

        _output.WriteLine($"rows total/Y-gen/Y-inj/N/ByDesign/KnownRed = {rows}/{yGenerated}/{yInjected}/{n}/{byDesign}/{knownRed} "
            + $"(NoConstruct rows {noConstruct}; spec files {UniverseFiles().Count}) (measured)");

        Assert.Equal(ExpectedRows, rows);
        Assert.Equal(ExpectedYGenerated, yGenerated);
        Assert.Equal(ExpectedYInjected, yInjected);
        Assert.Equal(ExpectedN, n);
        Assert.Equal(ExpectedKnownRed, knownRed);
        Assert.Equal(ExpectedByDesign, byDesign);
        Assert.Equal(ExpectedNoConstruct, noConstruct);
    }
}
