using CsCheck;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Algebraic;

/// <summary>
/// Algebraic properties of multi-iterable <c>map()</c> (#990) and <c>zip()</c> /
/// <c>zip(strict=True)</c> (#988), checked against Python semantics: both stop at the
/// shortest input, <c>map</c> applies the function element-wise, and <c>strict=True</c>
/// raises exactly when the input lengths differ.
/// </summary>
/// <remarks>
/// Mappers are named functions rather than lambdas: an unannotated
/// <c>list(map(lambda ..., a, b))</c> over two or more iterables currently fails the
/// lambda's return-type inference (#1009; the named-function path was fixed by #999).
/// Named functions exercise the same builtin behaviour on the working path.
/// </remarks>
[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class MapZipPropertyTests : AlgebraicTestBase
{
    public MapZipPropertyTests(ITestOutputHelper output) : base(output) { }

    private static Gen<int[]> IntList => Gen.Int[-9, 9].Array[0, 5];

    private static string Lit(int[] xs) => "[" + string.Join(", ", xs) + "]";

    private const string AddDef =
        "def add(x: int, y: int) -> int:\n    return x + y\n\n";

    [Fact]
    public void MultiIterableMap_LengthIsShortestInput()
    {
        Gen.Select(IntList, IntList).Sample((a, b) =>
        {
            var src = AddDef +
                "def main() -> None:\n" +
                $"    a: list[int] = {Lit(a)}\n" +
                $"    b: list[int] = {Lit(b)}\n" +
                "    print(len(list(map(add, a, b))))";
            var r = RunAndCapture(src);
            var expected = Math.Min(a.Length, b.Length).ToString();
            if (r != null && r != expected)
                throw new Exception(
                    $"len(map(add, {Lit(a)}, {Lit(b)})) = {r}, expected {expected}");
        }, iter: 15);
    }

    [Fact]
    public void MultiIterableMap_MapsElementwise()
    {
        Gen.Select(IntList, IntList).Sample((a, b) =>
        {
            var src = AddDef +
                "def main() -> None:\n" +
                $"    a: list[int] = {Lit(a)}\n" +
                $"    b: list[int] = {Lit(b)}\n" +
                "    print(list(map(add, a, b)))";
            var r = RunAndCapture(src);
            int n = Math.Min(a.Length, b.Length);
            var expected = "[" + string.Join(", ",
                Enumerable.Range(0, n).Select(i => a[i] + b[i])) + "]";
            if (r != null && r != expected)
                throw new Exception(
                    $"map(add, {Lit(a)}, {Lit(b)}) = {r}, expected {expected}");
        }, iter: 15);
    }

    [Fact]
    public void Zip_LengthIsShortestInput()
    {
        Gen.Select(IntList, IntList).Sample((a, b) =>
        {
            var src =
                "def main() -> None:\n" +
                $"    a: list[int] = {Lit(a)}\n" +
                $"    b: list[int] = {Lit(b)}\n" +
                "    print(len(list(zip(a, b))))";
            var r = RunAndCapture(src);
            var expected = Math.Min(a.Length, b.Length).ToString();
            if (r != null && r != expected)
                throw new Exception(
                    $"len(zip({Lit(a)}, {Lit(b)})) = {r}, expected {expected}");
        }, iter: 15);
    }

    [Fact]
    public void ZipStrict_RaisesIffLengthsDiffer()
    {
        Gen.Select(IntList, IntList).Sample((a, b) =>
        {
            var src =
                "def main() -> None:\n" +
                $"    a: list[int] = {Lit(a)}\n" +
                $"    b: list[int] = {Lit(b)}\n" +
                "    try:\n" +
                "        z = list(zip(a, b, strict=True))\n" +
                "        print(\"ok \" + str(len(z)))\n" +
                "    except Exception:\n" +
                "        print(\"error\")";
            var r = RunAndCapture(src);
            var expected = a.Length == b.Length ? $"ok {a.Length}" : "error";
            if (r != null && r != expected)
                throw new Exception(
                    $"zip(strict=True) over ({Lit(a)}, {Lit(b)}) = {r}, expected {expected}");
        }, iter: 15);
    }
}
