using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validation for the @test family of decorators: @test, @test.parametrize,
/// @test.skip, @test.skip_if, @test.fixture, @test.mark, and @test.collection.
/// </summary>
internal partial class DecoratorValidator
{
    /// <summary>
    /// Validates @test decorator on function/method declarations.
    /// Rules:
    /// - Cannot combine with @abstract, @virtual, or @static
    /// - Cannot be applied to dunder methods
    /// - Accepts zero or one positional string argument (description)
    /// </summary>
    private void ValidateTestDecorator(IReadOnlyList<Decorator> decorators, string functionName, bool isDunder)
    {
        var testDecorator = decorators.FirstOrDefault(d => d.Name == DecoratorNames.Test);
        if (testDecorator == null)
            return;

        // @test on dunder methods
        if (isDunder)
        {
            AddError(
                $"'@test' cannot be applied to dunder method '{functionName}'. " +
                "Test decorators are only valid on regular methods.",
                testDecorator.LineStart,
                testDecorator.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidTarget,
                span: testDecorator.Span);
            return;
        }

        // @test + @abstract/@virtual/@static
        foreach (var decorator in decorators)
        {
            if (decorator.Name == DecoratorNames.Abstract
                || decorator.Name == DecoratorNames.Virtual
                || decorator.Name == DecoratorNames.Static)
            {
                AddError(
                    $"'@test' cannot be combined with '@{decorator.Name}' on '{functionName}'. " +
                    "Test methods must be concrete instance methods.",
                    testDecorator.LineStart,
                    testDecorator.ColumnStart,
                    code: DiagnosticCodes.Validation.TestDecoratorInvalidCombination,
                    span: testDecorator.Span);
                return;
            }
        }

        // Argument validation: 0 or 1 positional string arg
        if (testDecorator.Arguments.Length > 1)
        {
            AddWarning(
                $"'@test' on '{functionName}' accepts at most one string argument (description).",
                testDecorator.LineStart,
                testDecorator.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: testDecorator.Span);
        }
        else if (testDecorator.Arguments.Length == 1 && testDecorator.Arguments[0] is not StringLiteral)
        {
            AddWarning(
                $"'@test' argument on '{functionName}' must be a string literal (description).",
                testDecorator.Arguments[0].LineStart,
                testDecorator.Arguments[0].ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: testDecorator.Arguments[0].Span);
        }

        if (testDecorator.KeywordArguments.Length > 0)
        {
            AddWarning(
                $"'@test' on '{functionName}' does not accept keyword arguments.",
                testDecorator.KeywordArguments[0].Value.LineStart,
                testDecorator.KeywordArguments[0].Value.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: testDecorator.KeywordArguments[0].Value.Span);
        }
    }

    /// <summary>
    /// Validates that @test (and its sub-decorators like @test.parametrize) is not applied to type
    /// definitions (classes, structs, interfaces, enums) or to properties/events.
    /// </summary>
    private void ValidateTestDecoratorNotOnType(IEnumerable<Decorator> decorators, string typeName, string typeKind)
    {
        foreach (var decorator in decorators)
        {
            if (!DecoratorNames.KnownTestDecorators.Contains(decorator.Name))
                continue;

            AddError(
                $"'@{decorator.Name}' cannot be applied to {typeKind} '{typeName}'. " +
                "Test decorators are only valid on function and method declarations.",
                decorator.LineStart,
                decorator.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidTarget,
                span: decorator.Span);
        }
    }

    /// <summary>
    /// Validates @test.parametrize decorator on a function definition.
    /// Rules:
    /// - Must take exactly one positional argument (a list literal of tuples).
    /// - Each tuple element must have the same arity as the function's parameters
    ///   (excluding 'self' for methods).
    /// - Cannot be combined with plain @test (it implies @test by itself).
    /// </summary>
    private void ValidateTestParametrizeDecorator(FunctionDef function, string definitionName)
    {
        var parametrize = function.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.TestParametrize);
        if (parametrize == null)
            return;

        // Cannot combine with plain @test
        var plainTest = function.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.Test);
        if (plainTest != null)
        {
            AddError(
                $"'@test.parametrize' cannot be combined with '@test' on '{definitionName}'. " +
                "'@test.parametrize' already marks the function as a test.",
                parametrize.LineStart,
                parametrize.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidCombination,
                span: parametrize.Span);
        }

        // Keyword arguments are not supported
        if (parametrize.KeywordArguments.Length > 0)
        {
            AddWarning(
                $"'@test.parametrize' on '{definitionName}' does not accept keyword arguments.",
                parametrize.KeywordArguments[0].Value.LineStart,
                parametrize.KeywordArguments[0].Value.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: parametrize.KeywordArguments[0].Value.Span);
        }

        // Must take exactly one positional argument
        if (parametrize.Arguments.Length != 1)
        {
            AddWarning(
                $"'@test.parametrize' on '{definitionName}' requires exactly one argument: " +
                "a list of tuples with parameter values.",
                parametrize.LineStart,
                parametrize.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: parametrize.Span);
            return;
        }

        var argument = parametrize.Arguments[0];

        // Variable reference form: @test.parametrize(TEST_DATA) — the data lives in a
        // module-level variable. Validate the name resolves; row arity cannot be checked
        // statically here (the emitter generates a MemberData wrapper that defers to xUnit).
        if (argument is Identifier identifier)
        {
            var referenced = Context.SymbolTable.Lookup(identifier.Name);
            if (referenced == null)
            {
                AddError(
                    $"'@test.parametrize' on '{definitionName}' references undefined variable " +
                    $"'{identifier.Name}'.",
                    identifier.LineStart,
                    identifier.ColumnStart,
                    code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                    span: identifier.Span);
            }
            return;
        }

        if (argument is not ListLiteral listLiteral)
        {
            AddWarning(
                $"'@test.parametrize' argument on '{definitionName}' must be a list literal " +
                "of tuples (e.g. [(1, 2), (3, 4)]) or a reference to a module-level variable.",
                argument.LineStart,
                argument.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: argument.Span);
            return;
        }

        // Compute the expected arity: number of function parameters, excluding 'self' on methods.
        int expectedArity = function.Parameters.Length;
        if (_containingType != null && function.Parameters.Length > 0
            && function.Parameters[0].Name == PythonNames.Self)
        {
            expectedArity = function.Parameters.Length - 1;
        }

        for (int i = 0; i < listLiteral.Elements.Length; i++)
        {
            var element = listLiteral.Elements[i];
            if (element is not TupleLiteral tuple)
            {
                // A single-parameter function may use a flat list of values (e.g. [1, 2, 3]).
                // Otherwise require a tuple.
                if (expectedArity != 1)
                {
                    AddWarning(
                        $"'@test.parametrize' element {i} on '{definitionName}' must be a tuple " +
                        $"with {expectedArity} values (one per parameter).",
                        element.LineStart,
                        element.ColumnStart,
                        code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                        span: element.Span);
                }
                continue;
            }

            if (tuple.Elements.Length != expectedArity)
            {
                AddWarning(
                    $"'@test.parametrize' element {i} on '{definitionName}' has " +
                    $"{tuple.Elements.Length} value(s); expected {expectedArity} " +
                    "(one per function parameter).",
                    element.LineStart,
                    element.ColumnStart,
                    code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                    span: element.Span);
            }
        }
    }

    /// <summary>
    /// Validates @test.skip and @test.skip_if decorators on function definitions.
    /// Rules:
    /// - @test.skip(reason) takes exactly one string argument.
    /// - @test.skip_if(condition, reason) takes exactly two arguments; the second must be a string.
    /// - These decorators can be combined with @test or @test.parametrize.
    /// - Cannot be applied to type definitions (handled by ValidateTestDecoratorNotOnType).
    /// </summary>
    private void ValidateTestSkipDecorators(IReadOnlyList<Decorator> decorators, string definitionName)
    {
        foreach (var decorator in decorators)
        {
            if (decorator.Name == DecoratorNames.TestSkip)
            {
                ValidateTestSkipDecorator(decorator, definitionName);
            }
            else if (decorator.Name == DecoratorNames.TestSkipIf)
            {
                ValidateTestSkipIfDecorator(decorator, definitionName);
            }
        }
    }

    private void ValidateTestSkipDecorator(Decorator decorator, string definitionName)
    {
        if (decorator.KeywordArguments.Length > 0)
        {
            AddWarning(
                $"'@test.skip' on '{definitionName}' does not accept keyword arguments.",
                decorator.KeywordArguments[0].Value.LineStart,
                decorator.KeywordArguments[0].Value.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: decorator.KeywordArguments[0].Value.Span);
        }

        if (decorator.Arguments.Length != 1)
        {
            AddWarning(
                $"'@test.skip' on '{definitionName}' requires exactly one string argument: " +
                "@test.skip(\"reason\").",
                decorator.LineStart,
                decorator.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: decorator.Span);
            return;
        }

        if (decorator.Arguments[0] is not StringLiteral)
        {
            AddWarning(
                $"'@test.skip' argument on '{definitionName}' must be a string literal (the skip reason).",
                decorator.Arguments[0].LineStart,
                decorator.Arguments[0].ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: decorator.Arguments[0].Span);
        }
    }

    private void ValidateTestSkipIfDecorator(Decorator decorator, string definitionName)
    {
        if (decorator.KeywordArguments.Length > 0)
        {
            AddWarning(
                $"'@test.skip_if' on '{definitionName}' does not accept keyword arguments.",
                decorator.KeywordArguments[0].Value.LineStart,
                decorator.KeywordArguments[0].Value.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: decorator.KeywordArguments[0].Value.Span);
        }

        if (decorator.Arguments.Length != 2)
        {
            AddWarning(
                $"'@test.skip_if' on '{definitionName}' requires exactly two arguments: " +
                "@test.skip_if(condition, \"reason\").",
                decorator.LineStart,
                decorator.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: decorator.Span);
            return;
        }

        if (decorator.Arguments[1] is not StringLiteral)
        {
            AddWarning(
                $"'@test.skip_if' second argument on '{definitionName}' must be a string literal (the skip reason).",
                decorator.Arguments[1].LineStart,
                decorator.Arguments[1].ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: decorator.Arguments[1].Span);
        }
    }

    /// <summary>
    /// Validates @test.fixture decorator on a function definition.
    /// Rules:
    /// - Must be applied to a free function, not a method inside a class/struct/interface.
    /// - Must declare a return type annotation.
    /// - If the fixture body uses yield (setup/teardown pattern), there must be exactly one yield.
    /// - Cannot be combined with @test, @test.parametrize, @test.skip, or @test.skip_if.
    /// </summary>
    private void ValidateTestFixtureDecorator(FunctionDef function, string definitionName, bool isInsideType)
    {
        var fixture = function.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.TestFixture);
        if (fixture == null)
            return;

        // Fixtures must be free functions.
        if (isInsideType)
        {
            AddError(
                $"'@test.fixture' cannot be applied to method '{definitionName}'. " +
                "Fixtures must be free functions defined at module level.",
                fixture.LineStart,
                fixture.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidTarget,
                span: fixture.Span);
        }

        // Fixtures cannot be combined with other test-family decorators.
        foreach (var other in function.Decorators)
        {
            if (other.Name == DecoratorNames.TestFixture)
                continue;

            if (other.Name == DecoratorNames.Test
                || other.Name == DecoratorNames.TestParametrize
                || other.Name == DecoratorNames.TestSkip
                || other.Name == DecoratorNames.TestSkipIf)
            {
                AddError(
                    $"'@test.fixture' cannot be combined with '@{other.Name}' on '{definitionName}'. " +
                    "Fixtures are setup helpers, not tests.",
                    fixture.LineStart,
                    fixture.ColumnStart,
                    code: DiagnosticCodes.Validation.TestDecoratorInvalidCombination,
                    span: fixture.Span);
                break;
            }
        }

        // Fixtures should not take any decorator arguments themselves.
        if (fixture.Arguments.Length > 0 || fixture.KeywordArguments.Length > 0)
        {
            AddWarning(
                $"'@test.fixture' on '{definitionName}' does not accept arguments.",
                fixture.LineStart,
                fixture.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: fixture.Span);
        }

        // Must declare a return type annotation.
        if (function.ReturnType == null)
        {
            AddWarning(
                $"'@test.fixture' on '{definitionName}' must declare a return type annotation. " +
                "The return type defines the value injected into tests that consume the fixture.",
                fixture.LineStart,
                fixture.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: fixture.Span);
        }

        // If the fixture uses yield, there must be exactly one yield point.
        int yieldCount = CountYieldStatements(function.Body);
        if (yieldCount > 1)
        {
            AddError(
                $"'@test.fixture' '{definitionName}' contains {yieldCount} yield statements; " +
                "fixtures must have exactly one yield (setup before, teardown after).",
                fixture.LineStart,
                fixture.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidCombination,
                span: fixture.Span);
        }
    }

    /// <summary>
    /// Returns true if the decorator list contains any decorator that registers the function
    /// as a test (e.g., @test or @test.parametrize). Used to determine whether @test.mark
    /// is being applied to a real test function.
    /// </summary>
    private static bool HasAnyTestMarker(IEnumerable<Decorator> decorators)
    {
        foreach (var decorator in decorators)
        {
            if (decorator.Name == DecoratorNames.Test
                || decorator.Name == DecoratorNames.TestParametrize)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Validates @test.mark decorator usage on function definitions.
    /// Rules:
    /// - Takes exactly one positional string argument (the trait category name).
    /// - No keyword arguments.
    /// - Multiple @test.mark decorators may be applied (each maps to a [Trait]).
    /// - Should be combined with @test or @test.parametrize; otherwise warn.
    /// </summary>
    private void ValidateTestMarkDecorators(IReadOnlyList<Decorator> decorators, string definitionName, bool hasTestDecorator)
    {
        foreach (var decorator in decorators)
        {
            if (decorator.Name != DecoratorNames.TestMark)
                continue;

            if (decorator.KeywordArguments.Length > 0)
            {
                AddWarning(
                    $"'@test.mark' on '{definitionName}' does not accept keyword arguments.",
                    decorator.KeywordArguments[0].Value.LineStart,
                    decorator.KeywordArguments[0].Value.ColumnStart,
                    code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                    span: decorator.KeywordArguments[0].Value.Span);
            }

            if (decorator.Arguments.Length != 1)
            {
                AddWarning(
                    $"'@test.mark' on '{definitionName}' requires exactly one string argument: " +
                    "@test.mark(\"category\").",
                    decorator.LineStart,
                    decorator.ColumnStart,
                    code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                    span: decorator.Span);
                continue;
            }

            if (decorator.Arguments[0] is not StringLiteral)
            {
                AddWarning(
                    $"'@test.mark' argument on '{definitionName}' must be a string literal (the category name).",
                    decorator.Arguments[0].LineStart,
                    decorator.Arguments[0].ColumnStart,
                    code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                    span: decorator.Arguments[0].Span);
            }

            if (!hasTestDecorator)
            {
                AddWarning(
                    $"'@test.mark' on '{definitionName}' has no effect without '@test' or '@test.parametrize'. " +
                    "Add a test marker decorator to register this function as a test.",
                    decorator.LineStart,
                    decorator.ColumnStart,
                    code: DiagnosticCodes.Validation.TestDecoratorInvalidCombination,
                    span: decorator.Span);
            }
        }
    }

    /// <summary>
    /// Validates @test.collection decorator on type declarations.
    /// Rules:
    /// - Only valid on classes (not structs, interfaces, enums, or functions).
    /// - Takes exactly one positional string argument (the collection name).
    /// - No keyword arguments.
    /// </summary>
    private void ValidateTestCollectionDecorator(IReadOnlyList<Decorator> decorators, string definitionName, string definitionKind, bool allowOnThisKind)
    {
        foreach (var decorator in decorators)
        {
            if (decorator.Name != DecoratorNames.TestCollection)
                continue;

            if (!allowOnThisKind)
            {
                AddError(
                    $"'@test.collection' cannot be applied to {definitionKind} '{definitionName}'. " +
                    "Test collection grouping is only valid on classes.",
                    decorator.LineStart,
                    decorator.ColumnStart,
                    code: DiagnosticCodes.Validation.TestDecoratorInvalidTarget,
                    span: decorator.Span);
                continue;
            }

            if (decorator.KeywordArguments.Length > 0)
            {
                AddWarning(
                    $"'@test.collection' on '{definitionName}' does not accept keyword arguments.",
                    decorator.KeywordArguments[0].Value.LineStart,
                    decorator.KeywordArguments[0].Value.ColumnStart,
                    code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                    span: decorator.KeywordArguments[0].Value.Span);
            }

            if (decorator.Arguments.Length != 1)
            {
                AddWarning(
                    $"'@test.collection' on '{definitionName}' requires exactly one string argument: " +
                    "@test.collection(\"name\").",
                    decorator.LineStart,
                    decorator.ColumnStart,
                    code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                    span: decorator.Span);
                continue;
            }

            if (decorator.Arguments[0] is not StringLiteral)
            {
                AddWarning(
                    $"'@test.collection' argument on '{definitionName}' must be a string literal (the collection name).",
                    decorator.Arguments[0].LineStart,
                    decorator.Arguments[0].ColumnStart,
                    code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                    span: decorator.Arguments[0].Span);
            }
        }
    }

    /// <summary>
    /// Counts the number of YieldStatement nodes anywhere in the given block of statements.
    /// Does not descend into nested FunctionDef/ClassDef bodies (those have their own scope).
    /// </summary>
    // Only counts top-level yield statements in the body. Does not descend into nested
    // blocks (if/for/while) because the fixture emitter splits the body at the yield index
    // and cannot handle yields inside control flow.
    private static int CountYieldStatements(IReadOnlyCollection<Statement> body)
    {
        int count = 0;
        foreach (var stmt in body)
        {
            if (stmt is YieldStatement)
                count++;
        }
        return count;
    }
}
