namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Detailed explanation for a diagnostic code.
/// </summary>
public class DiagnosticExplanation
{
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Example { get; set; }
    public string? Fix { get; set; }
    public string Category { get; set; } = "";
}

/// <summary>
/// Registry of detailed explanations for Sharpy compiler diagnostic codes.
/// Used by the <c>sharpyc explain</c> command to provide rich error documentation.
/// </summary>
public static partial class DiagnosticExplanations
{
    private static readonly Dictionary<string, DiagnosticExplanation> _explanations = BuildExplanations();

    /// <summary>
    /// Look up an explanation by diagnostic code (case-insensitive).
    /// Returns null if the code is not documented.
    /// </summary>
    public static DiagnosticExplanation? Get(string code)
    {
        _explanations.TryGetValue(code.ToUpperInvariant(), out var explanation);
        return explanation;
    }

    /// <summary>
    /// Returns all documented diagnostic explanations.
    /// </summary>
    public static IReadOnlyDictionary<string, DiagnosticExplanation> GetAll() => _explanations;

    private static Dictionary<string, DiagnosticExplanation> BuildExplanations()
    {
        var dict = new Dictionary<string, DiagnosticExplanation>(StringComparer.OrdinalIgnoreCase);

        AddLexerEntries(dict);
        AddParserEntries(dict);
        AddSemanticEntries(dict);
        AddValidationEntries(dict);

        // ── Code generation errors (SPY0500-SPY0599) ───────────────────

        Add(dict, DiagnosticCodes.CodeGen.EmitError, "Code generation error", "CodeGen",
            "An error occurred during C# code generation. This is typically an internal compiler error that should be reported as a bug.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues with the source file that triggered it.");

        Add(dict, DiagnosticCodes.CodeGen.UnsupportedFeature, "Unsupported feature in code generation", "CodeGen",
            "The code uses a language feature that the code generator does not yet support. The feature is valid Sharpy syntax but cannot be compiled to C# yet.",
            null,
            "Check the language specification for supported features, or file a feature request.");

        Add(dict, DiagnosticCodes.CodeGen.EmptyClassName, "Empty class name in code generation", "CodeGen",
            "The code generator encountered a class with an empty name. This is an internal compiler error.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues.");

        Add(dict, DiagnosticCodes.CodeGen.DuplicateMember, "Duplicate member in generated code", "CodeGen",
            "The code generator detected a duplicate member name in the generated C# class. This can happen when name mangling produces a collision.",
            null,
            "Rename one of the conflicting members to avoid the collision.");

        Add(dict, DiagnosticCodes.CodeGen.EmptyMethodName, "Empty method name in code generation", "CodeGen",
            "The code generator encountered a method with an empty name. This is an internal compiler error.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues.");

        Add(dict, DiagnosticCodes.CodeGen.AbstractMethodWithBody, "Abstract method with body in code generation", "CodeGen",
            "The code generator encountered an abstract method that has a body. Abstract methods should not have implementations.",
            null,
            "This is an internal compiler error. The semantic analyzer should have caught this. Report it at https://github.com/antonsynd/sharpy/issues.");

        Add(dict, DiagnosticCodes.CodeGen.NonAbstractMethodWithoutBody, "Non-abstract method without body", "CodeGen",
            "The code generator encountered a concrete (non-abstract) method that has no body. Only abstract and interface methods can omit the body.",
            null,
            "This is an internal compiler error. The semantic analyzer should have caught this. Report it at https://github.com/antonsynd/sharpy/issues.");

        Add(dict, DiagnosticCodes.CodeGen.VarWithoutInitializer, "Variable without initializer in code generation", "CodeGen",
            "The code generator encountered a variable declaration without an initializer. All variables should have initializers by this point in the compilation pipeline.",
            null,
            "This is an internal compiler error. Report it at https://github.com/antonsynd/sharpy/issues.");

        Add(dict, DiagnosticCodes.CodeGen.PositionalPatternFallback, "Positional pattern Deconstruct fallback", "CodeGen",
            "The code generator is emitting a positional pattern as a Deconstruct call. " +
            "This is a defensive warning — the semantic layer should have caught types without Deconstruct (SPY0369).",
            null,
            "This is an internal compiler warning. If Deconstruct is missing, check that type checking caught it.");

        Add(dict, DiagnosticCodes.CodeGen.UnrecognizedStatementType, "Unrecognized statement type not emitted", "CodeGen",
            "The code generator encountered a statement type that it does not know how to emit. " +
            "The statement was silently skipped, meaning the generated code does not include it. " +
            "This is a compiler bug — the code generator is missing a handler for this AST node type.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        Add(dict, DiagnosticCodes.CodeGen.UnsupportedExpressionType, "Unsupported expression type in code generation", "CodeGen",
            "The code generator encountered an expression or statement type that it does not know how to emit. " +
            "This is either a not-yet-implemented feature or a compiler bug.",
            null,
            "If you believe this is valid Sharpy code, report it at https://github.com/antonsynd/sharpy/issues.");

        Add(dict, DiagnosticCodes.CodeGen.UnsupportedOperator, "Unsupported operator in code generation", "CodeGen",
            "The code generator encountered an operator that it does not know how to emit. " +
            "This is either a not-yet-implemented operator or a compiler bug.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        Add(dict, DiagnosticCodes.CodeGen.NameCollision, "Module class name collision", "CodeGen",
            "A type name in the source file matches the module class name derived from the file name. " +
            "For class types, the class absorbs module-level members and becomes the module representative. " +
            "For struct, interface, or enum types, this is an error because they cannot serve as module classes.",
            "# File: animal.spy\n# Module class name would be 'Animal', but 'struct Animal' collides\nstruct Animal:\n    name: str",
            "Rename the type or the source file so that the type name does not match the file name in PascalCase.");

        Add(dict, DiagnosticCodes.CodeGen.MemberNameCollision, "Member name collision after mangling", "CodeGen",
            "Two symbols in the same scope produce the same C# name after name mangling. " +
            "For example, 'foo_bar' and 'FooBar' both compile to 'FooBar'.",
            "class Foo:\n    def foo_bar(self): ...\n    def FooBar(self): ...",
            "Rename one of the conflicting symbols or use backtick escaping.");

        Add(dict, DiagnosticCodes.CodeGen.FunctionModuleClassCollision, "Function name collides with module class name", "CodeGen",
            "A module-level function's mangled name matches the module class name derived from the source filename. " +
            "In C#, a member cannot have the same name as its enclosing type (CS0542).",
            "# File: bubble_sort.spy\ndef bubble_sort(arr: list[int]) -> list[int]:\n    ...\n# 'bubble_sort' compiles to 'BubbleSort', same as class 'BubbleSort' from filename",
            "Rename the function or the source file so the function's PascalCase name does not match the filename's PascalCase name.");

        Add(dict, DiagnosticCodes.CodeGen.TypeReExportNotSupported, "Type re-export not supported", "CodeGen",
            "A type cannot be re-exported from an __init__.spy package file. Types should be imported directly " +
            "from their defining module rather than re-exported through package init files.",
            "# __init__.spy\nfrom .helpers import MyClass  # MyClass is a type — cannot re-export",
            "Import the type directly from its defining module instead of through the package init file.");

        Add(dict, DiagnosticCodes.CodeGen.InternalGeneratedCSharpParseError, "Internal error: generated C# contains syntax errors", "CodeGen",
            "The compiler generated C# code that fails to parse. This indicates a bug in the Sharpy compiler's code generation phase. " +
            "The generated C# has syntax errors that would prevent compilation.",
            null,
            "This is an internal compiler error. Please report it at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        // ── Source generator errors (SPY0550-SPY0569) ────────────────────

        Add(dict, DiagnosticCodes.CodeGen.GeneratorExecutionError, "Source generator execution error", "CodeGen",
            "A source generator threw an exception during execution. The generator class was instantiated " +
            "and its Generate() method was called, but it failed with an unhandled exception.",
            "@[MyGenerator]\nclass Foo:\n    x: int",
            "Fix the exception in the source generator's Generate() method. Check the error message for details.");

        Add(dict, DiagnosticCodes.CodeGen.GeneratorTimeout, "Source generator timed out", "CodeGen",
            "A source generator did not complete within the allowed time limit (30 seconds). " +
            "This usually indicates an infinite loop or very expensive computation in the generator.",
            null,
            "Optimize the source generator or break it into smaller generators.");

        Add(dict, DiagnosticCodes.CodeGen.GeneratorInvalidSource, "Source generator produced invalid Sharpy source", "CodeGen",
            "A source generator returned Sharpy source code that contains syntax errors. " +
            "The generated source is re-parsed and must be valid Sharpy syntax.",
            null,
            "Fix the source generator to produce valid Sharpy syntax. Inspect the generated source for errors.");

        Add(dict, DiagnosticCodes.CodeGen.GeneratorCycleDetected, "Source generator cycle detected", "CodeGen",
            "A source generator attribute was applied to another source generator class, creating a cycle. " +
            "Generators cannot decorate other generators.",
            "@[GenerateRepr]\nclass MyGenerator(SourceGenerator):\n    def generate(self, context: GeneratorContext) -> GeneratorOutput: ...",
            "Remove the generator attribute from the generator class.");

        Add(dict, DiagnosticCodes.CodeGen.GeneratorEmptyOutput, "Source generator returned empty output", "CodeGen",
            "A source generator returned null or an empty source string. This is a warning — " +
            "the generator was invoked but produced no code.",
            null,
            "Check the generator's Generate() method to ensure it returns non-empty source code.");

        // ── Infrastructure errors (SPY0900-SPY0999) ────────────────────

        Add(dict, DiagnosticCodes.Infrastructure.CompilationFailed, "Compilation failed", "Infrastructure",
            "The overall compilation process failed. This is a summary error that accompanies more specific errors from earlier phases.",
            null,
            "Fix the errors reported in earlier phases (lexer, parser, semantic, or code generation).");

        Add(dict, DiagnosticCodes.Infrastructure.CompilationCancelled, "Compilation cancelled", "Infrastructure",
            "The compilation was cancelled, either by user request or by a timeout. No output was produced.",
            null,
            "Re-run the compilation. If it keeps timing out, check for very large files or circular dependencies.");

        Add(dict, DiagnosticCodes.Infrastructure.AssemblyCompilationFailed, "Assembly compilation failed", "Infrastructure",
            "The Roslyn C# compilation of the generated code failed. This means the compiler produced C# code that the .NET compiler could not compile.",
            null,
            "This is likely an internal compiler error. Report it at https://github.com/antonsynd/sharpy/issues with the source file.");

        Add(dict, DiagnosticCodes.Infrastructure.FileReadError, "File read error", "Infrastructure",
            "A source file could not be read from disk. This may be due to missing files, permission issues, or invalid file paths.",
            null,
            "Verify the file exists, the path is correct, and you have read permissions.");

        Add(dict, DiagnosticCodes.Infrastructure.InvariantViolation, "Internal invariant violation", "Infrastructure",
            "An internal compiler invariant was violated. This is a compiler bug — " +
            "the semantic pipeline produced data that fails a post-phase consistency check. " +
            "The compilation may still succeed, but the generated code could be incorrect.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        Add(dict, DiagnosticCodes.Infrastructure.TooManyErrors, "Too many errors", "Infrastructure",
            "The compiler stopped reporting errors because the maximum error limit was reached. " +
            "Additional errors may exist but were suppressed. The reported errors should be fixed first, " +
            "as later errors are often caused by earlier ones.",
            null,
            "Fix the reported errors and re-compile. Use '--max-errors N' to increase the limit if needed.");

        Add(dict, DiagnosticCodes.Infrastructure.ParserLoopStall, "Parser loop stall detected", "Infrastructure",
            "The parser detected that it made no progress in a parsing loop. This is a safety mechanism " +
            "that prevents the parser from hanging on malformed input. The parser forcibly advanced past " +
            "the problematic token to continue parsing. This warning indicates the input may be malformed " +
            "or there is an edge case in the parser that should be reported.",
            null,
            "Check the source code at the indicated location for syntax errors. If the input looks correct, " +
            "report this at https://github.com/antonsynd/sharpy/issues with the source file.");

        Add(dict, DiagnosticCodes.Infrastructure.UnexpectedUnknownType, "Unexpected unknown type", "Infrastructure",
            "Type inference produced an UnknownType for an expression without a corresponding error diagnostic. " +
            "This indicates a gap in the type checker where a type could not be resolved but no user-facing error " +
            "was emitted. This is distinct from error-recovery Unknown types, which are expected when the user " +
            "writes invalid code.",
            null,
            "Report this at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        // ── Informational diagnostics (SPY1000-SPY1099) ────────────────────

        Add(dict, DiagnosticCodes.Info.ImplicitInterfaceSynthesis, "Implicit interface synthesis", "CodeGen",
            "The compiler automatically added a .NET interface to the generated class because the class defines " +
            "a dunder method that maps to that interface. For example, defining __len__ causes the class to " +
            "implement ISized, and defining __bool__ causes it to implement IBoolConvertible.",
            null,
            "This is informational only. No action is required. The synthesized interface enables interop with " +
            ".NET code that expects the interface (e.g., len() dispatch via ISized).");

        Add(dict, DiagnosticCodes.Info.FunctoolsPartialPlaceholderHint, "Prefer '_' placeholder over functools.partial", "Semantic",
            "Sharpy supports Python's functools.partial as a compatibility shim that desugars to an equivalent " +
            "underscore-placeholder lambda. The idiomatic Sharpy form uses the '_' placeholder directly, which is " +
            "more concise and avoids the extra import.",
            "functools.partial(add, 5)   # compatibility shim\n" +
            "add(5, _)                   # idiomatic Sharpy",
            "Replace 'functools.partial(f, fixed_args...)' with 'f(fixed_args..., _, ...)' using '_' for the " +
            "remaining (unfixed) arguments. The two forms are equivalent at runtime.");

        // ── Import redirect diagnostics (SPY0310-SPY0311) ────────────────

        Add(dict, DiagnosticCodes.Semantic.TypingModuleRedirect, "typing module not needed", "Semantic",
            "The Python 'typing' module is not needed in Sharpy. All common typing constructs have native syntax equivalents: " +
            "'X?' for Optional, 'list[X]' for List, 'dict[K,V]' for Dict, '(X) -> Y' for Callable, 'interface' for Protocol, etc.",
            "from typing import Optional\n\nx: Optional[int] = None",
            "Remove the import and use native type syntax:\n  x: int? = None");

        Add(dict, DiagnosticCodes.Semantic.DataclassesModuleRedirect, "dataclasses module not needed", "Semantic",
            "The Python 'dataclasses' module is not needed in Sharpy. The '@dataclass' decorator is a native language feature " +
            "that requires no import.",
            "from dataclasses import dataclass\n\n@dataclass\nclass Point:\n    x: float\n    y: float",
            "Remove the import — '@dataclass' works directly:\n  @dataclass\n  class Point:\n      x: float\n      y: float");

        return dict;
    }

    private static void Add(
        Dictionary<string, DiagnosticExplanation> dict,
        string code,
        string title,
        string category,
        string description,
        string? example,
        string? fix)
    {
        dict[code] = new DiagnosticExplanation
        {
            Code = code,
            Title = title,
            Description = description,
            Example = example,
            Fix = fix,
            Category = category
        };
    }
}
