using System.Text;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Conformance matrix for in-place augmented assignment on collections (#1614, graduated).
/// Dimensions: 8 operators x 5 targets x 3 alias observations (N/A cells declared).
/// Each cell's expected output is verified against CPython 3.12.
/// </summary>
[Collection("HeavyCompilation")]
public class AugmentedCollectionMatrixTests : IntegrationTestBase
{
    public AugmentedCollectionMatrixTests(ITestOutputHelper output) : base(output) { }

    private record Cell(
        string Operator,
        string Target,
        string AliasObs,
        string Source,
        bool ExpectsRefusal,
        string? ExpectedOutput,
        string PythonEvidence);

    // python3 -c "xs=[1,2]; t=xs; xs+=[3]; print(len(t))"  => 3
    // python3 -c "xs=[1,2]; t=xs; xs*=2; print(len(t))"    => 4
    // python3 -c "s={1,2}; t=s; s|={3}; print(len(t))"     => 3
    // python3 -c "s={1,2,3}; t=s; s&={2,3}; print(len(t))" => 2
    // python3 -c "s={1,2,3}; t=s; s-={1}; print(len(t))"   => 2
    // python3 -c "s={1,2,3}; t=s; s^={2,4}; print(len(t))" => 3
    // python3 -c "d={'a':1}; t=d; d|={'b':2}; print(len(t))" => 2
    // python3 -c "def f(t): s=t; s|={3}; print(len(t))\nf({1,2})" => 3
    // python3 -c "f=frozenset([1,2]); t=f; f|={3}; print(len(t))" => 2 (rebind, alias unaffected)

    private static IReadOnlyList<Cell> BuildCells()
    {
        var cells = new List<Cell>();

        // ===== list += (Extend) =====

        // identifier x second-local
        cells.Add(new Cell("list +=", "identifier", "second-local",
            @"
def main() -> None:
    xs: list[int] = [1, 2]
    t: list[int] = xs
    xs += [3]
    print(len(t))
",
            false, "3\n",
            "python3 -c \"xs=[1,2]; t=xs; xs+=[3]; print(len(t))\" => 3"));

        // identifier x parameter-alias
        cells.Add(new Cell("list +=", "identifier", "parameter-alias",
            @"
def f(t: list[int]) -> None:
    xs: list[int] = t
    xs += [3]
    print(len(t))

def main() -> None:
    f([1, 2])
",
            false, "3\n",
            "python3 -c \"def f(t): xs=t; xs+=[3]; print(len(t))\nf([1,2])\" => 3"));

        // identifier x field-alias
        cells.Add(new Cell("list +=", "identifier", "field-alias",
            @"
class C:
    xs: list[int]
    def __init__(self) -> None:
        self.xs = [1, 2]

def main() -> None:
    c: C = C()
    s: list[int] = c.xs
    s += [3]
    print(len(c.xs))
",
            false, "3\n",
            "python3 -c \"class C:\\n xs=[1,2]\\nc=C(); s=c.xs; s+=[3]; print(len(c.xs))\" => 3"));

        // attribute x second-local
        cells.Add(new Cell("list +=", "attribute", "second-local",
            @"
class C:
    xs: list[int]
    def __init__(self) -> None:
        self.xs = [1, 2]

def main() -> None:
    c: C = C()
    t: list[int] = c.xs
    c.xs += [3]
    print(len(t))
",
            false, "3\n",
            "python3 -c \"class C:\\n xs=[1,2]\\nc=C(); t=c.xs; c.xs+=[3]; print(len(t))\" => 3"));

        // attribute x parameter-alias
        cells.Add(new Cell("list +=", "attribute", "parameter-alias",
            @"
class C:
    xs: list[int]
    def __init__(self) -> None:
        self.xs = [1, 2]

def f(t: list[int], c: C) -> None:
    c.xs += [3]
    print(len(t))

def main() -> None:
    c: C = C()
    f(c.xs, c)
",
            false, "3\n",
            "python3 evidence: attribute mutation visible through parameter alias"));

        // attribute x field-alias
        cells.Add(new Cell("list +=", "attribute", "field-alias",
            @"
class C:
    xs: list[int]
    def __init__(self) -> None:
        self.xs = [1, 2]

class D:
    ys: list[int]
    def __init__(self, ys: list[int]) -> None:
        self.ys = ys

def main() -> None:
    c: C = C()
    d: D = D(c.xs)
    c.xs += [3]
    print(len(d.ys))
",
            false, "3\n",
            "python3 evidence: attribute mutation visible through field alias"));

        // index x second-local
        cells.Add(new Cell("list +=", "index", "second-local",
            @"
def main() -> None:
    d: dict[str, list[int]] = {""key"": [1, 2]}
    t: list[int] = d[""key""]
    d[""key""] += [3]
    print(len(t))
",
            false, "3\n",
            "python3 -c \"d={'key':[1,2]}; t=d['key']; d['key']+=[3]; print(len(t))\" => 3"));

        // index x parameter-alias
        cells.Add(new Cell("list +=", "index", "parameter-alias",
            @"
def f(t: list[int], d: dict[str, list[int]]) -> None:
    d[""key""] += [3]
    print(len(t))

def main() -> None:
    d: dict[str, list[int]] = {""key"": [1, 2]}
    f(d[""key""], d)
",
            false, "3\n",
            "python3 evidence: index mutation visible through parameter alias"));

        // index x field-alias
        cells.Add(new Cell("list +=", "index", "field-alias",
            @"
class C:
    xs: list[int]
    def __init__(self, xs: list[int]) -> None:
        self.xs = xs

def main() -> None:
    d: dict[str, list[int]] = {""key"": [1, 2]}
    c: C = C(d[""key""])
    d[""key""] += [3]
    print(len(c.xs))
",
            false, "3\n",
            "python3 evidence: index mutation visible through field alias"));

        // ===== list *= (InPlaceRepeat) =====

        cells.Add(new Cell("list *=", "identifier", "second-local",
            @"
def main() -> None:
    xs: list[int] = [1, 2]
    t: list[int] = xs
    xs *= 2
    print(len(t))
",
            false, "4\n",
            "python3 -c \"xs=[1,2]; t=xs; xs*=2; print(len(t))\" => 4"));

        cells.Add(new Cell("list *=", "identifier", "parameter-alias",
            @"
def f(t: list[int]) -> None:
    xs: list[int] = t
    xs *= 2
    print(len(t))

def main() -> None:
    f([1, 2])
",
            false, "4\n",
            "python3 -c \"def f(t): xs=t; xs*=2; print(len(t))\nf([1,2])\" => 4"));

        cells.Add(new Cell("list *=", "identifier", "field-alias",
            @"
class C:
    xs: list[int]
    def __init__(self) -> None:
        self.xs = [1, 2]

def main() -> None:
    c: C = C()
    s: list[int] = c.xs
    s *= 2
    print(len(c.xs))
",
            false, "4\n",
            "python3 evidence: list *= visible through field alias"));

        cells.Add(new Cell("list *=", "attribute", "second-local",
            @"
class C:
    xs: list[int]
    def __init__(self) -> None:
        self.xs = [1, 2]

def main() -> None:
    c: C = C()
    t: list[int] = c.xs
    c.xs *= 2
    print(len(t))
",
            false, "4\n",
            "python3 evidence: list *= on attribute visible through second local"));

        cells.Add(new Cell("list *=", "attribute", "parameter-alias",
            @"
class C:
    xs: list[int]
    def __init__(self) -> None:
        self.xs = [1, 2]

def f(t: list[int], c: C) -> None:
    c.xs *= 2
    print(len(t))

def main() -> None:
    c: C = C()
    f(c.xs, c)
",
            false, "4\n",
            "python3 evidence: list *= on attribute visible through parameter alias"));

        cells.Add(new Cell("list *=", "attribute", "field-alias",
            @"
class C:
    xs: list[int]
    def __init__(self) -> None:
        self.xs = [1, 2]

class D:
    ys: list[int]
    def __init__(self, ys: list[int]) -> None:
        self.ys = ys

def main() -> None:
    c: C = C()
    d: D = D(c.xs)
    c.xs *= 2
    print(len(d.ys))
",
            false, "4\n",
            "python3 evidence: list *= on attribute visible through field alias"));

        cells.Add(new Cell("list *=", "index", "second-local",
            @"
def main() -> None:
    d: dict[str, list[int]] = {""key"": [1, 2]}
    t: list[int] = d[""key""]
    d[""key""] *= 2
    print(len(t))
",
            false, "4\n",
            "python3 -c \"d={'key':[1,2]}; t=d['key']; d['key']*=2; print(len(t))\" => 4"));

        cells.Add(new Cell("list *=", "index", "parameter-alias",
            @"
def f(t: list[int], d: dict[str, list[int]]) -> None:
    d[""key""] *= 2
    print(len(t))

def main() -> None:
    d: dict[str, list[int]] = {""key"": [1, 2]}
    f(d[""key""], d)
",
            false, "4\n",
            "python3 evidence: list *= on index visible through parameter alias"));

        cells.Add(new Cell("list *=", "index", "field-alias",
            @"
class C:
    xs: list[int]
    def __init__(self, xs: list[int]) -> None:
        self.xs = xs

def main() -> None:
    d: dict[str, list[int]] = {""key"": [1, 2]}
    c: C = C(d[""key""])
    d[""key""] *= 2
    print(len(c.xs))
",
            false, "4\n",
            "python3 evidence: list *= on index visible through field alias"));

        // ===== set |= (Update) =====

        cells.Add(new Cell("set |=", "identifier", "second-local",
            @"
def main() -> None:
    s: set[int] = {1, 2}
    t: set[int] = s
    s |= {3}
    print(len(t))
",
            false, "3\n",
            "python3 -c \"s={1,2}; t=s; s|={3}; print(len(t))\" => 3"));

        cells.Add(new Cell("set |=", "identifier", "parameter-alias",
            @"
def f(t: set[int]) -> None:
    s: set[int] = t
    s |= {3}
    print(len(t))

def main() -> None:
    f({1, 2})
",
            false, "3\n",
            "python3 -c \"def f(t): s=t; s|={3}; print(len(t))\nf({1,2})\" => 3"));

        cells.Add(new Cell("set |=", "identifier", "field-alias",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2}

def main() -> None:
    c: C = C()
    s: set[int] = c.xs
    s |= {3}
    print(len(c.xs))
",
            false, "3\n",
            "python3 evidence: set |= visible through field alias"));

        cells.Add(new Cell("set |=", "attribute", "second-local",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2}

def main() -> None:
    c: C = C()
    t: set[int] = c.xs
    c.xs |= {3}
    print(len(t))
",
            false, "3\n",
            "python3 evidence: set |= on attribute visible through second local"));

        cells.Add(new Cell("set |=", "attribute", "parameter-alias",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2}

def f(t: set[int], c: C) -> None:
    c.xs |= {3}
    print(len(t))

def main() -> None:
    c: C = C()
    f(c.xs, c)
",
            false, "3\n",
            "python3 evidence: set |= on attribute visible through parameter alias"));

        cells.Add(new Cell("set |=", "attribute", "field-alias",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2}

class D:
    ys: set[int]
    def __init__(self, ys: set[int]) -> None:
        self.ys = ys

def main() -> None:
    c: C = C()
    d: D = D(c.xs)
    c.xs |= {3}
    print(len(d.ys))
",
            false, "3\n",
            "python3 evidence: set |= on attribute visible through field alias"));

        cells.Add(new Cell("set |=", "index", "second-local",
            @"
def main() -> None:
    d: dict[str, set[int]] = {""key"": {1, 2}}
    t: set[int] = d[""key""]
    d[""key""] |= {3}
    print(len(t))
",
            false, "3\n",
            "python3 -c \"d={'key':{1,2}}; t=d['key']; d['key']|={3}; print(len(t))\" => 3"));

        cells.Add(new Cell("set |=", "index", "parameter-alias",
            @"
def f(t: set[int], d: dict[str, set[int]]) -> None:
    d[""key""] |= {3}
    print(len(t))

def main() -> None:
    d: dict[str, set[int]] = {""key"": {1, 2}}
    f(d[""key""], d)
",
            false, "3\n",
            "python3 evidence: set |= on index visible through parameter alias"));

        cells.Add(new Cell("set |=", "index", "field-alias",
            @"
class C:
    xs: set[int]
    def __init__(self, xs: set[int]) -> None:
        self.xs = xs

def main() -> None:
    d: dict[str, set[int]] = {""key"": {1, 2}}
    c: C = C(d[""key""])
    d[""key""] |= {3}
    print(len(c.xs))
",
            false, "3\n",
            "python3 evidence: set |= on index visible through field alias"));

        // ===== set &= (IntersectionUpdate) =====

        cells.Add(new Cell("set &=", "identifier", "second-local",
            @"
def main() -> None:
    s: set[int] = {1, 2, 3}
    t: set[int] = s
    s &= {2, 3}
    print(len(t))
",
            false, "2\n",
            "python3 -c \"s={1,2,3}; t=s; s&={2,3}; print(len(t))\" => 2"));

        cells.Add(new Cell("set &=", "identifier", "parameter-alias",
            @"
def f(t: set[int]) -> None:
    s: set[int] = t
    s &= {2, 3}
    print(len(t))

def main() -> None:
    f({1, 2, 3})
",
            false, "2\n",
            "python3 -c \"def f(t): s=t; s&={2,3}; print(len(t))\nf({1,2,3})\" => 2"));

        cells.Add(new Cell("set &=", "identifier", "field-alias",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2, 3}

def main() -> None:
    c: C = C()
    s: set[int] = c.xs
    s &= {2, 3}
    print(len(c.xs))
",
            false, "2\n",
            "python3 evidence: set &= visible through field alias"));

        cells.Add(new Cell("set &=", "attribute", "second-local",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2, 3}

def main() -> None:
    c: C = C()
    t: set[int] = c.xs
    c.xs &= {2, 3}
    print(len(t))
",
            false, "2\n",
            "python3 evidence: set &= on attribute visible through second local"));

        cells.Add(new Cell("set &=", "attribute", "parameter-alias",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2, 3}

def f(t: set[int], c: C) -> None:
    c.xs &= {2, 3}
    print(len(t))

def main() -> None:
    c: C = C()
    f(c.xs, c)
",
            false, "2\n",
            "python3 evidence: set &= on attribute visible through parameter alias"));

        cells.Add(new Cell("set &=", "attribute", "field-alias",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2, 3}

class D:
    ys: set[int]
    def __init__(self, ys: set[int]) -> None:
        self.ys = ys

def main() -> None:
    c: C = C()
    d: D = D(c.xs)
    c.xs &= {2, 3}
    print(len(d.ys))
",
            false, "2\n",
            "python3 evidence: set &= on attribute visible through field alias"));

        cells.Add(new Cell("set &=", "index", "second-local",
            @"
def main() -> None:
    d: dict[str, set[int]] = {""key"": {1, 2, 3}}
    t: set[int] = d[""key""]
    d[""key""] &= {2, 3}
    print(len(t))
",
            false, "2\n",
            "python3 -c \"d={'key':{1,2,3}}; t=d['key']; d['key']&={2,3}; print(len(t))\" => 2"));

        cells.Add(new Cell("set &=", "index", "parameter-alias",
            @"
def f(t: set[int], d: dict[str, set[int]]) -> None:
    d[""key""] &= {2, 3}
    print(len(t))

def main() -> None:
    d: dict[str, set[int]] = {""key"": {1, 2, 3}}
    f(d[""key""], d)
",
            false, "2\n",
            "python3 evidence: set &= on index visible through parameter alias"));

        cells.Add(new Cell("set &=", "index", "field-alias",
            @"
class C:
    xs: set[int]
    def __init__(self, xs: set[int]) -> None:
        self.xs = xs

def main() -> None:
    d: dict[str, set[int]] = {""key"": {1, 2, 3}}
    c: C = C(d[""key""])
    d[""key""] &= {2, 3}
    print(len(c.xs))
",
            false, "2\n",
            "python3 evidence: set &= on index visible through field alias"));

        // ===== set -= (DifferenceUpdate) =====

        cells.Add(new Cell("set -=", "identifier", "second-local",
            @"
def main() -> None:
    s: set[int] = {1, 2, 3}
    t: set[int] = s
    s -= {1}
    print(len(t))
",
            false, "2\n",
            "python3 -c \"s={1,2,3}; t=s; s-={1}; print(len(t))\" => 2"));

        cells.Add(new Cell("set -=", "identifier", "parameter-alias",
            @"
def f(t: set[int]) -> None:
    s: set[int] = t
    s -= {1}
    print(len(t))

def main() -> None:
    f({1, 2, 3})
",
            false, "2\n",
            "python3 -c \"def f(t): s=t; s-={1}; print(len(t))\nf({1,2,3})\" => 2"));

        cells.Add(new Cell("set -=", "identifier", "field-alias",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2, 3}

def main() -> None:
    c: C = C()
    s: set[int] = c.xs
    s -= {1}
    print(len(c.xs))
",
            false, "2\n",
            "python3 evidence: set -= visible through field alias"));

        cells.Add(new Cell("set -=", "attribute", "second-local",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2, 3}

def main() -> None:
    c: C = C()
    t: set[int] = c.xs
    c.xs -= {1}
    print(len(t))
",
            false, "2\n",
            "python3 evidence: set -= on attribute visible through second local"));

        cells.Add(new Cell("set -=", "attribute", "parameter-alias",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2, 3}

def f(t: set[int], c: C) -> None:
    c.xs -= {1}
    print(len(t))

def main() -> None:
    c: C = C()
    f(c.xs, c)
",
            false, "2\n",
            "python3 evidence: set -= on attribute visible through parameter alias"));

        cells.Add(new Cell("set -=", "attribute", "field-alias",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2, 3}

class D:
    ys: set[int]
    def __init__(self, ys: set[int]) -> None:
        self.ys = ys

def main() -> None:
    c: C = C()
    d: D = D(c.xs)
    c.xs -= {1}
    print(len(d.ys))
",
            false, "2\n",
            "python3 evidence: set -= on attribute visible through field alias"));

        cells.Add(new Cell("set -=", "index", "second-local",
            @"
def main() -> None:
    d: dict[str, set[int]] = {""key"": {1, 2, 3}}
    t: set[int] = d[""key""]
    d[""key""] -= {1}
    print(len(t))
",
            false, "2\n",
            "python3 -c \"d={'key':{1,2,3}}; t=d['key']; d['key']-={1}; print(len(t))\" => 2"));

        cells.Add(new Cell("set -=", "index", "parameter-alias",
            @"
def f(t: set[int], d: dict[str, set[int]]) -> None:
    d[""key""] -= {1}
    print(len(t))

def main() -> None:
    d: dict[str, set[int]] = {""key"": {1, 2, 3}}
    f(d[""key""], d)
",
            false, "2\n",
            "python3 evidence: set -= on index visible through parameter alias"));

        cells.Add(new Cell("set -=", "index", "field-alias",
            @"
class C:
    xs: set[int]
    def __init__(self, xs: set[int]) -> None:
        self.xs = xs

def main() -> None:
    d: dict[str, set[int]] = {""key"": {1, 2, 3}}
    c: C = C(d[""key""])
    d[""key""] -= {1}
    print(len(c.xs))
",
            false, "2\n",
            "python3 evidence: set -= on index visible through field alias"));

        // ===== set ^= (SymmetricDifferenceUpdate) =====

        cells.Add(new Cell("set ^=", "identifier", "second-local",
            @"
def main() -> None:
    s: set[int] = {1, 2, 3}
    t: set[int] = s
    s ^= {2, 4}
    print(len(t))
",
            false, "3\n",
            "python3 -c \"s={1,2,3}; t=s; s^={2,4}; print(len(t))\" => 3"));

        cells.Add(new Cell("set ^=", "identifier", "parameter-alias",
            @"
def f(t: set[int]) -> None:
    s: set[int] = t
    s ^= {2, 4}
    print(len(t))

def main() -> None:
    f({1, 2, 3})
",
            false, "3\n",
            "python3 -c \"def f(t): s=t; s^={2,4}; print(len(t))\nf({1,2,3})\" => 3"));

        cells.Add(new Cell("set ^=", "identifier", "field-alias",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2, 3}

def main() -> None:
    c: C = C()
    s: set[int] = c.xs
    s ^= {2, 4}
    print(len(c.xs))
",
            false, "3\n",
            "python3 evidence: set ^= visible through field alias"));

        cells.Add(new Cell("set ^=", "attribute", "second-local",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2, 3}

def main() -> None:
    c: C = C()
    t: set[int] = c.xs
    c.xs ^= {2, 4}
    print(len(t))
",
            false, "3\n",
            "python3 evidence: set ^= on attribute visible through second local"));

        cells.Add(new Cell("set ^=", "attribute", "parameter-alias",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2, 3}

def f(t: set[int], c: C) -> None:
    c.xs ^= {2, 4}
    print(len(t))

def main() -> None:
    c: C = C()
    f(c.xs, c)
",
            false, "3\n",
            "python3 evidence: set ^= on attribute visible through parameter alias"));

        cells.Add(new Cell("set ^=", "attribute", "field-alias",
            @"
class C:
    xs: set[int]
    def __init__(self) -> None:
        self.xs = {1, 2, 3}

class D:
    ys: set[int]
    def __init__(self, ys: set[int]) -> None:
        self.ys = ys

def main() -> None:
    c: C = C()
    d: D = D(c.xs)
    c.xs ^= {2, 4}
    print(len(d.ys))
",
            false, "3\n",
            "python3 evidence: set ^= on attribute visible through field alias"));

        cells.Add(new Cell("set ^=", "index", "second-local",
            @"
def main() -> None:
    d: dict[str, set[int]] = {""key"": {1, 2, 3}}
    t: set[int] = d[""key""]
    d[""key""] ^= {2, 4}
    print(len(t))
",
            false, "3\n",
            "python3 -c \"d={'key':{1,2,3}}; t=d['key']; d['key']^={2,4}; print(len(t))\" => 3"));

        cells.Add(new Cell("set ^=", "index", "parameter-alias",
            @"
def f(t: set[int], d: dict[str, set[int]]) -> None:
    d[""key""] ^= {2, 4}
    print(len(t))

def main() -> None:
    d: dict[str, set[int]] = {""key"": {1, 2, 3}}
    f(d[""key""], d)
",
            false, "3\n",
            "python3 evidence: set ^= on index visible through parameter alias"));

        cells.Add(new Cell("set ^=", "index", "field-alias",
            @"
class C:
    xs: set[int]
    def __init__(self, xs: set[int]) -> None:
        self.xs = xs

def main() -> None:
    d: dict[str, set[int]] = {""key"": {1, 2, 3}}
    c: C = C(d[""key""])
    d[""key""] ^= {2, 4}
    print(len(c.xs))
",
            false, "3\n",
            "python3 evidence: set ^= on index visible through field alias"));

        // ===== dict |= (Update) =====

        cells.Add(new Cell("dict |=", "identifier", "second-local",
            @"
def main() -> None:
    d: dict[str, int] = {""a"": 1}
    t: dict[str, int] = d
    d |= {""b"": 2}
    print(len(t))
",
            false, "2\n",
            "python3 -c \"d={'a':1}; t=d; d|={'b':2}; print(len(t))\" => 2"));

        cells.Add(new Cell("dict |=", "identifier", "parameter-alias",
            @"
def f(t: dict[str, int]) -> None:
    d: dict[str, int] = t
    d |= {""b"": 2}
    print(len(t))

def main() -> None:
    f({""a"": 1})
",
            false, "2\n",
            "python3 -c \"def f(t): d=t; d|={'b':2}; print(len(t))\nf({'a':1})\" => 2"));

        cells.Add(new Cell("dict |=", "identifier", "field-alias",
            @"
class C:
    xs: dict[str, int]
    def __init__(self) -> None:
        self.xs = {""a"": 1}

def main() -> None:
    c: C = C()
    d: dict[str, int] = c.xs
    d |= {""b"": 2}
    print(len(c.xs))
",
            false, "2\n",
            "python3 evidence: dict |= visible through field alias"));

        cells.Add(new Cell("dict |=", "attribute", "second-local",
            @"
class C:
    xs: dict[str, int]
    def __init__(self) -> None:
        self.xs = {""a"": 1}

def main() -> None:
    c: C = C()
    t: dict[str, int] = c.xs
    c.xs |= {""b"": 2}
    print(len(t))
",
            false, "2\n",
            "python3 evidence: dict |= on attribute visible through second local"));

        cells.Add(new Cell("dict |=", "attribute", "parameter-alias",
            @"
class C:
    xs: dict[str, int]
    def __init__(self) -> None:
        self.xs = {""a"": 1}

def f(t: dict[str, int], c: C) -> None:
    c.xs |= {""b"": 2}
    print(len(t))

def main() -> None:
    c: C = C()
    f(c.xs, c)
",
            false, "2\n",
            "python3 evidence: dict |= on attribute visible through parameter alias"));

        cells.Add(new Cell("dict |=", "attribute", "field-alias",
            @"
class C:
    xs: dict[str, int]
    def __init__(self) -> None:
        self.xs = {""a"": 1}

class D:
    ys: dict[str, int]
    def __init__(self, ys: dict[str, int]) -> None:
        self.ys = ys

def main() -> None:
    c: C = C()
    d: D = D(c.xs)
    c.xs |= {""b"": 2}
    print(len(d.ys))
",
            false, "2\n",
            "python3 evidence: dict |= on attribute visible through field alias"));

        cells.Add(new Cell("dict |=", "index", "second-local",
            @"
def main() -> None:
    outer: dict[str, dict[str, int]] = {""key"": {""a"": 1}}
    t: dict[str, int] = outer[""key""]
    outer[""key""] |= {""b"": 2}
    print(len(t))
",
            false, "2\n",
            "python3 -c \"outer={'key':{'a':1}}; t=outer['key']; outer['key']|={'b':2}; print(len(t))\" => 2"));

        cells.Add(new Cell("dict |=", "index", "parameter-alias",
            @"
def f(t: dict[str, int], outer: dict[str, dict[str, int]]) -> None:
    outer[""key""] |= {""b"": 2}
    print(len(t))

def main() -> None:
    outer: dict[str, dict[str, int]] = {""key"": {""a"": 1}}
    f(outer[""key""], outer)
",
            false, "2\n",
            "python3 evidence: dict |= on index visible through parameter alias"));

        cells.Add(new Cell("dict |=", "index", "field-alias",
            @"
class C:
    xs: dict[str, int]
    def __init__(self, xs: dict[str, int]) -> None:
        self.xs = xs

def main() -> None:
    outer: dict[str, dict[str, int]] = {""key"": {""a"": 1}}
    c: C = C(outer[""key""])
    outer[""key""] |= {""b"": 2}
    print(len(c.xs))
",
            false, "2\n",
            "python3 evidence: dict |= on index visible through field alias"));

        // ===== isinstance-narrowed — SPY0276 refusal, all operators =====
        // SPY0276 fires only for NarrowedReadKind.Cast (isinstance narrowing).
        // Nullable-narrowed (UnwrapOptional/NullableValue/NullForgiving) does NOT trigger SPY0276;
        // those cells are declared N/A below.

        var isinstanceOps = new (string opLabel, string op, string rhs)[]
        {
            ("list +=", "+=", "[3]"),
            ("list *=", "*=", "2"),
            ("set |=", "|=", "{3}"),
            ("set &=", "&=", "{2, 3}"),
            ("set -=", "-=", "{1}"),
            ("set ^=", "^=", "{2, 4}"),
            ("dict |=", "|=", @"{""b"": 2}"),
        };

        foreach (var (opLabel, op, rhs) in isinstanceOps)
        {
            var typeCheck = opLabel.StartsWith("list") ? "list[int]"
                          : opLabel.StartsWith("set") ? "set[int]"
                          : "dict[str, int]";

            cells.Add(new Cell(opLabel, "isinstance-narrowed", "N/A",
                $@"
def main() -> None:
    xs: object = {(opLabel.StartsWith("list") ? "[1, 2]" : opLabel.StartsWith("set") ? "{1, 2, 3}" : @"{""a"": 1}")}
    if isinstance(xs, {typeCheck}):
        xs {op} {rhs}
",
                true, null,
                $"SPY0276: isinstance-narrowed receiver refuses augmented assignment"));
        }

        // ===== frozenset |= — rebinds, not in-place =====
        // python3 -c "f=frozenset([1,2]); t=f; f|={3}; print(len(t), len(f), f is t)" => 2 3 False

        cells.Add(new Cell("frozenset |=", "identifier", "second-local",
            @"
def main() -> None:
    f: frozenset[int] = frozenset([1, 2])
    t: frozenset[int] = f
    f |= frozenset([3])
    print(len(t))
    print(len(f))
",
            false, "2\n3\n",
            "python3 -c \"f=frozenset([1,2]); t=f; f|={3}; print(len(t)); print(len(f))\" => 2, 3 (rebind)"));

        return cells;
    }

    private static readonly HashSet<(string Op, string Target, string Alias)> NACells = new()
    {
        // nullable-narrowed x all: SPY0276 only fires for isinstance-narrowed (NarrowedReadKind.Cast);
        // nullable-narrowed uses UnwrapOptional/NullableValue/NullForgiving and is NOT refused.
        // The augmented assignment semantics on nullable-narrowed variables are a separate concern.
        ("list +=", "nullable-narrowed", "second-local"),
        ("list +=", "nullable-narrowed", "parameter-alias"),
        ("list +=", "nullable-narrowed", "field-alias"),
        ("list *=", "nullable-narrowed", "second-local"),
        ("list *=", "nullable-narrowed", "parameter-alias"),
        ("list *=", "nullable-narrowed", "field-alias"),
        ("set |=", "nullable-narrowed", "second-local"),
        ("set |=", "nullable-narrowed", "parameter-alias"),
        ("set |=", "nullable-narrowed", "field-alias"),
        ("set &=", "nullable-narrowed", "second-local"),
        ("set &=", "nullable-narrowed", "parameter-alias"),
        ("set &=", "nullable-narrowed", "field-alias"),
        ("set -=", "nullable-narrowed", "second-local"),
        ("set -=", "nullable-narrowed", "parameter-alias"),
        ("set -=", "nullable-narrowed", "field-alias"),
        ("set ^=", "nullable-narrowed", "second-local"),
        ("set ^=", "nullable-narrowed", "parameter-alias"),
        ("set ^=", "nullable-narrowed", "field-alias"),
        ("dict |=", "nullable-narrowed", "second-local"),
        ("dict |=", "nullable-narrowed", "parameter-alias"),
        ("dict |=", "nullable-narrowed", "field-alias"),
        ("frozenset |=", "nullable-narrowed", "second-local"),
        ("frozenset |=", "nullable-narrowed", "parameter-alias"),
        ("frozenset |=", "nullable-narrowed", "field-alias"),

        // isinstance-narrowed x any-alias: SPY0276 refusal is target-level, alias observation doesn't apply
        ("list +=", "isinstance-narrowed", "second-local"),
        ("list +=", "isinstance-narrowed", "parameter-alias"),
        ("list +=", "isinstance-narrowed", "field-alias"),
        ("list *=", "isinstance-narrowed", "second-local"),
        ("list *=", "isinstance-narrowed", "parameter-alias"),
        ("list *=", "isinstance-narrowed", "field-alias"),
        ("set |=", "isinstance-narrowed", "second-local"),
        ("set |=", "isinstance-narrowed", "parameter-alias"),
        ("set |=", "isinstance-narrowed", "field-alias"),
        ("set &=", "isinstance-narrowed", "second-local"),
        ("set &=", "isinstance-narrowed", "parameter-alias"),
        ("set &=", "isinstance-narrowed", "field-alias"),
        ("set -=", "isinstance-narrowed", "second-local"),
        ("set -=", "isinstance-narrowed", "parameter-alias"),
        ("set -=", "isinstance-narrowed", "field-alias"),
        ("set ^=", "isinstance-narrowed", "second-local"),
        ("set ^=", "isinstance-narrowed", "parameter-alias"),
        ("set ^=", "isinstance-narrowed", "field-alias"),
        ("dict |=", "isinstance-narrowed", "second-local"),
        ("dict |=", "isinstance-narrowed", "parameter-alias"),
        ("dict |=", "isinstance-narrowed", "field-alias"),
        ("frozenset |=", "isinstance-narrowed", "second-local"),
        ("frozenset |=", "isinstance-narrowed", "parameter-alias"),
        ("frozenset |=", "isinstance-narrowed", "field-alias"),

        // frozenset x attribute: rebinding on attribute targets is a separate code path not yet supported
        ("frozenset |=", "attribute", "second-local"),
        ("frozenset |=", "attribute", "parameter-alias"),
        ("frozenset |=", "attribute", "field-alias"),

        // frozenset x index: same for index targets
        ("frozenset |=", "index", "second-local"),
        ("frozenset |=", "index", "parameter-alias"),
        ("frozenset |=", "index", "field-alias"),

        // frozenset identifier x parameter-alias / field-alias: rebinding means alias doesn't see mutation
        ("frozenset |=", "identifier", "parameter-alias"),
        ("frozenset |=", "identifier", "field-alias"),
    };

    private static readonly string[] Operators =
    {
        "list +=", "list *=", "set |=", "set &=", "set -=", "set ^=", "dict |=", "frozenset |="
    };

    private static readonly string[] Targets =
    {
        "identifier", "attribute", "index", "nullable-narrowed", "isinstance-narrowed"
    };

    private static readonly string[] AliasObservations =
    {
        "second-local", "parameter-alias", "field-alias"
    };

    [Fact]
    public void TotalityCoverage()
    {
        var cells = BuildCells();
        var cellKeys = cells.Select(c => (c.Operator, c.Target, c.AliasObs)).ToHashSet();

        var missing = new List<string>();
        foreach (var op in Operators)
        {
            foreach (var target in Targets)
            {
                foreach (var alias in AliasObservations)
                {
                    var key = (op, target, alias);
                    if (!cellKeys.Contains(key) && !NACells.Contains(key))
                    {
                        var naKey = (op, target, "N/A");
                        if (!cellKeys.Contains(naKey))
                            missing.Add($"{op} x {target} x {alias}");
                    }
                }
            }
        }

        missing.Should().BeEmpty("every (operator x target x alias) triple must be a cell or declared N/A");
    }

    [Fact]
    public void AllCells_MatchCPython()
    {
        var cells = BuildCells();
        var failures = new StringBuilder();
        int passed = 0;

        foreach (var cell in cells)
        {
            var result = CompileAndExecute(cell.Source);

            if (cell.ExpectsRefusal)
            {
                if (result.Success)
                {
                    failures.AppendLine(
                        $"[{cell.Operator} x {cell.Target} x {cell.AliasObs}] expected SPY0276 refusal but compiled successfully");
                    continue;
                }

                var hasSPY0276 = result.RawDiagnostics.Any(d =>
                    d.Code == DiagnosticCodes.Semantic.NarrowedReceiverAugAssign);

                if (!hasSPY0276)
                {
                    failures.AppendLine(
                        $"[{cell.Operator} x {cell.Target} x {cell.AliasObs}] expected SPY0276 but got: "
                        + string.Join("; ", result.CompilationErrors));
                    continue;
                }

                passed++;
            }
            else
            {
                if (!result.Success)
                {
                    failures.AppendLine(
                        $"[{cell.Operator} x {cell.Target} x {cell.AliasObs}] expected success but got: "
                        + string.Join("; ", result.CompilationErrors));
                    continue;
                }

                if (result.StandardOutput != cell.ExpectedOutput)
                {
                    failures.AppendLine(
                        $"[{cell.Operator} x {cell.Target} x {cell.AliasObs}] output mismatch: "
                        + $"expected {Repr(cell.ExpectedOutput)} got {Repr(result.StandardOutput)}");
                    continue;
                }

                passed++;
            }
        }

        failures.Length.Should().Be(0,
            $"{passed}/{cells.Count} cells passed. Failures:\n{failures}");
    }

    private static string Repr(string? s)
        => s == null ? "(null)" : $"\"{s.Replace("\n", "\\n").Replace("\r", "\\r")}\"";
}
