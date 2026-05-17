using CsCheck;

namespace Sharpy.Compiler.Tests.Properties.Generators;

internal static class GenLineContinuation
{
    private static readonly string[] BinaryOps = { "+", "-", "*", "/", "==", "!=", "<", ">", "and", "or" };

    private static readonly string[] LeftOperands = { "x", "y", "z", "1", "2", "10", "result", "value" };
    private static readonly string[] RightOperands = { "a", "b", "c", "3", "4", "20", "total", "count" };

    public static Gen<(string WithContinuation, string Equivalent)> ExplicitContinuation { get; } =
        Gen.Select(
            Gen.OneOfConst(LeftOperands),
            Gen.OneOfConst(BinaryOps),
            Gen.OneOfConst(RightOperands),
            (left, op, right) => (
                WithContinuation: $"result = {left} {op}\\\n    {right}\n",
                Equivalent: $"result = {left} {op} {right}\n"));

    public static Gen<(string WithContinuation, string Equivalent)> ImplicitContinuationParens { get; } =
        Gen.Select(
            Gen.OneOfConst(LeftOperands),
            Gen.OneOfConst(BinaryOps),
            Gen.OneOfConst(RightOperands),
            (left, op, right) => (
                WithContinuation: $"result = ({left} {op}\n    {right})\n",
                Equivalent: $"result = ({left} {op} {right})\n"));

    public static Gen<(string WithContinuation, string Equivalent)> ImplicitContinuationBrackets { get; } =
        Gen.Select(
            Gen.OneOfConst(LeftOperands),
            Gen.OneOfConst(RightOperands),
            Gen.OneOfConst(RightOperands),
            (a, b, c) => (
                WithContinuation: $"items = [{a},\n    {b},\n    {c}]\n",
                Equivalent: $"items = [{a}, {b}, {c}]\n"));

    private static readonly string[] DictValues = { "1", "2", "3", "4", "10", "20", "42", "100" };

    public static Gen<(string WithContinuation, string Equivalent)> ImplicitContinuationBraces { get; } =
        Gen.Select(
            Gen.OneOfConst(LeftOperands),
            Gen.OneOfConst(DictValues),
            (key, val) => (
                WithContinuation: $"d = {{\"{key}\":\n    {val}}}\n",
                Equivalent: $"d = {{\"{key}\": {val}}}\n"));

    private static readonly string[] TrailingWhitespaceSources =
    {
        "result = x +\\ \n    a\n",
        "result = y -\\  \n    b\n",
        "result = z *\\\t\n    c\n",
        "result = 1 +\\ \t\n    3\n",
        "result = value ==\\ \n    total\n",
        "result = x and\\ \n    y\n",
        "result = 2 /\\  \n    4\n",
        "result = result !=\\\t\n    count\n"
    };

    public static Gen<string> BackslashWithTrailingWhitespace { get; } =
        Gen.OneOfConst(TrailingWhitespaceSources);

    public static Gen<(string WithContinuation, string Equivalent)> MixedContinuation { get; } =
        Gen.Select(
            Gen.OneOfConst(LeftOperands),
            Gen.OneOfConst(BinaryOps),
            Gen.OneOfConst(RightOperands),
            Gen.OneOfConst(BinaryOps),
            Gen.OneOfConst(RightOperands),
            (a, op1, b, op2, c) => (
                WithContinuation: $"result = ({a} {op1}\\\n    {b} {op2}\n    {c})\n",
                Equivalent: $"result = ({a} {op1} {b} {op2} {c})\n"));
}
