using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The OPERATOR axis of the CLR call-checking parity sweep (batch instrument for the 2026-08-13
/// remediation round; #1395, #1501). The instance/static axes are #1451's (Batch C, plan-8e962a);
/// when they land their rows into this file the two merge additively rather than forking harnesses.
///
/// <para>
/// <b>Contract.</b> An operator dunder is resolved by one of two paths depending only on how many
/// candidates share the dunder: a LONE candidate is type-checked directly against its operand (the
/// "check" path), while TWO OR MORE route through the specificity-based betterness core (the
/// "resolve" path). Those paths must not disagree: the overload the resolve path selects from a set
/// must, ON ITS OWN, still accept the same operand; and an operand the resolve path refuses must be
/// refused by every candidate individually. A type whose operator gains an unrelated overload must
/// not thereby change which operand its existing overload accepts (#1395).
/// </para>
///
/// <para>
/// Ratchet discipline: the sweep starts green with no allowlist. Any future divergence fails loudly;
/// a divergence is never allowlisted without an issue reference.
/// </para>
/// </summary>
public class ClrCallCheckingParityConformanceTests
{
    private static TypeChecker NewChecker()
    {
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);
        return new TypeChecker(symbolTable, semanticInfo, typeResolver);
    }

    private static FunctionSymbol Dunder(string name, params (string Name, SemanticType Type)[] parameters) =>
        new()
        {
            Name = name,
            Kind = SymbolKind.Function,
            ReturnType = SemanticType.Int,
            Parameters = parameters
                .Select(p => new ParameterSymbol { Name = p.Name, Type = p.Type })
                .ToList()
        };

    // A CLR-shaped operator (receiver reflected as `left`, never `self`) and a Sharpy-shaped one
    // (leading `self`), so the offset axis is exercised on both spellings.
    private static FunctionSymbol ClrOp(string name, SemanticType operand) =>
        Dunder(name, ("left", SemanticType.Int), ("right", operand));

    private static FunctionSymbol SharpyOp(string name, SemanticType operand) =>
        Dunder(name, (PythonNames.Self, SemanticType.Int), ("other", operand));

    public sealed record Row(
        string Name,
        IReadOnlyList<FunctionSymbol> Candidates,
        SemanticType Operand,
        bool ExpectResolved);

    public static IEnumerable<object[]> OperatorRows()
    {
        var rows = new List<Row>();

        // Single candidate, CHECKED — the operand matches the sole overload.
        rows.Add(new("clr-single-checked", new[] { ClrOp("__or__", SemanticType.Str) }, SemanticType.Str, true));
        rows.Add(new("sharpy-single-checked", new[] { SharpyOp("__or__", SemanticType.Str) }, SemanticType.Str, true));

        // Single candidate, UNCHECKED — the operand does not match, so the lone candidate is refused
        // (the check path is not a rubber stamp: a single overload still type-checks its operand,
        // #1311).
        rows.Add(new("clr-single-unchecked", new[] { ClrOp("__or__", SemanticType.Str) }, SemanticType.Float, false));

        // Multiple candidates, CHECKED — one of the two accepts the operand.
        rows.Add(new("clr-multi-checked",
            new[] { ClrOp("__or__", SemanticType.Int), ClrOp("__or__", SemanticType.Str) }, SemanticType.Str, true));
        rows.Add(new("sharpy-multi-checked",
            new[] { SharpyOp("__or__", SemanticType.Int), SharpyOp("__or__", SemanticType.Str) }, SemanticType.Str, true));

        // Multiple candidates, betterness TIEBREAK — both accept int, the int overload is strictly
        // more specific than the float one (int→float widening) and wins. This reaches the second
        // offset copy (IsMoreSpecificOverload), where a name-based skip would mis-offset (#1395).
        rows.Add(new("clr-multi-tiebreak",
            new[] { ClrOp("__or__", SemanticType.Float), ClrOp("__or__", SemanticType.Int) }, SemanticType.Int, true));

        // Multiple candidates, UNCHECKED — neither accepts the operand.
        rows.Add(new("clr-multi-unchecked",
            new[] { ClrOp("__or__", SemanticType.Int), ClrOp("__or__", SemanticType.Str) }, SemanticType.Float, false));

        foreach (var row in rows)
            yield return new object[] { row };
    }

    [Theory]
    [MemberData(nameof(OperatorRows))]
    public void CheckAndResolvePaths_SelectConsistently(Row row)
    {
        var checker = NewChecker();

        var resolved = checker.ResolveDunderOverload(row.Candidates, row.Operand);

        if (row.ExpectResolved)
        {
            resolved.Should().NotBeNull($"row '{row.Name}' should resolve an overload for the operand");

            // The overload the resolve path chose must, ON ITS OWN, still accept the same operand —
            // the check path (a lone candidate) agrees with the resolve path (many).
            var alone = checker.ResolveDunderOverload(new[] { resolved! }, row.Operand);
            alone.Should().BeSameAs(resolved,
                $"row '{row.Name}': the selected overload must accept the operand on its own — the "
                + "check and resolve paths must not disagree (#1395)");
        }
        else
        {
            resolved.Should().BeNull($"row '{row.Name}' should refuse the operand");

            // A refused operand must be refused by every candidate individually — the resolve path's
            // refusal is not an artifact of having several candidates.
            foreach (var candidate in row.Candidates)
            {
                checker.ResolveDunderOverload(new[] { candidate }, row.Operand)
                    .Should().BeNull(
                        $"row '{row.Name}': no single candidate accepts the operand either");
            }
        }
    }
}
