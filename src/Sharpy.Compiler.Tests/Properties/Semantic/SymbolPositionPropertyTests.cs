using CsCheck;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class SymbolPositionPropertyTests
{
    private readonly ITestOutputHelper _output;

    public SymbolPositionPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SymbolPositions_AreWithinSourceBounds()
    {
        var gen = SemanticFilter.WellTypedProgram(
            Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
                GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2)));

        gen.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Analyze(source, "position_test.spy");

            if (!result.Success || result.SymbolTable == null)
                return;

            var lineCount = source.Split('\n').Length;
            var errors = new List<string>();

            foreach (var symbol in result.SymbolTable.GetAllModuleScopeSymbols())
            {
                AssertPositionWithinBounds(symbol, lineCount, errors);
            }

            if (errors.Count > 0)
            {
                throw new Exception(
                    $"Position bound violations ({errors.Count}):\n" +
                    $"  {string.Join("\n  ", errors.Take(10))}\n" +
                    $"Source:\n{source}");
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
    }

    [Fact]
    public void NameDeclarationPosition_IsAtOrAfterDeclarationPosition()
    {
        var gen = SemanticFilter.WellTypedProgram(
            Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
                GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2)));

        gen.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Analyze(source, "position_test.spy");

            if (!result.Success || result.SymbolTable == null)
                return;

            var errors = new List<string>();

            foreach (var symbol in result.SymbolTable.GetAllModuleScopeSymbols())
            {
                AssertNameAfterDeclaration(symbol, errors);
            }

            if (errors.Count > 0)
            {
                throw new Exception(
                    $"Name-before-declaration violations ({errors.Count}):\n" +
                    $"  {string.Join("\n  ", errors.Take(10))}\n" +
                    $"Source:\n{source}");
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
    }

    [Fact]
    public void EffectiveNamePosition_FallsBackCorrectly()
    {
        var gen = SemanticFilter.WellTypedProgram(
            Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
                GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2)));

        gen.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Analyze(source, "position_test.spy");

            if (!result.Success || result.SymbolTable == null)
                return;

            var errors = new List<string>();

            foreach (var symbol in result.SymbolTable.GetAllModuleScopeSymbols())
            {
                // EffectiveNameLine fallback
                if (!symbol.NameDeclarationLine.HasValue)
                {
                    if (symbol.EffectiveNameLine != symbol.DeclarationLine)
                    {
                        errors.Add(
                            $"'{symbol.Name}': EffectiveNameLine={symbol.EffectiveNameLine} " +
                            $"should equal DeclarationLine={symbol.DeclarationLine} " +
                            $"when NameDeclarationLine is null");
                    }
                }
                else
                {
                    if (symbol.EffectiveNameLine != symbol.NameDeclarationLine)
                    {
                        errors.Add(
                            $"'{symbol.Name}': EffectiveNameLine={symbol.EffectiveNameLine} " +
                            $"should equal NameDeclarationLine={symbol.NameDeclarationLine}");
                    }
                }

                // EffectiveNameColumn fallback
                if (!symbol.NameDeclarationColumn.HasValue)
                {
                    if (symbol.EffectiveNameColumn != symbol.DeclarationColumn)
                    {
                        errors.Add(
                            $"'{symbol.Name}': EffectiveNameColumn={symbol.EffectiveNameColumn} " +
                            $"should equal DeclarationColumn={symbol.DeclarationColumn} " +
                            $"when NameDeclarationColumn is null");
                    }
                }
                else
                {
                    if (symbol.EffectiveNameColumn != symbol.NameDeclarationColumn)
                    {
                        errors.Add(
                            $"'{symbol.Name}': EffectiveNameColumn={symbol.EffectiveNameColumn} " +
                            $"should equal NameDeclarationColumn={symbol.NameDeclarationColumn}");
                    }
                }
            }

            if (errors.Count > 0)
            {
                throw new Exception(
                    $"EffectiveNamePosition fallback violations ({errors.Count}):\n" +
                    $"  {string.Join("\n  ", errors.Take(10))}\n" +
                    $"Source:\n{source}");
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
    }

    [Fact]
    public void SymbolPositions_WithClasses()
    {
        var gen = SemanticFilter.WellTypedProgram(
            GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2));

        gen.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Analyze(source, "position_test.spy");

            if (!result.Success || result.SymbolTable == null)
                return;

            var lineCount = source.Split('\n').Length;
            var errors = new List<string>();
            var typeSymbolCount = 0;
            var functionSymbolCount = 0;
            var variableSymbolCount = 0;

            foreach (var symbol in result.SymbolTable.GetAllModuleScopeSymbols())
            {
                switch (symbol)
                {
                    case TypeSymbol:
                        typeSymbolCount++;
                        break;
                    case FunctionSymbol:
                        functionSymbolCount++;
                        break;
                    case VariableSymbol:
                        variableSymbolCount++;
                        break;
                }

                AssertPositionWithinBounds(symbol, lineCount, errors);
                AssertNameAfterDeclaration(symbol, errors);
            }

            if (errors.Count > 0)
            {
                throw new Exception(
                    $"Class position violations ({errors.Count}), " +
                    $"symbols: {typeSymbolCount} types, {functionSymbolCount} functions, " +
                    $"{variableSymbolCount} variables:\n" +
                    $"  {string.Join("\n  ", errors.Take(10))}\n" +
                    $"Source:\n{source}");
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 20);
    }

    [Fact]
    public void DeclarationSpan_IsConsistentWithLineColumn()
    {
        var gen = SemanticFilter.WellTypedProgram(
            Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
                GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2)));

        gen.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Analyze(source, "position_test.spy");

            if (!result.Success || result.SymbolTable == null)
                return;

            var errors = new List<string>();

            foreach (var symbol in result.SymbolTable.GetAllModuleScopeSymbols())
            {
                if (symbol.DeclarationSpan == null ||
                    !symbol.DeclarationLine.HasValue ||
                    !symbol.DeclarationColumn.HasValue)
                    continue;

                var offset = LineColumnToOffset(
                    source, symbol.DeclarationLine.Value, symbol.DeclarationColumn.Value);
                var span = symbol.DeclarationSpan.Value;

                if (offset < span.Start || offset >= span.End)
                {
                    errors.Add(
                        $"'{symbol.Name}': computed offset {offset} " +
                        $"(line {symbol.DeclarationLine}, col {symbol.DeclarationColumn}) " +
                        $"not within DeclarationSpan [{span.Start}..{span.End})");
                }
            }

            if (errors.Count > 0)
            {
                throw new Exception(
                    $"DeclarationSpan/LineColumn inconsistencies ({errors.Count}):\n" +
                    $"  {string.Join("\n  ", errors.Take(10))}\n" +
                    $"Source:\n{source}");
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 30);
    }

    private static void AssertPositionWithinBounds(Symbol symbol, int lineCount, List<string> errors)
    {
        if (symbol.DeclarationLine.HasValue)
        {
            if (symbol.DeclarationLine.Value < 1)
            {
                errors.Add(
                    $"'{symbol.Name}': DeclarationLine={symbol.DeclarationLine} < 1");
            }
            if (symbol.DeclarationLine.Value > lineCount)
            {
                errors.Add(
                    $"'{symbol.Name}': DeclarationLine={symbol.DeclarationLine} > lineCount={lineCount}");
            }
        }

        if (symbol.DeclarationColumn.HasValue && symbol.DeclarationColumn.Value < 0)
        {
            errors.Add(
                $"'{symbol.Name}': DeclarationColumn={symbol.DeclarationColumn} < 0");
        }

        if (symbol.NameDeclarationLine.HasValue)
        {
            if (symbol.NameDeclarationLine.Value < 1)
            {
                errors.Add(
                    $"'{symbol.Name}': NameDeclarationLine={symbol.NameDeclarationLine} < 1");
            }
            if (symbol.NameDeclarationLine.Value > lineCount)
            {
                errors.Add(
                    $"'{symbol.Name}': NameDeclarationLine={symbol.NameDeclarationLine} > lineCount={lineCount}");
            }
        }

        if (symbol.NameDeclarationColumn.HasValue && symbol.NameDeclarationColumn.Value < 0)
        {
            errors.Add(
                $"'{symbol.Name}': NameDeclarationColumn={symbol.NameDeclarationColumn} < 0");
        }
    }

    private static void AssertNameAfterDeclaration(Symbol symbol, List<string> errors)
    {
        if (!symbol.DeclarationLine.HasValue || !symbol.NameDeclarationLine.HasValue)
            return;

        if (symbol.NameDeclarationLine.Value < symbol.DeclarationLine.Value)
        {
            errors.Add(
                $"'{symbol.Name}': NameDeclarationLine={symbol.NameDeclarationLine} " +
                $"< DeclarationLine={symbol.DeclarationLine}");
        }
        else if (symbol.NameDeclarationLine.Value == symbol.DeclarationLine.Value &&
                 symbol.DeclarationColumn.HasValue && symbol.NameDeclarationColumn.HasValue &&
                 symbol.NameDeclarationColumn.Value < symbol.DeclarationColumn.Value)
        {
            errors.Add(
                $"'{symbol.Name}': NameDeclarationColumn={symbol.NameDeclarationColumn} " +
                $"< DeclarationColumn={symbol.DeclarationColumn} on same line");
        }
    }

    private static int LineColumnToOffset(string source, int line, int column)
    {
        var lines = source.Split('\n');
        int offset = 0;
        for (int i = 0; i < line - 1 && i < lines.Length; i++)
            offset += lines[i].Length + 1; // +1 for newline
        return offset + column;
    }
}