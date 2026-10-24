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
/// ("Phase 4 adds the injection"); a weak refusal lane Phase 2 re-judges cites #2288; the 62 constructs that
/// were generated, had a refusal and had no injection cited #2324 until P63b's mutation lanes
/// (<c>ErrorInjector.Mutations.cs</c> over <c>Generators/Typed/Mutations/</c>) drained them by measurement
/// (2026-10-24) — to <see cref="YInjected"/>, to a compiler pin, or to <see cref="ByDesign"/> with its spec line
/// and census control; compiler cells pinned by an injection cite their tracker (#2143, #2335, #2021, #2363). A construct with no refusal surface is <c>ByDesign("no typing surface")</c>; same-scope
/// re-<c>let</c> is <see cref="ByDesign"/> by <c>variable_scoping.md</c> §Variable Shadowing.</item>
/// <item><b>NoConstruct exemption</b> — allowed only for a file with NO fenced Sharpy block
/// (<c>```python</c>/<c>spy</c>/<c>sharpy</c>, the fences <c>build_tools/spec_blocks.py</c> reads), whatever
/// its marker: an example — compiled, fragment or an error-marked refusal — shows a construct, so the
/// exemption cannot be its own subject (#2324: compiled-only admitted a file of refusal examples).</item>
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
    /// <c>GenTyped.NarrowedBlock</c> (statements, an env with Optionals) — at the fuel it samples (2; this
    /// helper said 1, a configuration no acceptance property sampled — found by the argument-identity link,
    /// #2324).</summary>
    private static Gen<Module> TypedOptionalStatements() =>
        Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
            GenTyped.TypedProgram(TypeEnv.WithOptionals, type, fuel: 2, withStatements: true));

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
            new YInjected("ErrorInjector.InjectWrongTypedArithmeticOperand", "SPY0222", CompilerDiagnosticSeverity.Error)),
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
            new YInjected("ErrorInjector.InjectOverloadedBuiltinWrongArgument", "SPY0354", CompilerDiagnosticSeverity.Error)),
        new("builtin_functions.md", "Mathematical Functions", "abs()",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => CallTo(n, "abs")),
            new YInjected("ErrorInjector.InjectOverloadedBuiltinWrongArgument", "SPY0354", CompilerDiagnosticSeverity.Error)),
        new("builtin_functions.md", "Type Conversion", "str() / int() conversion",
            new Y("GenFStrings.FStringWithNestedCalls", () => GenFStrings.FStringWithNestedCalls(), n => CallTo(n, "str", "int")),
            new YInjected("ErrorInjector.InjectOverloadedBuiltinWrongArgument", "SPY0354", CompilerDiagnosticSeverity.Error)),
        new("builtin_functions.md", "Type Checking", "isinstance()",
            new KnownRedRow("#2287", "no typed generator emits isinstance"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // class_methods.md
        NoConstructRow("class_methods.md", "Class Methods",
            "states that @classmethod does not exist in Sharpy; no example block"),

        // classes.md
        new("classes.md", "Basic Class Definition", "class with typed fields, __init__ and methods",
            new Y("GenClasses.ModuleWithClasses", () => Classes(), n => n is ClassDef c && c.Body.OfType<VariableDeclaration>().Any(v => v.Type != null)),
            new YInjected("ErrorInjector.InjectUndefinedField", "SPY0203", CompilerDiagnosticSeverity.Error)),
        new("classes.md", "Field Initializer Expressions", "field with an initializer in the class body",
            new KnownRedRow("#2287", "no typed generator emits a field initializer"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // collection_types.md
        new("collection_types.md", "Collection Types", "list literal",
            new Y("GenTyped.TypedProgram", () => TypedCollections(), n => n is ListLiteral),
            new YInjected("ErrorInjector.InjectWrongTypedListInitializer", "SPY0220", CompilerDiagnosticSeverity.Error)),
        new("collection_types.md", "Collection Types", "dict and set literals",
            new KnownRedRow("#2287", "no typed generator emits a dict or set literal"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),
        new("collection_types.md", "Collection Types", "tuple literal and tuple[...] annotation",
            new Y("GenMatchPatterns.MatchWithNestedPatterns", () => GenMatchPatterns.MatchWithNestedPatterns(), n => n is TupleLiteral),
            new KnownRedRow("#2287", "the file refuses SPY0149 (a `tuple[T, ...]` annotation), a parser refusal no typed generator draws — was ByDesign, wrong by the file's prose (P63b spec-code ratchet)")),
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
            new YInjected("ErrorInjector.InjectWrongTypedComparisonOperand", "SPY0222", CompilerDiagnosticSeverity.Error)),

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
            new YInjected("ErrorInjector.InjectUndefinedNameAtWithTarget", "SPY0200", CompilerDiagnosticSeverity.Error)),
        new("context_managers.md", "Async Context Managers", "async with over __aenter__/__aexit__",
            new Y("GenContextManagers.AsyncContextManagerProgram", () => GenContextManagers.AsyncContextManagerProgram(), n => n is WithStatement { IsAsync: true }),
            // declaration shape — a hand lane (DeclarationShapes), the axis the site enumerator cannot list
            new YInjected("ErrorInjector.InjectAsyncWithOverSyncManager", "SPY0324", CompilerDiagnosticSeverity.Error)),
        new("context_managers.md", "`__exit__` Signature Variants", "zero-argument __exit__(self)",
            new Y("GenContextManagers.ValidContextManagerProgram", () => GenContextManagers.ValidContextManagerProgram(), n => n is FunctionDef { Name: "__exit__", Parameters.Length: 1 }),
            // declaration shape — a hand lane (DeclarationShapes), the axis the site enumerator cannot list
            new YInjected("ErrorInjector.InjectExitArity", "SPY0320", CompilerDiagnosticSeverity.Error)),

        // conventions.md
        new("conventions.md", "Function Return Type Annotations", "omitted return annotation (implicit -> None)",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is FunctionDef { Name: "main", ReturnType: null }),
            new YInjected("ErrorInjector.InjectReturnValueInImplicitNoneDef", "SPY0260", CompilerDiagnosticSeverity.Error)),

        // conversion_operators.md
        new("conversion_operators.md", "Conversion Operators", "__implicit__ / __explicit__",
            new KnownRedRow("#2287", "no typed generator emits a conversion operator"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // dataclass.md
        new("dataclass.md", "Basic Usage", "@dataclass class constructed positionally",
            new Y("GenDecorators.ModuleWithDataclass", () => GenDecorators.ModuleWithDataclass(), n => Decorated(n, "dataclass")),
            new KnownRedRow("#2021", "analyses clean; run is SPY0908 CS1503 — the @dataclass constructor has no semantic signature (ErrorInjector.InjectDataclassWrongArgument)")),
        new("dataclass.md", "Field Defaults", "dataclass field defaults",
            new KnownRedRow("#2287", "no typed generator emits a dataclass field default"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // decorators.md
        new("decorators.md", "Method Modifiers", "@virtual base method and @override in the derived class",
            new Y("GenClasses.ModuleWithClasses", () => Classes(), n => Decorated(n, "override")),
            new KnownRedRow("#2143", "analyses clean; `run` is SPY0908 CS0508 — no override signature check for a Sharpy base")),
        new("decorators.md", "Method Modifiers", "@override @final",
            new Y("GenDecorators.ModuleWithDecoratedFunction", () => GenDecorators.ModuleWithDecoratedFunction(validDecorator: true), n => Decorated(n, "final")),
            // declaration shape — a hand lane (DeclarationShapes), the axis the site enumerator cannot list
            new YInjected("ErrorInjector.InjectOverrideWithoutBaseMember", "SPY0248", CompilerDiagnosticSeverity.Error)),
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
            new YInjected("ErrorInjector.InjectDunderCalledByName", "SPY0427", CompilerDiagnosticSeverity.Error)),

        // dunder_methods.md
        new("dunder_methods.md", "Arithmetic Operators", "arithmetic and unary dunder definitions",
            new Y("GenOperators.BinaryOperatorProgram", () => GenOperators.BinaryOperatorProgram(), n => DefNamed(n, "__add__", "__sub__", "__mul__", "__mod__")),
            new YInjected("GenOperators.WrongArityOperatorDunderProgram", "SPY0321", CompilerDiagnosticSeverity.Error)),
        new("dunder_methods.md", "Comparison Operators", "comparison dunder definitions",
            new Y("GenOperators.ComparisonOperatorProgram", () => GenOperators.ComparisonOperatorProgram(), n => DefNamed(n, "__lt__", "__le__", "__gt__", "__ge__", "__eq__", "__ne__")),
            // declaration shape — a hand lane (DeclarationShapes), the axis the site enumerator cannot list
            new YInjected("ErrorInjector.InjectDunderWrongReturnType", "SPY0321", CompilerDiagnosticSeverity.Error)),
        new("dunder_methods.md", "Special Methods", "__len__ / __bool__ protocol synthesis",
            new Y("GenInterfaces.ModuleWithProtocolDunder", () => Gen.OneOfConst("__len__", "__bool__").SelectMany(GenInterfaces.ModuleWithProtocolDunder), n => DefNamed(n, "__len__", "__bool__")),
            new YInjected("ErrorInjector.InjectProtocolDunderRemoved", "SPY0320", CompilerDiagnosticSeverity.Error)),
        new("dunder_methods.md", "Special Methods", "__iter__ / __next__ iterator protocol",
            new Y("GenIterators.ModuleWithIterProtocol", () => GenIterators.ModuleWithIterProtocol(complete: true), n => DefNamed(n, "__next__")),
            new YInjected("GenIterators.ModuleWithIterProtocol", "SPY0320", CompilerDiagnosticSeverity.Error)),

        // dunder_methods_recommendations.md
        new("dunder_methods_recommendations.md", "1. Equality Contract (`__eq__` / `__hash__`)", "user __eq__",
            new Y("GenOperators.ComparisonOperatorProgram", () => GenOperators.ComparisonOperatorProgram(), n => DefNamed(n, "__eq__")),
            // declaration shape — a hand lane (DeclarationShapes), the axis the site enumerator cannot list
            new YInjected("ErrorInjector.InjectDunderWrongReturnType", "SPY0321", CompilerDiagnosticSeverity.Error)),

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
            // declaration shape — a hand lane (DeclarationShapes), the axis the site enumerator cannot list
            new YInjected("ErrorInjector.InjectHandlerTypeNotException", "SPY0399", CompilerDiagnosticSeverity.Error)),
        new("exception_handling.md", "Try/Except/Finally", "nested try",
            new Y("GenExceptions.ModuleWithNestedTry", () => GenExceptions.ModuleWithNestedTry(), n => n is TryStatement t && t.Body.Any(s => s is TryStatement)),
            new ByDesign("no typing surface")),
        new("exception_handling.md", "Raise Statement", "raise of a constructed exception",
            new Y("GenExceptions.ModuleWithRaiseExpression", () => GenExceptions.ModuleWithRaiseExpression(), n => n is RaiseStatement { Exception: not null }),
            // declaration shape — a hand lane (DeclarationShapes), the axis the site enumerator cannot list
            new YInjected("ErrorInjector.InjectRaiseNonException", "SPY0242", CompilerDiagnosticSeverity.Error)),
        new("exception_handling.md", "Raise Statement", "bare raise inside an except block",
            new Y("GenExceptions.ModuleWithBareRaise", () => GenExceptions.ModuleWithBareRaise(insideExcept: true), n => n is RaiseStatement { Exception: null }),
            new YInjected("GenExceptions.ModuleWithBareRaise", "SPY0242", CompilerDiagnosticSeverity.Error)),
        new("exception_handling.md", "Exception Type Hierarchy", "subclass handler before its base",
            new Y("GenExceptions.ModuleWithExceptionHierarchy", () => GenExceptions.ModuleWithExceptionHierarchy(), n => n is TryStatement { Handlers.Length: >= 2 }),
            // declaration shape — a hand lane (DeclarationShapes), the axis the site enumerator cannot list
            new KnownRedRow("#2363", "analyses clean; run is SPY0908 CS0160 — handler reachability is left to Roslyn (ErrorInjector.InjectHandlerOrderBaseFirst)")),
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
            new YInjected("ErrorInjector.InjectMismatchedConditionalBranch", "SPY0220", CompilerDiagnosticSeverity.Error)),
        new("expressions.md", "Member Access", "obj.member",
            new Y("GenClasses.ModuleWithClasses", () => Classes(), n => n is MemberAccess),
            new YInjected("ErrorInjector.InjectUndefinedMember", "SPY0203", CompilerDiagnosticSeverity.Error)),
        new("expressions.md", "Function Calls", "call to a user function: argument count",
            new Y("GenFunctions.ModuleWithFunctions", () => Functions(), n => CallTo(n, "compute", "process", "transform")),
            new YInjected("ErrorInjector.InjectWrongFunctionArgCount", "SPY0224", CompilerDiagnosticSeverity.Error)),
        new("expressions.md", "Function Calls", "call to an undefined function",
            new Y("GenFunctions.ModuleWithFunctions", () => Functions(), n => CallTo(n, "compute", "process", "transform")),
            new YInjected("ErrorInjector.InjectUndefinedFunctionCall", "SPY0200", CompilerDiagnosticSeverity.Error)),
        new("expressions.md", "Index Access", "list[index]",
            new Y("GenTyped.ListSubscript", () => Gen.OneOfConst("int", "str").SelectMany(element => GenTyped.ListSubscript(TypeEnv.WithCollections, element)), n => n is IndexAccess),
            new YInjected("ErrorInjector.InjectWrongTypedIndex", "SPY0220", CompilerDiagnosticSeverity.Error)),

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
            new KnownRedRow("#2324", "the file refuses SPY0210 (a float literal out of its type's range) and SPY0220 (a literal outside a narrower slot's range); generated, no injected lane breaks them — was ByDesign, wrong by the file's prose (P63b spec-code ratchet)")),

        // for_statement.md
        new("for_statement.md", "For Loop", "for i in range(n)",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is ForStatement),
            new YInjected("ErrorInjector.InjectOverloadedBuiltinWrongArgument", "SPY0354", CompilerDiagnosticSeverity.Error)),

        // fstrings.md
        new("fstrings.md", "F-Strings (Formatted String Literals)", "f-string with an expression hole",
            new Y("GenFStrings.FStringWithArithmetic", () => GenFStrings.FStringWithArithmetic(), n => n is FStringLiteral f && f.Parts.Any(p => p.Expression is BinaryOp)),
            new YInjected("ErrorInjector.InjectUndefinedNameInFStringHole", "SPY0200", CompilerDiagnosticSeverity.Error)),
        new("fstrings.md", "Format Spec Grammar", "format spec in a hole",
            new Y("GenFStrings.FStringWithFormatSpecs", () => GenFStrings.FStringWithFormatSpecs(), n => n is FStringLiteral f && f.Parts.Any(p => p.Spec != null)),
            new YInjected("ErrorInjector.InjectStrHoleUnderNumericSpec", "SPY0609", CompilerDiagnosticSeverity.Error)),
        new("fstrings.md", "Interpolation Hole Type", "method call in a hole",
            new Y("GenFStrings.FStringWithMethodCalls", () => GenFStrings.FStringWithMethodCalls(), n => n is FStringLiteral f && f.Parts.Any(p => p.Expression is FunctionCall)),
            new YInjected("ErrorInjector.InjectUndefinedMethod", "SPY0203", CompilerDiagnosticSeverity.Error)),

        // function_default_parameters.md
        new("function_default_parameters.md", "Function Default Parameters", "default parameter values",
            new KnownRedRow("#2287", "no typed generator emits a default parameter value"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // function_definition.md
        new("function_definition.md", "Function Definition", "def with typed parameters and a return annotation",
            new Y("GenFunctions.ModuleWithFunctions", () => Functions(), n => n is FunctionDef { ReturnType: not null, Name: "compute" or "process" or "transform" }),
            new YInjected("ErrorInjector.InjectUndefinedTypeAtParameter", "SPY0202", CompilerDiagnosticSeverity.Error)),

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
            new YInjected("ErrorInjector.InjectWrongTypedYield", "SPY0220", CompilerDiagnosticSeverity.Error)),
        new("generators.md", "Syntax", "yield from",
            new Y("GenIterators.ModuleWithYieldFrom", () => GenIterators.ModuleWithYieldFrom(), n => n is YieldStatement { IsFrom: true }),
            new YInjected("ErrorInjector.InjectYieldFromNonIterable", "SPY0220", CompilerDiagnosticSeverity.Error)),
        new("generators.md", "Return Type Annotation", "element-type annotation on a generator",
            new Y("GenIterators.ModuleWithGenerator", () => GenIterators.ModuleWithGenerator(valid: true), n => n is FunctionDef { ReturnType: not null } f && f.Body.OfType<YieldStatement>().Any()),
            new YInjected("ErrorInjector.InjectUndefinedTypeAtReturn", "SPY0202", CompilerDiagnosticSeverity.Error)),

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
            new YInjected("ErrorInjector.InjectUndefinedTypeAtTypeArgument", "SPY0202", CompilerDiagnosticSeverity.Error)),
        new("generics.md", "Generic Functions", "def identity[T]",
            new Y("GenGenerics.GenericFunctionProgram", () => GenGenerics.GenericFunctionProgram(TypeEnv.WithGenerics, fuel: 2), n => n is FunctionDef { TypeParameters.Length: > 0 }),
            new YInjected("ErrorInjector.InjectUndefinedTypeAtParameter", "SPY0202", CompilerDiagnosticSeverity.Error)),
        new("generics.md", "Generic Function Instantiation", "identity[int](...)",
            new Y("GenGenerics.GenericFunctionProgram", () => GenGenerics.GenericFunctionProgram(TypeEnv.WithGenerics, fuel: 2), n => n is FunctionCall { Function: IndexAccess { Object: Identifier { Name: "identity" } } }),
            new YInjected("ErrorInjector.InjectWrongTypedExplicitGenericArgument", "SPY0220", CompilerDiagnosticSeverity.Error)),
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
            new ByDesign("identity_operators.md names SPY0349 (`is <Type>`, l.42) and SPY0465 (both operands value types, l.86); `v is None` on a non-Optional names neither — census control NonOptionalIdentityOperand @ TypedOptionals analyses clean (P63b, #2324)")),

        // if_statement.md
        new("if_statement.md", "If Statement", "if / else",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is IfStatement { ElseBody.Length: > 0 }),
            new YInjected("ErrorInjector.InjectNoFalsyCaseTruthTest", "SPY0220", CompilerDiagnosticSeverity.Error)),

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
            new YInjected("ErrorInjector.InjectOverrideWithoutBaseMember", "SPY0248", CompilerDiagnosticSeverity.Error)),
        new("inheritance.md", "The `super()` Function", "super().__init__(...)",
            new Y("GenClasses.ModuleWithClasses", () => Classes(), n => n is SuperExpression),
            new YInjected("ErrorInjector.InjectSuperInitArity", "SPY0224", CompilerDiagnosticSeverity.Error)),
        new("inheritance.md", "Multiple Interface Implementation", "class implementing two interfaces",
            new Y("GenInterfaces.ModuleWithMultipleInterfaces", () => GenInterfaces.ModuleWithMultipleInterfaces(), n => n is ClassDef { BaseClasses.Length: >= 2 }),
            new YInjected("ErrorInjector.InjectInterfaceMemberRemoved", "SPY0325", CompilerDiagnosticSeverity.Error)),

        // integer_literals.md
        new("integer_literals.md", "Integer Literals", "integer literals",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is IntegerLiteral),
            new KnownRedRow("#2324", "the file refuses SPY0220 (a literal outside its slot's range, an unsigned suffix in a signed slot) and SPY0222 (an unsigned literal against a signed operand); generated, no injected lane breaks them — was ByDesign, wrong by the file's prose (P63b spec-code ratchet)")),

        // interface_default_methods.md
        new("interface_default_methods.md", "Interface Default Methods", "interface methods with a default body",
            new KnownRedRow("#2287", "no typed generator emits an interface default method"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // interfaces.md
        new("interfaces.md", "Interfaces", "interface implemented by a class",
            new Y("GenInterfaces.ModuleWithInterface", () => GenInterfaces.ModuleWithInterface(methodCount: 0, completeImpl: true), n => n is InterfaceDef),
            new YInjected("ErrorInjector.InjectInterfaceMemberRemoved", "SPY0325", CompilerDiagnosticSeverity.Error)),
        new("interfaces.md", "Interface Inheritance", "interface deriving an interface",
            new Y("GenInterfaces.ModuleWithInterfaceHierarchy", () => GenInterfaces.ModuleWithInterfaceHierarchy(), n => n is InterfaceDef { BaseInterfaces.Length: > 0 }),
            new YInjected("ErrorInjector.InjectUndefinedTypeAtBase", "SPY0202", CompilerDiagnosticSeverity.Error)),
        new("interfaces.md", "Generic Interfaces", "interface I[T]",
            new Y("GenVariance.CovariantInterfaceProgram", () => GenVariance.CovariantInterfaceProgram(), n => n is InterfaceDef { TypeParameters.Length: > 0 }),
            new YInjected("ErrorInjector.InjectTypeArgumentArity", "SPY0224", CompilerDiagnosticSeverity.Error)),

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
            new YInjected("ErrorInjector.InjectNoFalsyCaseLogicalOperand", "SPY0220", CompilerDiagnosticSeverity.Error)),
        new("logical_operators.md", "Truthiness", "if on a non-bool operand",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is IfStatement
            {
                Test: IntegerLiteral or StringLiteral or ListLiteral or Identifier { Name: "x" or "y" or "s" }
                    or BinaryOp { Operator: BinaryOperator.Subtract or BinaryOperator.Multiply }
            }, Samples: 200),
            new YInjected("ErrorInjector.InjectNoFalsyCaseTruthTest", "SPY0220", CompilerDiagnosticSeverity.Error)),

        // loop_else.md
        new("loop_else.md", "Loop Else Clause", "for/while else",
            new KnownRedRow("#2287", "no typed generator emits a loop else clause"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // match_statement.md
        new("match_statement.md", "Match Statement", "match statement with a wildcard arm",
            new Y("GenMatchPatterns.MatchWithOrPatterns", () => GenMatchPatterns.MatchWithOrPatterns(), n => n is MatchStatement m && m.Cases.Any(c => c.Pattern is WildcardPattern)),
            new YInjected("ErrorInjector.InjectIrrefutableArmFirst", "SPY0700", CompilerDiagnosticSeverity.Error)),
        new("match_statement.md", "Guard Patterns", "case p if guard",
            new Y("GenMatchPatterns.MatchWithGuards", () => GenMatchPatterns.MatchWithGuards(), n => (n is MatchStatement m && m.Cases.Any(c => c.Guard != null)) || n is GuardPattern),
            new YInjected("ErrorInjector.InjectNoFalsyCaseGuard", "SPY0241", CompilerDiagnosticSeverity.Error)),
        new("match_statement.md", "Supported Patterns", "or-patterns",
            new Y("GenMatchPatterns.MatchWithOrPatterns", () => GenMatchPatterns.MatchWithOrPatterns(), n => n is OrPattern),
            new YInjected("ErrorInjector.InjectOrPatternBindingMismatch", "SPY0359", CompilerDiagnosticSeverity.Error)),
        new("match_statement.md", "Supported Patterns", "list patterns with a star capture",
            new Y("GenMatchPatterns.MatchWithListPatterns", () => GenMatchPatterns.MatchWithListPatterns(), n => n is ListPattern l && l.Elements.Any(e => e is StarPattern)),
            new YInjected("ErrorInjector.InjectListPatternElementOfOtherType", "SPY0220", CompilerDiagnosticSeverity.Error)),
        new("match_statement.md", "Supported Patterns", "and-patterns",
            new Y("GenMatchPatterns.MatchWithAndPatterns", () => GenMatchPatterns.MatchWithAndPatterns(), n => n is AndPattern),
            new YInjected("ErrorInjector.InjectAndPatternDuplicateCapture", "SPY0372", CompilerDiagnosticSeverity.Error)),
        new("match_statement.md", "Positional Patterns", "class patterns case C()",
            new Y("GenMatchPatterns.MatchWithTypePatterns", () => GenMatchPatterns.MatchWithTypePatterns(), n => n is PositionalPattern { Type: not null } or TypePattern),
            new YInjected("ErrorInjector.InjectUndefinedTypeAtPatternHead", "SPY0202", CompilerDiagnosticSeverity.Error)),
        new("match_statement.md", "Tuple Patterns", "tuple patterns",
            new Y("GenMatchPatterns.MatchWithNestedPatterns", () => GenMatchPatterns.MatchWithNestedPatterns(), n => n is TuplePattern),
            new YInjected("ErrorInjector.InjectTuplePatternExtraElement", "SPY0257", CompilerDiagnosticSeverity.Error)),
        new("match_statement.md", "Constant Patterns", "literal constant patterns",
            new Y("GenMatchPatterns.MatchWithOrPatterns", () => GenMatchPatterns.MatchWithOrPatterns(), n => n is LiteralPattern),
            new YInjected("ErrorInjector.InjectLiteralPatternOfOtherType", "SPY0220", CompilerDiagnosticSeverity.Error)),
        new("match_statement.md", "Exhaustiveness Checking", "non-exhaustive match statement (warning)",
            new Y("GenMatchPatterns.MatchWithOrPatterns", () => GenMatchPatterns.MatchWithOrPatterns(), n => n is MatchStatement m && m.Cases[^1].Pattern is WildcardPattern),
            new YInjected("GenMatchPatterns.MatchNonExhaustive", "SPY0463", CompilerDiagnosticSeverity.Warning)),
        new("match_statement.md", "Match Statement vs Match Expression", "match expression",
            new Y("GenMatchPatterns.MatchExpressionExhaustive", () => GenMatchPatterns.MatchExpressionExhaustive(), n => n is MatchExpression),
            new YInjected("GenMatchPatterns.MatchExpressionNonExhaustive", "SPY0416", CompilerDiagnosticSeverity.Error)),
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
            new KnownRedRow("#2287", "not generated, so no injection can break it; the file refuses SPY0212 (a type named for a builtin) and warns SPY0453 (consecutive underscores) and SPY0483 (a builtin shadowed in value position) — was ByDesign, wrong by the file's prose (P63b spec-code ratchet)")),

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
            // declaration shape — a hand lane (DeclarationShapes), the axis the site enumerator cannot list
            new YInjected("ErrorInjector.InjectCamelCaseLocal", "SPY0453", CompilerDiagnosticSeverity.Warning)),

        // nested_types.md
        new("nested_types.md", "Nested Types", "nested type declarations",
            new KnownRedRow("#2287", "no typed generator emits a nested type"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),

        // none_literal.md
        new("none_literal.md", "None Literal", "None",
            new Y("GenTyped.TypedProgram", () => TypedOptionals(), n => n is NoneLiteral),
            new KnownRedRow("#2324", "the file refuses SPY0604 (a bare None stored to T?); generated, no injected lane breaks it — was ByDesign, wrong by the file's prose (P63b spec-code ratchet)")),

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
            new YInjected("ErrorInjector.InjectWrongTypedArithmeticOperand", "SPY0222", CompilerDiagnosticSeverity.Error)),
        new("operator_overloading.md", "Arithmetic Operators", "unary operator on a user type",
            new Y("GenOperators.UnaryOperatorProgram", () => GenOperators.UnaryOperatorProgram(), n => n is UnaryOp { Operand: Identifier { Name: "v" } }),
            new YInjected("ErrorInjector.InjectWrongTypedUnaryOperand", "SPY0223", CompilerDiagnosticSeverity.Error)),
        new("operator_overloading.md", "Comparison Operators", "comparison operator on a user type",
            new Y("GenOperators.ComparisonOperatorProgram", () => GenOperators.ComparisonOperatorProgram(), n => n is BinaryOp { Left: Identifier { Name: "a" }, Right: Identifier { Name: "b" } }),
            new YInjected("ErrorInjector.InjectWrongTypedComparisonOperand", "SPY0222", CompilerDiagnosticSeverity.Error)),

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
            // declaration shape — a hand lane (DeclarationShapes), the axis the site enumerator cannot list
            new KnownRedRow("#2335", "analyses clean; run is SPY0908 CS1558 — main's parameter list is left to Roslyn (ErrorInjector.InjectMainParameter)")),

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
            new YInjected("ErrorInjector.InjectWrongTypedReturnValue", "SPY0260", CompilerDiagnosticSeverity.Error)),

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
            new ByDesign("statements.md §Expression Statement l.5: any expression can be a statement — no typing surface of its own; census control ExpressionStatementOfAnyValue @ TypedStatements analyses clean (P63b, #2324)")),
        new("statements.md", "Declaration Keywords", "let declarations",
            new KnownRedRow("#2287", "no typed generator emits let"),
            new KnownRedRow("#2287", "not generated, so no injection can break it")),
        new("statements.md", "Constants", "const declaration and its reassignment",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is VariableDeclaration { IsConst: true }),
            new YInjected("ErrorInjector.InjectConstReassignment", "SPY0225", CompilerDiagnosticSeverity.Error)),
        new("statements.md", "Module-Level Declaration Rules", "module-level typed constant",
            new Y("GenImports.ImportingModule", () => GenImports.ImportingModule(TypeEnv.Default, fuel: 1), n => n is Module m && m.Body.OfType<VariableDeclaration>().Any()),
            new YInjected("ErrorInjector.InjectWrongTypedLibraryConstant", "SPY0220", CompilerDiagnosticSeverity.Error)),

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
            new YInjected("ErrorInjector.InjectWrongTypedArithmeticOperand", "SPY0222", CompilerDiagnosticSeverity.Error)),

        // string_type.md
        new("string_type.md", "String Method Availability", "str.upper() / lower() / strip()",
            new Y("GenTyped.TypedProgram", () => TypedStatements(), n => n is FunctionCall { Function: MemberAccess { Member: "upper" or "lower" or "strip" } }, Samples: 100),
            new YInjected("ErrorInjector.InjectUndefinedMethod", "SPY0203", CompilerDiagnosticSeverity.Error)),

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
            new YInjected("ErrorInjector.InjectTypeArgumentArity", "SPY0224", CompilerDiagnosticSeverity.Error)),
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
            new YInjected("ErrorInjector.InjectWrongTypedReassignment", "SPY0220", CompilerDiagnosticSeverity.Error)),
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

    /// <summary>Line numbers of every fenced Sharpy block, whatever its marker.</summary>
    private static List<int> SharpyBlockLines(string file)
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
            if (SharpyFence.IsMatch(lines[i]))
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
            // ANY Sharpy block, marked or not (#2324, P63b): an error-marked example shows a refusal of a
            // construct, and a fragment shows the construct — compiled-only let a file of refusal examples pass.
            var blocks = SharpyBlockLines(group.Key);
            if (blocks.Count > 0)
                problems.Add($"{group.Key}: NoConstruct, but the file has {blocks.Count} Sharpy block(s) "
                    + $"(fence line(s) {string.Join(", ", blocks)}) — an example (compiled, fragment or error) shows a construct; row it");
        }

        Assert.True(problems.Count == 0,
            $"{problems.Count} malformed roster lane(s):\n  " + string.Join("\n  ", problems));
    }

    // ---- RAW positive control (Task 3) -------------------------------------------------------

    private const ulong RawSeed = 2288;

    // The compiler-side defects the raw corpus draws are pinned ONCE, in KnownGeneratorPins (shared with
    // the generated lane's RejectionSink, #2288): a sample is tolerated (excluded from the corpus, counted,
    // printed) only when its PRE-unparse AST has the pinned shape AND every re-parse error carries the
    // pin's code; any other parse error stays red. The roster is empty since the #2169 pin drained.

    private sealed record RawControlResult(int RowsTested, IReadOnlyList<string> Failures,
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
        _output.WriteLine($"RAW control: {result.RowsTested} Y-generated rows x {RawSamples} samples, more where a row raises it (seed {RawSeed}) "
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
        Assert.True(result.RowsTested > 0, "no Y-generated row was checked — the control is vacuous");
        Assert.True(result.Failures.Count == 0,
            $"{result.Failures.Count} Y-generated row(s) fail the raw control:\n  " + string.Join("\n  ", result.Failures));
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

    // ---- Roster ↔ property links (#2288 verify F3) ------------------------------------------------
    //
    // A roster lane is a claim about a property; these facts read the property sources (Roslyn) so the
    // claim cannot drift green from the code that sustains it. The property universe is every *.cs under
    // Properties/ except this roster and the generator namespace's own directory.

    private static string PropertiesRoot => Path.Combine(FixtureRoots.RepositoryRoot, "src", "Sharpy.Compiler.Tests", "Properties");

    private const string RosterFile = "Semantic/TypedGeneratorRosterTests.cs";
    private const string GeneratorDirectory = "Generators/Typed/";
    /// <summary>The injector's files: <c>ErrorInjector.cs</c> and its partials (<c>ErrorInjector.Mutations.cs</c>, #2324).</summary>
    private const string InjectorFilePrefix = "Generators/Typed/ErrorInjector";

    private static readonly Lazy<PropertySourceIndex> PropertySources = new(() => PropertySourceIndex.Load(PropertiesRoot,
        file => file != RosterFile && !file.StartsWith(GeneratorDirectory, StringComparison.Ordinal)));

    /// <summary>The refusal verdicts a YInjected lane's property must reach (the one seam).</summary>
    private static readonly HashSet<string> RefusalVerdicts = new(StringComparer.Ordinal)
    {
        "ErrorInjector.Judge", "ErrorInjector.JudgeSource", "ErrorInjector.JudgeProject",
    };

    /// <summary>
    /// The ACCEPTANCE verdicts a Y-generated lane's property must reach — each judges every sample, never a
    /// fraction: the generated-lane filter with its sink (<c>SemanticFilter.*</c>), the accepted-program
    /// verdict (<c>ErrorInjector.JudgeClean</c>), <c>PropertyCorpus.AssertAllPassOrAllowed</c> with NO
    /// allowed codes, the variance lanes' diagnostic-free core
    /// (<c>VariancePropertyTests.AssertEverySampleIsDiagnosticFree</c>), and the decorator lanes'
    /// <c>Assert.Equal(set.Total, set.Passed)</c> over every sample.
    /// </summary>
    private static readonly HashSet<string> AcceptanceVerdicts = new(StringComparer.Ordinal)
    {
        "SemanticFilter.WellTypedProgram", "SemanticFilter.CompilableProgram", "SemanticFilter.WellTypedSource",
        "SemanticFilter.CompilableProject", "SemanticFilter.AdmitWellTyped", "ErrorInjector.JudgeClean",
        "PropertyCorpus.AssertAllPassOrAllowed", "AssertEverySampleIsDiagnosticFree", "Assert.Equal",
    };

    private static bool IsAcceptance(PropertySourceIndex.Method method, string name,
        Microsoft.CodeAnalysis.CSharp.Syntax.ArgumentListSyntax arguments)
    {
        if (!AcceptanceVerdicts.Contains(name))
            return false;
        var args = arguments.Arguments;
        return name switch
        {
            // No allowed codes: the second argument (or `allowedCodes:`) is the null literal.
            "PropertyCorpus.AssertAllPassOrAllowed" => args.Any(a =>
                (a.NameColon?.Name.Identifier.ValueText == "allowedCodes" || (a.NameColon is null && args.IndexOf(a) == 1))
                && Microsoft.CodeAnalysis.CSharpExtensions.IsKind(a.Expression, Microsoft.CodeAnalysis.CSharp.SyntaxKind.NullLiteralExpression)),
            // Only the variance file's helper of that name.
            "AssertEverySampleIsDiagnosticFree" => method.File == "Semantic/VariancePropertyTests.cs",
            // Only Assert.Equal(x.Total, x.Passed): every sample passed.
            "Assert.Equal" => args.Count == 2
                && args[0].Expression is Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Total" }
                && args[1].Expression is Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Passed" },
            _ => true,
        };
    }

    /// <summary>
    /// Every <see cref="YInjected"/> row is the claim of a real refusal lane. For a generator member
    /// (an invalid-program generator): some property method names the member AND the row's code literal in
    /// ONE body, and its closure (the method plus the same-file helpers it calls) reaches the refusal seam
    /// at the row's severity. For an <c>ErrorInjector</c> method: the method's own body draws exactly the
    /// row's code (its only code literal, and <c>CompilerDiagnosticSeverity.Warning</c> named iff the row is
    /// a warning), and some property method references it with a closure reaching
    /// <c>ErrorInjector.Judge</c>.
    /// </summary>
    [Fact]
    public void YInjected_RowsAreBackedByTheirRefusalProperty()
    {
        var index = PropertySources.Value;
        var injector = PropertySourceIndex.Load(PropertiesRoot, file => file.StartsWith(InjectorFilePrefix, StringComparison.Ordinal));
        var problems = new List<string>();
        var checkedRows = 0;

        foreach (var row in Rows)
        {
            if (row.Injected is not YInjected yi)
                continue;
            checkedRows++;
            var (type, member) = (yi.Member.Split('.')[0], yi.Member.Split('.')[1]);
            var severity = $"CompilerDiagnosticSeverity.{yi.Severity}";

            if (type == "ErrorInjector")
            {
                var bodies = injector.Methods.Where(m => m.Name == member).ToList();
                var codes = bodies.SelectMany(m => m.Literals).Where(l => DiagnosticCode.IsMatch(l)).Distinct().ToList();
                if (bodies.Count == 0)
                    problems.Add($"{yi.Member}: no such method in {InjectorFilePrefix}*.cs");
                else if (codes is not [var only] || only != yi.Code)
                    problems.Add($"{yi.Member}: the roster says {yi.Code}, the injector's body draws [{string.Join(", ", codes)}]");
                var warns = bodies.Any(m => m.References.Contains("CompilerDiagnosticSeverity.Warning"));
                if (warns != (yi.Severity == CompilerDiagnosticSeverity.Warning))
                    problems.Add($"{yi.Member}: the roster says {yi.Severity}, the injector {(warns ? "names" : "does not name")} a Warning severity");

                if (!index.Methods.Any(m => m.References.Contains(yi.Member)
                        && index.ClosureInvocationNodes(m).Any(i => PropertySourceIndex.InvokedName(i) == "ErrorInjector.Judge"
                            && index.IsConsumed(m, i))))
                    problems.Add($"{yi.Member}: no property method references it and reaches ErrorInjector.Judge with its verdict consumed");
                continue;
            }

            var backing = index.Methods
                .Where(m => m.References.Contains(yi.Member) && m.Literals.Contains(yi.Code))
                .Where(m => index.Closure(m).Any(c => c.References.Contains(severity)))
                .Where(m => index.ClosureInvocationNodes(m).Any(i => RefusalVerdicts.Contains(PropertySourceIndex.InvokedName(i))
                    && index.IsConsumed(m, i)))
                .ToList();
            if (backing.Count == 0)
                problems.Add($"{yi.Member} ({yi.Code}, {yi.Severity}): no property method names the member and \"{yi.Code}\" "
                    + $"in one body and reaches the refusal seam at {severity} with its verdict consumed (asserted, never discarded)");
            else
                _output.WriteLine($"  {yi.Member} {yi.Code} {yi.Severity} <- {string.Join(", ", backing)}");
        }

        Assert.Equal(ExpectedYInjected, checkedRows);
        Assert.True(problems.Count == 0,
            $"{problems.Count} YInjected row(s) are not backed by their property:\n  " + string.Join("\n  ", problems));
    }

    /// <summary>
    /// Every <see cref="Y"/> row's generator CONFIGURATION is sampled by a property that accepts every sample
    /// (#2324, P63b Decision 8 — the name-only link let a neutered lane keep backing its row): some property
    /// method calls the row's member with the SAME normalized arguments the row's lambda passes (following
    /// same-file helpers on both sides, named arguments placed by parameter, lambda parameters as <c>_</c>),
    /// its closure reaches one of <see cref="AcceptanceVerdicts"/>, and that verdict is CONSUMED — asserted,
    /// never discarded (<see cref="PropertySourceIndex.IsConsumed"/>).
    /// </summary>
    [Fact]
    public void YGenerated_RowsAreBackedByAnAcceptanceProperty()
    {
        var index = PropertySources.Value;
        var members = GeneratorMembers();
        var roster = PropertySourceIndex.Load(PropertiesRoot, file => file == RosterFile);
        var problems = new List<string>();
        var callsTested = new HashSet<PropertySourceIndex.GeneratorCall>();
        var backingByCall = new Dictionary<PropertySourceIndex.GeneratorCall, List<PropertySourceIndex.Method>>();
        var callsByMethod = index.Methods.ToDictionary(m => m, m => index.GeneratorCalls(m.File, m.Body, members).ToHashSet());

        foreach (var (label, line, generator) in roster.YRowGenerators(RosterFile))
        {
            var calls = roster.GeneratorCalls(RosterFile, generator, members).Where(c => c.Member == label).ToList();
            if (calls.Count == 0)
            {
                problems.Add($"line {line}: the row's lambda calls no {label}");
                continue;
            }
            foreach (var call in calls)
            {
                if (!callsTested.Add(call))
                    continue;
                var backing = index.Methods
                    .Where(m => callsByMethod[m].Contains(call))
                    .Where(m => index.ClosureInvocationNodes(m).Any(i =>
                        IsAcceptance(m, PropertySourceIndex.InvokedName(i), i.ArgumentList) && index.IsConsumed(m, i)))
                    .ToList();
                backingByCall[call] = backing;
                if (backing.Count == 0)
                    problems.Add($"line {line}: no property samples {call} and consumes an acceptance verdict");
            }
        }

        foreach (var (call, backing) in backingByCall.OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal))
            _output.WriteLine($"  {call} <- {string.Join(", ", backing.Take(3))}{(backing.Count > 3 ? $" (+{backing.Count - 3})" : "")}");
        Assert.True(callsTested.Count > 0, "no Y-generated configuration was checked — the link is vacuous");
        Assert.True(problems.Count == 0,
            $"{problems.Count} Y-generated configuration(s) have no acceptance property:\n  " + string.Join("\n  ", problems));
    }

    /// <summary>Every public static member of a type in the generator namespace, by <c>Type.Member</c>.</summary>
    private static IReadOnlyDictionary<string, MethodInfo[]> GeneratorMembers() =>
        typeof(GenTyped).Assembly.GetTypes()
            .Where(t => t.Namespace == GeneratorsNamespace && !t.IsNested && !t.Name.StartsWith("<", StringComparison.Ordinal))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static).Select(m => (Name: $"{t.Name}.{m.Name}", Method: m)))
            .GroupBy(m => m.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(m => m.Method).ToArray(), StringComparer.Ordinal);

    /// <summary>
    /// Every <see cref="Y"/> row's label is the generator member its lambda calls — read from this file's
    /// syntax, following the roster's consumer-argument helpers — so the label <see cref="Rows_AreWellFormed"/>
    /// reflects on and the roster's links read cannot name one member while the control samples another.
    /// </summary>
    [Fact]
    public void YGenerated_LabelIsTheMemberItsGeneratorCalls()
    {
        var generatorMethods = typeof(GenTyped).Assembly.GetTypes()
            .Where(t => t.Namespace == GeneratorsNamespace && !t.IsNested && !t.Name.StartsWith("<", StringComparison.Ordinal))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static).Select(m => $"{t.Name}.{m.Name}"))
            .ToHashSet(StringComparer.Ordinal);
        var rows = PropertySourceIndex.YRows(File.ReadAllText(Path.Combine(PropertiesRoot, RosterFile)), generatorMethods);

        var problems = rows
            .Where(r => r.Invoked.Count != 1 || !r.Invoked.Contains(r.Label))
            .Select(r => $"line {r.Line}: label '{r.Label}', the generator calls [{string.Join(", ", r.Invoked.OrderBy(i => i, StringComparer.Ordinal))}]")
            .ToList();

        Assert.Equal(ExpectedYGenerated, rows.Count);
        Assert.True(problems.Count == 0,
            $"{problems.Count} Y row(s) label a member their generator does not call:\n  " + string.Join("\n  ", problems));
    }

    // ---- Spec-named codes (#2324, P63b Decision 10) ------------------------------------------------

    /// <summary>A code a spec file names that no roster row of the file claims, and the issue that owns the
    /// missing lane: #2287 for a lexer/parser code or a file no typed generator emits, #2324 for a typed code
    /// in a file the generator does emit. The type name carries <c>KnownRed</c> and the cite is the first
    /// argument, so <c>build_tools/allowlist_issue_state.py</c>'s <c>KNOWNRED_CELL_RE</c> reads every row
    /// (a closed cite is red at <c>/push</c>).</summary>
    public sealed record KnownRedSpecCode(string Issue, string File, string Code);

    /// <summary>A code a spec file names only as a cross-reference to <paramref name="ReferencedFile"/>'s
    /// construct; that file must itself name the code AND claim it by a row — the exemption is never its own
    /// subject.</summary>
    public sealed record ProseCrossReference(string File, string Code, string ReferencedFile);

    /// <summary>Seeded @ the P63b spec-code commit from the spec text and the roster: every (file, code) pair
    /// with no claiming row. A ratchet — a row whose pair a roster row now claims, or whose file no longer
    /// names the code, is red (drain on fix).</summary>
    private static readonly KnownRedSpecCode[] UnclaimedSpecCodes =
    {
        new KnownRedSpecCode("#2324", "arithmetic_operators.md", "SPY0220"),
        new KnownRedSpecCode("#2324", "arithmetic_operators.md", "SPY0223"),
        new KnownRedSpecCode("#2324", "arithmetic_operators.md", "SPY0328"),
        new KnownRedSpecCode("#2324", "arithmetic_operators.md", "SPY0348"),
        new KnownRedSpecCode("#2287", "assignment_operators.md", "SPY0220"),
        new KnownRedSpecCode("#2287", "assignment_operators.md", "SPY0222"),
        new KnownRedSpecCode("#2287", "assignment_operators.md", "SPY0276"),
        new KnownRedSpecCode("#2324", "async_programming.md", "SPY0274"),
        new KnownRedSpecCode("#2324", "async_programming.md", "SPY0358"),
        new KnownRedSpecCode("#2287", "bitwise_operators.md", "SPY0213"),
        new KnownRedSpecCode("#2287", "bitwise_operators.md", "SPY0220"),
        new KnownRedSpecCode("#2287", "bitwise_operators.md", "SPY0222"),
        new KnownRedSpecCode("#2287", "bitwise_operators.md", "SPY0348"),
        new KnownRedSpecCode("#2324", "builtin_functions.md", "SPY0220"),
        new KnownRedSpecCode("#2324", "builtin_functions.md", "SPY0230"),
        new KnownRedSpecCode("#2324", "builtin_functions.md", "SPY0342"),
        new KnownRedSpecCode("#2324", "builtin_functions.md", "SPY0344"),
        new KnownRedSpecCode("#2324", "builtin_functions.md", "SPY0345"),
        new KnownRedSpecCode("#2324", "builtin_functions.md", "SPY0349"),
        new KnownRedSpecCode("#2324", "builtin_functions.md", "SPY0386"),
        new KnownRedSpecCode("#2324", "builtin_functions.md", "SPY0609"),
        new KnownRedSpecCode("#2324", "builtin_functions.md", "SPY0613"),
        new KnownRedSpecCode("#2324", "classes.md", "SPY0384"),
        new KnownRedSpecCode("#2324", "classes.md", "SPY0385"),
        new KnownRedSpecCode("#2324", "classes.md", "SPY0400"),
        new KnownRedSpecCode("#2324", "collection_types.md", "SPY0222"),
        new KnownRedSpecCode("#2324", "collection_types.md", "SPY0320"),
        new KnownRedSpecCode("#2324", "collection_types.md", "SPY0602"),
        new KnownRedSpecCode("#2287", "comprehensions.md", "SPY0147"),
        new KnownRedSpecCode("#2287", "comprehensions.md", "SPY0220"),
        new KnownRedSpecCode("#2287", "comprehensions.md", "SPY0358"),
        new KnownRedSpecCode("#2287", "comprehensions.md", "SPY0704"),
        new KnownRedSpecCode("#2324", "context_managers.md", "SPY0239"),
        new KnownRedSpecCode("#2324", "context_managers.md", "SPY0266"),
        new KnownRedSpecCode("#2324", "context_managers.md", "SPY0600"),
        new KnownRedSpecCode("#2324", "context_managers.md", "SPY0703"),
        new KnownRedSpecCode("#2287", "conversion_operators.md", "SPY0436"),
        new KnownRedSpecCode("#2287", "conversion_operators.md", "SPY0437"),
        new KnownRedSpecCode("#2287", "conversion_operators.md", "SPY0438"),
        new KnownRedSpecCode("#2287", "conversion_operators.md", "SPY0439"),
        new KnownRedSpecCode("#2324", "dataclass.md", "SPY0380"),
        new KnownRedSpecCode("#2324", "dataclass.md", "SPY0381"),
        new KnownRedSpecCode("#2324", "dataclass.md", "SPY0382"),
        new KnownRedSpecCode("#2324", "dataclass.md", "SPY0383"),
        new KnownRedSpecCode("#2324", "dataclass.md", "SPY0400"),
        new KnownRedSpecCode("#2324", "dataclass.md", "SPY0401"),
        new KnownRedSpecCode("#2324", "dataclass.md", "SPY0706"),
        new KnownRedSpecCode("#2324", "decorators.md", "SPY0301"),
        new KnownRedSpecCode("#2324", "decorators.md", "SPY0322"),
        new KnownRedSpecCode("#2324", "decorators.md", "SPY0415"),
        new KnownRedSpecCode("#2324", "decorators.md", "SPY0425"),
        new KnownRedSpecCode("#2324", "decorators.md", "SPY0442"),
        new KnownRedSpecCode("#2324", "decorators.md", "SPY0443"),
        new KnownRedSpecCode("#2324", "decorators.md", "SPY0444"),
        new KnownRedSpecCode("#2324", "decorators.md", "SPY0464"),
        new KnownRedSpecCode("#2324", "decorators.md", "SPY0466"),
        new KnownRedSpecCode("#2324", "decorators.md", "SPY0480"),
        new KnownRedSpecCode("#2324", "decorators.md", "SPY0495"),
        new KnownRedSpecCode("#2324", "decorators.md", "SPY0707"),
        new KnownRedSpecCode("#2287", "del_statement.md", "SPY0144"),
        new KnownRedSpecCode("#2287", "diagnostic_suppression.md", "SPY0201"),
        new KnownRedSpecCode("#2287", "diagnostic_suppression.md", "SPY0322"),
        new KnownRedSpecCode("#2287", "diagnostic_suppression.md", "SPY0450"),
        new KnownRedSpecCode("#2287", "diagnostic_suppression.md", "SPY0451"),
        new KnownRedSpecCode("#2287", "diagnostic_suppression.md", "SPY0452"),
        new KnownRedSpecCode("#2287", "diagnostic_suppression.md", "SPY0480"),
        new KnownRedSpecCode("#2287", "diagnostic_suppression.md", "SPY0481"),
        new KnownRedSpecCode("#2287", "diagnostic_suppression.md", "SPY0482"),
        new KnownRedSpecCode("#2287", "diagnostic_suppression.md", "SPY9999"),
        new KnownRedSpecCode("#2287", "dotnet_interop.md", "SPY0203"),
        new KnownRedSpecCode("#2287", "dotnet_interop.md", "SPY0229"),
        new KnownRedSpecCode("#2287", "dotnet_interop.md", "SPY0248"),
        new KnownRedSpecCode("#2287", "dotnet_interop.md", "SPY0320"),
        new KnownRedSpecCode("#2287", "dotnet_interop.md", "SPY0336"),
        new KnownRedSpecCode("#2287", "dstrings.md", "SPY0029"),
        new KnownRedSpecCode("#2324", "dunder_methods.md", "SPY0203"),
        new KnownRedSpecCode("#2324", "dunder_methods.md", "SPY0220"),
        new KnownRedSpecCode("#2324", "dunder_methods.md", "SPY0222"),
        new KnownRedSpecCode("#2324", "dunder_methods.md", "SPY0260"),
        new KnownRedSpecCode("#2324", "dunder_methods.md", "SPY0331"),
        new KnownRedSpecCode("#2324", "dunder_methods.md", "SPY0427"),
        new KnownRedSpecCode("#2324", "dunder_methods.md", "SPY0454"),
        new KnownRedSpecCode("#2324", "dunder_methods.md", "SPY0522"),
        new KnownRedSpecCode("#2324", "dunder_methods.md", "SPY0604"),
        new KnownRedSpecCode("#2324", "dunder_methods.md", "SPY1001"),
        new KnownRedSpecCode("#2324", "dunder_methods_recommendations.md", "SPY0201"),
        new KnownRedSpecCode("#2324", "dunder_methods_recommendations.md", "SPY0301"),
        new KnownRedSpecCode("#2324", "dunder_methods_recommendations.md", "SPY0454"),
        new KnownRedSpecCode("#2287", "ellipsis_literal.md", "SPY0149"),
        new KnownRedSpecCode("#2287", "enums.md", "SPY0525"),
        new KnownRedSpecCode("#2287", "events.md", "SPY0496"),
        new KnownRedSpecCode("#2324", "exception_handling.md", "SPY0272"),
        new KnownRedSpecCode("#2324", "exception_handling.md", "SPY0702"),
        new KnownRedSpecCode("#2287", "feature_roadmap.md", "SPY0130"),
        new KnownRedSpecCode("#2287", "feature_roadmap.md", "SPY0302"),
        new KnownRedSpecCode("#2287", "float_literals.md", "SPY0023"),
        new KnownRedSpecCode("#2324", "for_statement.md", "SPY0227"),
        new KnownRedSpecCode("#2287", "fstrings.md", "SPY0021"),
        new KnownRedSpecCode("#2324", "fstrings.md", "SPY0234"),
        new KnownRedSpecCode("#2324", "fstrings.md", "SPY0522"),
        new KnownRedSpecCode("#2324", "fstrings.md", "SPY0613"),
        new KnownRedSpecCode("#2324", "fstrings.md", "SPY1001"),
        new KnownRedSpecCode("#2287", "function_default_parameters.md", "SPY0229"),
        new KnownRedSpecCode("#2287", "function_default_parameters.md", "SPY0326"),
        new KnownRedSpecCode("#2287", "function_default_parameters.md", "SPY0401"),
        new KnownRedSpecCode("#2324", "function_definition.md", "SPY0204"),
        new KnownRedSpecCode("#2324", "function_parameters.md", "SPY0353"),
        new KnownRedSpecCode("#2287", "function_types.md", "SPY0104"),
        new KnownRedSpecCode("#2287", "function_types.md", "SPY0277"),
        new KnownRedSpecCode("#2287", "function_types.md", "SPY0279"),
        new KnownRedSpecCode("#2287", "function_types.md", "SPY0280"),
        new KnownRedSpecCode("#2287", "function_types.md", "SPY0339"),
        new KnownRedSpecCode("#2287", "function_types.md", "SPY0342"),
        new KnownRedSpecCode("#2287", "function_types.md", "SPY0346"),
        new KnownRedSpecCode("#2287", "function_types.md", "SPY0486"),
        new KnownRedSpecCode("#2287", "function_variadic_arguments.md", "SPY0496"),
        new KnownRedSpecCode("#2324", "generators.md", "SPY0265"),
        new KnownRedSpecCode("#2324", "generators.md", "SPY0267"),
        new KnownRedSpecCode("#2324", "generators.md", "SPY0268"),
        new KnownRedSpecCode("#2324", "generators.md", "SPY0269"),
        new KnownRedSpecCode("#2324", "generators.md", "SPY0270"),
        new KnownRedSpecCode("#2324", "generators.md", "SPY0703"),
        new KnownRedSpecCode("#2324", "generators.md", "SPY1001"),
        new KnownRedSpecCode("#2324", "generic_variance.md", "SPY0220"),
        new KnownRedSpecCode("#2324", "generics.md", "SPY0229"),
        new KnownRedSpecCode("#2324", "generics.md", "SPY0339"),
        new KnownRedSpecCode("#2324", "generics.md", "SPY0604"),
        new KnownRedSpecCode("#2324", "generics.md", "SPY0705"),
        new KnownRedSpecCode("#2287", "identifiers.md", "SPY0015"),
        new KnownRedSpecCode("#2324", "identity_operators.md", "SPY0349"),
        new KnownRedSpecCode("#2324", "identity_operators.md", "SPY0465"),
        new KnownRedSpecCode("#2324", "import_statements.md", "SPY0708"),
        new KnownRedSpecCode("#2287", "integer_literals.md", "SPY0023"),
        new KnownRedSpecCode("#2324", "interfaces.md", "SPY0248"),
        new KnownRedSpecCode("#2324", "interfaces.md", "SPY0266"),
        new KnownRedSpecCode("#2324", "interfaces.md", "SPY0413"),
        new KnownRedSpecCode("#2324", "interfaces.md", "SPY0464"),
        new KnownRedSpecCode("#2324", "interfaces.md", "SPY0607"),
        new KnownRedSpecCode("#2324", "interfaces.md", "SPY0707"),
        new KnownRedSpecCode("#2324", "interfaces.md", "SPY1001"),
        new KnownRedSpecCode("#2287", "keywords.md", "SPY0101"),
        new KnownRedSpecCode("#2287", "keywords.md", "SPY0144"),
        new KnownRedSpecCode("#2287", "lexer_implementation.md", "SPY0150"),
        new KnownRedSpecCode("#2287", "literal_string.md", "SPY0103"),
        new KnownRedSpecCode("#2287", "literal_string.md", "SPY0220"),
        new KnownRedSpecCode("#2287", "literal_string.md", "SPY0222"),
        new KnownRedSpecCode("#2287", "match_statement.md", "SPY0102"),
        new KnownRedSpecCode("#2287", "match_statement.md", "SPY0125"),
        new KnownRedSpecCode("#2324", "match_statement.md", "SPY0224"),
        new KnownRedSpecCode("#2324", "match_statement.md", "SPY0242"),
        new KnownRedSpecCode("#2324", "match_statement.md", "SPY0345"),
        new KnownRedSpecCode("#2324", "match_statement.md", "SPY0361"),
        new KnownRedSpecCode("#2324", "match_statement.md", "SPY0363"),
        new KnownRedSpecCode("#2324", "match_statement.md", "SPY0468"),
        new KnownRedSpecCode("#2324", "match_statement.md", "SPY0498"),
        new KnownRedSpecCode("#2324", "match_statement.md", "SPY0605"),
        new KnownRedSpecCode("#2324", "match_statement.md", "SPY0908"),
        new KnownRedSpecCode("#2287", "membership_operators.md", "SPY0222"),
        new KnownRedSpecCode("#2287", "membership_operators.md", "SPY0320"),
        new KnownRedSpecCode("#2287", "method_overloading.md", "SPY0220"),
        new KnownRedSpecCode("#2287", "method_overloading.md", "SPY0244"),
        new KnownRedSpecCode("#2287", "method_overloading.md", "SPY0353"),
        new KnownRedSpecCode("#2287", "method_overloading.md", "SPY0354"),
        new KnownRedSpecCode("#2287", "method_overloading.md", "SPY0355"),
        new KnownRedSpecCode("#2287", "method_overloading.md", "SPY0701"),
        new KnownRedSpecCode("#2287", "module_system.md", "SPY0305"),
        new KnownRedSpecCode("#2287", "module_system.md", "SPY0313"),
        new KnownRedSpecCode("#2287", "module_system.md", "SPY0520"),
        new KnownRedSpecCode("#2287", "module_system.md", "SPY0522"),
        new KnownRedSpecCode("#2287", "module_system.md", "SPY0523"),
        new KnownRedSpecCode("#2287", "module_system.md", "SPY0526"),
        new KnownRedSpecCode("#2287", "module_system.md", "SPY0615"),
        new KnownRedSpecCode("#2287", "name_shadowing.md", "SPY0212"),
        new KnownRedSpecCode("#2287", "name_shadowing.md", "SPY0220"),
        new KnownRedSpecCode("#2287", "name_shadowing.md", "SPY0453"),
        new KnownRedSpecCode("#2287", "name_shadowing.md", "SPY0483"),
        new KnownRedSpecCode("#2287", "name_shadowing.md", "SPY0492"),
        new KnownRedSpecCode("#2287", "nested_types.md", "SPY0708"),
        new KnownRedSpecCode("#2287", "null_coalescing_assignment.md", "SPY0146"),
        new KnownRedSpecCode("#2287", "null_coalescing_assignment.md", "SPY0220"),
        new KnownRedSpecCode("#2287", "null_coalescing_assignment.md", "SPY0222"),
        new KnownRedSpecCode("#2287", "null_coalescing_assignment.md", "SPY0244"),
        new KnownRedSpecCode("#2287", "nullable_types.md", "SPY0229"),
        new KnownRedSpecCode("#2287", "nullable_types.md", "SPY0604"),
        new KnownRedSpecCode("#2287", "overload_resolution.md", "SPY0220"),
        new KnownRedSpecCode("#2287", "overload_resolution.md", "SPY0224"),
        new KnownRedSpecCode("#2287", "overload_resolution.md", "SPY0234"),
        new KnownRedSpecCode("#2287", "overload_resolution.md", "SPY0235"),
        new KnownRedSpecCode("#2287", "overload_resolution.md", "SPY0353"),
        new KnownRedSpecCode("#2287", "overload_resolution.md", "SPY0354"),
        new KnownRedSpecCode("#2287", "overload_resolution.md", "SPY0355"),
        new KnownRedSpecCode("#2287", "overload_resolution.md", "SPY0370"),
        new KnownRedSpecCode("#2287", "parameter_modifiers.md", "SPY0103"),
        new KnownRedSpecCode("#2287", "parameter_modifiers.md", "SPY0104"),
        new KnownRedSpecCode("#2287", "properties.md", "SPY0148"),
        new KnownRedSpecCode("#2287", "properties_function_style.md", "SPY0405"),
        new KnownRedSpecCode("#2287", "properties_function_style.md", "SPY0407"),
        new KnownRedSpecCode("#2287", "properties_function_style.md", "SPY0496"),
        new KnownRedSpecCode("#2287", "question_mark_operator.md", "SPY0211"),
        new KnownRedSpecCode("#2287", "question_mark_operator.md", "SPY0460"),
        new KnownRedSpecCode("#2287", "question_mark_operator.md", "SPY0461"),
        new KnownRedSpecCode("#2287", "question_mark_operator.md", "SPY0462"),
        new KnownRedSpecCode("#2287", "question_mark_operator.md", "SPY0480"),
        new KnownRedSpecCode("#2287", "source_files.md", "SPY0150"),
        new KnownRedSpecCode("#2287", "source_generators.md", "SPY0445"),
        new KnownRedSpecCode("#2287", "source_generators.md", "SPY0446"),
        new KnownRedSpecCode("#2287", "source_generators.md", "SPY0447"),
        new KnownRedSpecCode("#2287", "source_generators.md", "SPY0550"),
        new KnownRedSpecCode("#2287", "source_generators.md", "SPY0551"),
        new KnownRedSpecCode("#2287", "source_generators.md", "SPY0552"),
        new KnownRedSpecCode("#2287", "source_generators.md", "SPY0553"),
        new KnownRedSpecCode("#2287", "source_generators.md", "SPY0554"),
        new KnownRedSpecCode("#2287", "spread_operator.md", "SPY0239"),
        new KnownRedSpecCode("#2287", "statements.md", "SPY0103"),
        new KnownRedSpecCode("#2287", "statements.md", "SPY0104"),
        new KnownRedSpecCode("#2324", "statements.md", "SPY0340"),
        new KnownRedSpecCode("#2324", "statements.md", "SPY0600"),
        new KnownRedSpecCode("#2287", "static_methods.md", "SPY0322"),
        new KnownRedSpecCode("#2287", "string_literals.md", "SPY0103"),
        new KnownRedSpecCode("#2287", "structs.md", "SPY0283"),
        new KnownRedSpecCode("#2287", "structs.md", "SPY0400"),
        new KnownRedSpecCode("#2287", "structs.md", "SPY0415"),
        new KnownRedSpecCode("#2287", "structs.md", "SPY0435"),
        new KnownRedSpecCode("#2287", "structs.md", "SPY0616"),
        new KnownRedSpecCode("#2287", "tagged_unions.md", "SPY0102"),
        new KnownRedSpecCode("#2287", "tagged_unions.md", "SPY0200"),
        new KnownRedSpecCode("#2287", "tagged_unions.md", "SPY0212"),
        new KnownRedSpecCode("#2287", "tagged_unions.md", "SPY0227"),
        new KnownRedSpecCode("#2287", "tagged_unions.md", "SPY0401"),
        new KnownRedSpecCode("#2287", "tagged_unions.md", "SPY0453"),
        new KnownRedSpecCode("#2287", "tagged_unions.md", "SPY0485"),
        new KnownRedSpecCode("#2287", "tagged_unions.md", "SPY0608"),
        new KnownRedSpecCode("#2324", "tagged_unions_optional.md", "SPY0212"),
        new KnownRedSpecCode("#2324", "tagged_unions_optional.md", "SPY0220"),
        new KnownRedSpecCode("#2324", "tagged_unions_optional.md", "SPY0326"),
        new KnownRedSpecCode("#2324", "tagged_unions_optional.md", "SPY0480"),
        new KnownRedSpecCode("#2324", "tagged_unions_optional.md", "SPY0498"),
        new KnownRedSpecCode("#2324", "tagged_unions_optional.md", "SPY0604"),
        new KnownRedSpecCode("#2324", "tagged_unions_optional.md", "SPY0608"),
        new KnownRedSpecCode("#2287", "tagged_unions_result.md", "SPY0480"),
        new KnownRedSpecCode("#2287", "template_strings.md", "SPY0203"),
        new KnownRedSpecCode("#2287", "template_strings.md", "SPY0609"),
        new KnownRedSpecCode("#2287", "tuple_unpacking.md", "SPY0225"),
        new KnownRedSpecCode("#2287", "tuple_unpacking.md", "SPY0229"),
        new KnownRedSpecCode("#2287", "tuple_unpacking.md", "SPY0239"),
        new KnownRedSpecCode("#2287", "tuple_unpacking.md", "SPY0338"),
        new KnownRedSpecCode("#2287", "tuple_unpacking.md", "SPY0356"),
        new KnownRedSpecCode("#2287", "tuple_unpacking.md", "SPY0604"),
        new KnownRedSpecCode("#2287", "type_aliases.md", "SPY0212"),
        new KnownRedSpecCode("#2287", "type_annotation_shorthand.md", "SPY0114"),
        new KnownRedSpecCode("#2324", "type_annotations.md", "SPY0227"),
        new KnownRedSpecCode("#2324", "type_annotations.md", "SPY0614"),
        new KnownRedSpecCode("#2287", "type_casting.md", "SPY0243"),
        new KnownRedSpecCode("#2287", "type_casting.md", "SPY0334"),
        new KnownRedSpecCode("#2287", "type_casting.md", "SPY0610"),
        new KnownRedSpecCode("#2324", "type_narrowing.md", "SPY0229"),
        new KnownRedSpecCode("#2324", "type_narrowing.md", "SPY0276"),
        new KnownRedSpecCode("#2324", "type_narrowing.md", "SPY0337"),
        new KnownRedSpecCode("#2324", "type_narrowing.md", "SPY0345"),
        new KnownRedSpecCode("#2324", "type_narrowing.md", "SPY0604"),
        new KnownRedSpecCode("#2287", "type_parameter_defaults.md", "SPY0224"),
        new KnownRedSpecCode("#2287", "type_parameter_defaults.md", "SPY0347"),
        new KnownRedSpecCode("#2287", "type_parameter_defaults.md", "SPY0395"),
        new KnownRedSpecCode("#2287", "type_parameter_defaults.md", "SPY0396"),
        new KnownRedSpecCode("#2287", "type_parameter_defaults.md", "SPY0908"),
        new KnownRedSpecCode("#2287", "typing_equivalences.md", "SPY0149"),
        new KnownRedSpecCode("#2287", "unittest.md", "SPY0448"),
        new KnownRedSpecCode("#2287", "unittest.md", "SPY0449"),
        new KnownRedSpecCode("#2287", "unittest.md", "SPY0469"),
        new KnownRedSpecCode("#2287", "unittest.md", "SPY0522"),
        new KnownRedSpecCode("#2287", "variable_declaration.md", "SPY0104"),
        new KnownRedSpecCode("#2324", "variable_declaration.md", "SPY0331"),
        new KnownRedSpecCode("#2324", "variable_declaration.md", "SPY0340"),
        new KnownRedSpecCode("#2324", "variable_declaration.md", "SPY0600"),
        new KnownRedSpecCode("#2287", "variable_scoping.md", "SPY0134"),
        new KnownRedSpecCode("#2324", "variable_scoping.md", "SPY0200"),
        new KnownRedSpecCode("#2324", "variable_scoping.md", "SPY0290"),
        new KnownRedSpecCode("#2324", "variable_scoping.md", "SPY0468"),
        new KnownRedSpecCode("#2324", "variable_scoping.md", "SPY0606"),
        new KnownRedSpecCode("#2287", "walrus_operator.md", "SPY0600"),
        new KnownRedSpecCode("#2287", "walrus_operator.md", "SPY0704"),
    };

    private static readonly ProseCrossReference[] ProseCrossReferences =
    {
    };

    private static readonly Regex SpecCode = new(@"SPY\d{4}");

    /// <summary>The codes a roster row of <paramref name="file"/> claims: its YInjected codes and the codes
    /// its KnownRedRow observations name.</summary>
    private static HashSet<string> ClaimedCodes(string file) =>
        Rows.Where(r => r.File == file)
            .SelectMany(r => new[] { r.Generated, r.Injected })
            .SelectMany(l => l switch
            {
                YInjected yi => new[] { yi.Code },
                KnownRedRow kr => SpecCode.Matches(kr.Observed).Select(m => m.Value).ToArray(),
                _ => Array.Empty<string>(),
            })
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Every SPY code a spec file's text names is claimed (#2324 worklist item 3 — about 7 of 30 sampled
    /// roster verdicts contradicted their file's own refusal prose): by a YInjected row of that file with the
    /// code, a KnownRedRow of that file whose observation names it, a <see cref="ProseCrossReference"/> to a
    /// file that names and claims it, or an <see cref="UnclaimedSpecCodes"/> cite. Stale rows are red: a
    /// cited pair a row now claims, a pair the file no longer names, a duplicate, a malformed cite.
    /// </summary>
    [Fact]
    public void SpecCodes_EveryNamedCodeIsClaimedByARow()
    {
        var universe = UniverseFiles();
        var named = universe.ToDictionary(f => f,
            f => SpecCode.Matches(File.ReadAllText(Path.Combine(SpecDir, f))).Select(m => m.Value).ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);
        var cited = UnclaimedSpecCodes.ToLookup(r => (r.File, r.Code));
        var prose = ProseCrossReferences.ToLookup(r => (r.File, r.Code));
        var problems = new List<string>();
        int pairsTested = 0, claimed = 0;

        foreach (var (file, codes) in named)
        {
            var claims = ClaimedCodes(file);
            foreach (var code in codes.OrderBy(c => c, StringComparer.Ordinal))
            {
                pairsTested++;
                var byRow = claims.Contains(code);
                if (byRow)
                    claimed++;
                if (byRow && cited.Contains((file, code)))
                    problems.Add($"{file} {code}: a roster row claims it — drop its UnclaimedSpecCodes row (drain on fix)");
                if (!byRow && !cited.Contains((file, code)) && !prose.Contains((file, code)))
                    problems.Add($"{file} names {code} and no roster row claims it — add a lane, a KnownRedRow naming it, or a cited UnclaimedSpecCodes row");
            }
        }

        foreach (var row in UnclaimedSpecCodes)
        {
            if (!IssueCite.IsMatch(row.Issue))
                problems.Add($"UnclaimedSpecCodes ({row.File}, {row.Code}): cite '{row.Issue}' is not #N");
            if (!named.TryGetValue(row.File, out var codes) || !codes.Contains(row.Code))
                problems.Add($"UnclaimedSpecCodes ({row.File}, {row.Code}): the file does not name the code — stale row");
        }
        foreach (var dup in UnclaimedSpecCodes.GroupBy(r => (r.File, r.Code)).Where(g => g.Count() > 1))
            problems.Add($"UnclaimedSpecCodes ({dup.Key.File}, {dup.Key.Code}): {dup.Count()} rows");
        foreach (var row in ProseCrossReferences)
        {
            if (!named.TryGetValue(row.File, out var codes) || !codes.Contains(row.Code))
                problems.Add($"ProseCrossReference ({row.File}, {row.Code}): the file does not name the code");
            if (!named.TryGetValue(row.ReferencedFile, out var referenced) || !referenced.Contains(row.Code)
                || !ClaimedCodes(row.ReferencedFile).Contains(row.Code))
                problems.Add($"ProseCrossReference ({row.File}, {row.Code}) -> {row.ReferencedFile}: that file must name the code and claim it by a row");
        }

        _output.WriteLine($"spec codes: {named.Count(n => n.Value.Count > 0)} files name codes; {pairsTested} (file, code) pairs; "
            + $"{claimed} claimed by a roster row; {UnclaimedSpecCodes.Length} cited ({UnclaimedSpecCodes.Count(r => r.Issue == "#2287")} #2287, "
            + $"{UnclaimedSpecCodes.Count(r => r.Issue == "#2324")} #2324); {ProseCrossReferences.Length} prose cross-reference(s) (measured)");
        Assert.True(pairsTested > 0, "no spec file names a code — the universe or the pattern is broken");
        Assert.True(problems.Count == 0, $"{problems.Count} spec-code problem(s):\n  " + string.Join("\n  ", problems.Take(40)));
    }

    // ---- Totality (Task 4) -------------------------------------------------------------------

    // Counted at landing and bumped in the same commit as the row that moves them. N = lanes citing the
    // generator tracker (#2287 P62; #2324's 62 rows drained by P63b, 2026-10-24); KnownRed = lanes pinning a
    // compiler cell to its tracker; ByDesign and the Y counts are lanes; NoConstruct counts rows.
    private const int ExpectedRows = 202;
    private const int ExpectedYGenerated = 103;
    private const int ExpectedYInjected = 89;
    private const int ExpectedN = 181;
    private const int ExpectedKnownRed = 5;
    private const int ExpectedByDesign = 18;
    private const int ExpectedNoConstruct = 4;

    private static readonly HashSet<string> GeneratorTrackers = new(StringComparer.Ordinal) { "#2287", "#2324" };

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
