using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators;

internal static class GenLiterals
{
    public static Gen<IntegerLiteral> Integer { get; } =
        Gen.Long[0, 10000].Select(v =>
            new IntegerLiteral { Value = v.ToString() });

    public static Gen<IntegerLiteral> IntegerWithSuffix { get; } =
        Gen.Select(Gen.Long[0, 10000], Gen.OneOfConst("", "L", "u"),
            (v, s) => new IntegerLiteral
            {
                Value = v.ToString(),
                Suffix = s == "" ? null : s
            });

    public static Gen<FloatLiteral> Float { get; } =
        Gen.Double[0.0, 1000.0]
            .Where(d => !double.IsNaN(d) && !double.IsInfinity(d))
            .Select(v =>
            {
                var s = v.ToString("G");
                if (!s.Contains('.', StringComparison.Ordinal) && !s.Contains('E', StringComparison.Ordinal))
                    s += ".0";
                return new FloatLiteral { Value = s };
            });

    public static Gen<StringLiteral> String { get; } =
        Gen.String.Where(s =>
                !s.Contains('\r', StringComparison.Ordinal) &&
                !s.Contains('\n', StringComparison.Ordinal) &&
                !s.Contains('\0', StringComparison.Ordinal))
            .Select(s => new StringLiteral { Value = s });

    public static Gen<StringLiteral> SimpleString { get; } =
        Gen.OneOfConst("hello", "world", "test", "foo", "bar", "", "abc 123", "x")
            .Select(s => new StringLiteral { Value = s });

    public static Gen<BooleanLiteral> Boolean { get; } =
        Gen.Bool.Select(b => new BooleanLiteral { Value = b });

    public static Gen<NoneLiteral> None { get; } =
        Gen.Const(new NoneLiteral());

    public static Gen<EllipsisLiteral> Ellipsis { get; } =
        Gen.Const(new EllipsisLiteral());

    public static Gen<Expression> AnyLiteral { get; } =
        Gen.OneOf<Expression>(
            Integer.Select(x => (Expression)x),
            Float.Select(x => (Expression)x),
            SimpleString.Select(x => (Expression)x),
            Boolean.Select(x => (Expression)x),
            None.Select(x => (Expression)x));

    public static Gen<ListLiteral> List(Gen<Expression> elemGen, int maxLen) =>
        elemGen.Array[0, maxLen].Select(elems =>
            new ListLiteral { Elements = elems.ToImmutableArray() });

    public static Gen<SetLiteral> Set(Gen<Expression> elemGen, int maxLen) =>
        elemGen.Array[1, Math.Max(1, maxLen)].Select(elems =>
            new SetLiteral { Elements = elems.ToImmutableArray() });

    public static Gen<DictLiteral> Dict(Gen<Expression> keyGen, Gen<Expression> valGen, int maxLen) =>
        Gen.Select(keyGen.Array[0, maxLen], valGen.Array[0, maxLen], (keys, vals) =>
        {
            var len = Math.Min(keys.Length, vals.Length);
            var entries = new DictEntry[len];
            for (int i = 0; i < len; i++)
                entries[i] = new DictEntry { Key = keys[i], Value = vals[i] };
            return new DictLiteral { Entries = entries.ToImmutableArray() };
        });

    public static Gen<TupleLiteral> Tuple(Gen<Expression> elemGen, int maxLen) =>
        elemGen.Array[1, Math.Max(1, maxLen)].Select(elems =>
            new TupleLiteral
            {
                Elements = elems.ToImmutableArray(),
                ElementNames = ImmutableArray<string?>.Empty
            });
}
