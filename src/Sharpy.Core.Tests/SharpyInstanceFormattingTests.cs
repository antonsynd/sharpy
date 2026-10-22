using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// #2057, R-HO: an instance of a Sharpy class is never a plain CLR sequence. A class without
/// <c>__str__</c>/<c>__repr__</c> prints python's <c>&lt;mod.C object&gt;</c> (R-CF, the address
/// omitted) at every formatting site, and formatting it never iterates it. Before the cure
/// <see cref="Builtins.Str(object)"/> and <see cref="Builtins.Repr(object)"/> ran the #1453
/// plain-sequence arm first, so a class with <c>__iter__</c>/<c>__next__</c> printed
/// <c>['item', 'item']</c> from <c>str</c> and <c>[]</c> everywhere after, and a loop afterwards saw
/// 0 items. The nested classes mirror what the compiler emits (read from <c>emit csharp</c>):
/// <c>__iter__</c> returning <c>Self</c> plus <c>__next__</c> makes the class its own
/// <c>IEnumerator&lt;string&gt;</c>; a generator <c>__iter__</c> is a re-enumerable
/// <c>IEnumerable&lt;int&gt;</c>; <c>__str__</c> and <c>__repr__</c> are each a <c>ToString</c>
/// override until R-CP gives <c>__repr__</c> its own <c>IRepr</c> member.
/// python3 3.12, each class defined in <c>__main__</c>: <c>str</c>/<c>repr</c>/<c>print</c>/
/// <c>f"{c}"</c> give <c>&lt;__main__.Counter object at …&gt;</c>, <c>[c]</c> gives
/// <c>[&lt;__main__.Counter object at …&gt;]</c>, and a loop afterwards counts 2.
/// </summary>
[Collection("ConsoleCapture")]
public class SharpyInstanceFormattingTests : IDisposable
{
    private readonly TextWriter _originalOut = Console.Out;

    public void Dispose()
    {
        Console.SetOut(_originalOut);
    }

    /// <summary>`class Counter` with `__iter__ -> Self` and `__next__` only.</summary>
    [SharpyModuleType("__main__", "Counter")]
    public class IterOnlyCounter : IEnumerator<string>, IEnumerable<string>
    {
        private int _index;
        private string _current = "";

        public IEnumerator<string> GetEnumerator() => this;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool MoveNext()
        {
            if (_index >= 2)
            {
                return false;
            }

            _index++;
            _current = "item";
            return true;
        }

        public string Current => _current;
        object IEnumerator.Current => _current;
        public void Reset() => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }

    /// <summary>The same class plus `__str__` returning "Counter!".</summary>
    [SharpyModuleType("__main__", "Counter")]
    public class StrCounter : IterOnlyCounter
    {
        public override string ToString() => "Counter!";
    }

    /// <summary>The same class plus `__repr__` returning "Counter()".</summary>
    [SharpyModuleType("__main__", "Counter")]
    public class ReprCounter : IterOnlyCounter
    {
        public override string ToString() => "Counter()";
    }

    /// <summary>`class Bag` whose `__iter__` is a generator (re-enumerable).</summary>
    [SharpyModuleType("__main__", "Bag")]
    public class GeneratorBag : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator()
        {
            yield return 1;
            yield return 2;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>`class Seq` with `__len__` and `__getitem__` (positive control: not enumerable).</summary>
    [SharpyModuleType("__main__", "Seq")]
    public class LenGetitemSeq : ISized
    {
        public virtual int Count => 2;
        public string this[int i] => "item";
    }

    /// <summary>`class Plain` with no dunders (positive control).</summary>
    [SharpyModuleType("__main__", "Plain")]
    public class Plain
    {
        public int X = 1;
    }

    private static object Make(string dunders) => dunders switch
    {
        "iter-next" => new IterOnlyCounter(),
        "iter-next+str" => new StrCounter(),
        "iter-next+repr" => new ReprCounter(),
        "iter-generator" => new GeneratorBag(),
        "len+getitem" => new LenGetitemSeq(),
        "none" => new Plain(),
        _ => throw new ArgumentOutOfRangeException(nameof(dunders), dunders, null),
    };

    private string Format(object value, string site)
    {
        switch (site)
        {
            case "str":
                return Builtins.Str(value);
            case "repr":
                return Builtins.Repr(value);
            case "print":
                var writer = new StringWriter();
                Console.SetOut(writer);
                Builtins.Print(value);
                Console.SetOut(_originalOut);
                return writer.ToString().TrimEnd('\r', '\n');
            case "fstring-hole":
                return PyFormat.Apply(value, "");
            case "list-element":
                return Builtins.Str(new List<object> { value });
            default:
                throw new ArgumentOutOfRangeException(nameof(site), site, null);
        }
    }

    /// <summary>
    /// dunder set × site. <c>expected</c> is the R-CF/python text; <c>null</c> marks the R-CP cell
    /// (a <c>__str__</c>-only class's repr is python's <c>&lt;mod.C object&gt;</c>, still
    /// <c>Counter!</c> here until R-CP lands on #2057), where only the no-iteration half of the
    /// contract is asserted. <c>itemsAfter</c> is what a loop sees after the site formatted the
    /// value (python: 2), or -1 for a class that is not iterable.
    /// </summary>
    [Theory]
    [InlineData("iter-next", "str", "<__main__.Counter object>", 2)]
    [InlineData("iter-next", "repr", "<__main__.Counter object>", 2)]
    [InlineData("iter-next", "print", "<__main__.Counter object>", 2)]
    [InlineData("iter-next", "fstring-hole", "<__main__.Counter object>", 2)]
    [InlineData("iter-next", "list-element", "[<__main__.Counter object>]", 2)]
    [InlineData("iter-next+str", "str", "Counter!", 2)]
    [InlineData("iter-next+str", "repr", null, 2)]
    [InlineData("iter-next+str", "print", "Counter!", 2)]
    [InlineData("iter-next+str", "fstring-hole", "Counter!", 2)]
    [InlineData("iter-next+str", "list-element", null, 2)]
    [InlineData("iter-next+repr", "str", "Counter()", 2)]
    [InlineData("iter-next+repr", "repr", "Counter()", 2)]
    [InlineData("iter-next+repr", "print", "Counter()", 2)]
    [InlineData("iter-next+repr", "fstring-hole", "Counter()", 2)]
    [InlineData("iter-next+repr", "list-element", "[Counter()]", 2)]
    [InlineData("iter-generator", "str", "<__main__.Bag object>", 2)]
    [InlineData("iter-generator", "repr", "<__main__.Bag object>", 2)]
    [InlineData("iter-generator", "print", "<__main__.Bag object>", 2)]
    [InlineData("iter-generator", "fstring-hole", "<__main__.Bag object>", 2)]
    [InlineData("iter-generator", "list-element", "[<__main__.Bag object>]", 2)]
    [InlineData("len+getitem", "str", "<__main__.Seq object>", -1)]
    [InlineData("len+getitem", "repr", "<__main__.Seq object>", -1)]
    [InlineData("len+getitem", "print", "<__main__.Seq object>", -1)]
    [InlineData("len+getitem", "fstring-hole", "<__main__.Seq object>", -1)]
    [InlineData("len+getitem", "list-element", "[<__main__.Seq object>]", -1)]
    [InlineData("none", "str", "<__main__.Plain object>", -1)]
    [InlineData("none", "repr", "<__main__.Plain object>", -1)]
    [InlineData("none", "print", "<__main__.Plain object>", -1)]
    [InlineData("none", "fstring-hole", "<__main__.Plain object>", -1)]
    [InlineData("none", "list-element", "[<__main__.Plain object>]", -1)]
    public void SharpyInstance_FormatsAsItself_AndIsNotIterated(string dunders, string site, string? expected, int itemsAfter)
    {
        var value = Make(dunders);

        var text = Format(value, site);

        if (expected != null)
        {
            text.Should().Be(expected);
        }

        text.Should().NotContain("item", "formatting must not enumerate the instance");
        if (itemsAfter >= 0)
        {
            var seen = 0;
            foreach (var unused in (IEnumerable)value)
            {
                seen++;
            }

            seen.Should().Be(itemsAfter, "formatting the instance must not consume it");
        }
        else
        {
            value.Should().NotBeAssignableTo<IEnumerable>();
        }
    }

    /// <summary>
    /// R-IF (#2057): a Sharpy generator object prints python's <c>&lt;generator object qualname&gt;</c>
    /// (the <c>at 0x…</c> address omitted, R-CI) at every formatting site, and formatting it never
    /// runs the body. The generator functions below mirror what the compiler emits (read from
    /// <c>emit csharp</c>): a C# iterator method stamped <see cref="SharpyGeneratorAttribute"/> with
    /// its python <c>__qualname__</c>. Before the cure the state machine was the plain sequence
    /// #1453's arm printed — <c>[1, 2]</c>, the body run once per formatting site. python3 3.12:
    /// <c>print(gen())</c> → <c>&lt;generator object gen at 0x…&gt;</c>, <c>K().items()</c> →
    /// <c>&lt;generator object K.items at 0x…&gt;</c>, body not run.
    /// </summary>
    [Theory]
    [InlineData("module-function", "str", "<generator object gen>")]
    [InlineData("module-function", "repr", "<generator object gen>")]
    [InlineData("module-function", "print", "<generator object gen>")]
    [InlineData("module-function", "fstring-hole", "<generator object gen>")]
    [InlineData("module-function", "list-element", "[<generator object gen>]")]
    [InlineData("method", "str", "<generator object Bag.items>")]
    [InlineData("method", "repr", "<generator object Bag.items>")]
    [InlineData("method", "print", "<generator object Bag.items>")]
    [InlineData("method", "fstring-hole", "<generator object Bag.items>")]
    [InlineData("method", "list-element", "[<generator object Bag.items>]")]
    [InlineData("generic-function", "str", "<generator object repeat>")]
    [InlineData("generic-function", "list-element", "[<generator object repeat>]")]
    [InlineData("local-function", "str", "<generator object main.<locals>.inner>")]
    [InlineData("local-function", "repr", "<generator object main.<locals>.inner>")]
    public void GeneratorObject_PrintsItsQualifiedName_AndTheBodyDoesNotRun(string producer, string site, string expected)
    {
        GeneratorModule.BodyRuns = 0;
        IEnumerable value = producer switch
        {
            "module-function" => GeneratorModule.Gen(),
            "method" => new StampedGeneratorOwner().Items(),
            "generic-function" => GeneratorModule.Repeat("x"),
            "local-function" => GeneratorModule.LocalGenerator(),
            _ => throw new ArgumentOutOfRangeException(nameof(producer), producer, null),
        };

        var text = Format(value, site);

        text.Should().Be(expected);
        GeneratorModule.BodyRuns.Should().Be(0, "formatting a generator object must not run its body");
        var seen = 0;
        foreach (var unused in value)
        {
            seen++;
        }

        seen.Should().BeGreaterThan(0, "the generator still yields when iterated after it was formatted");
    }

    /// <summary>
    /// Negative control for the generator arm: an iterator the compiler did NOT stamp (any CLR
    /// iterator method — here one nested in a static class, as a Sharpy module function's is) is the
    /// lazy sequence #1453 renders as the list its semantic type says it is.
    /// </summary>
    [Fact]
    public void UnstampedIterator_KeepsThePlainSequenceRendering()
    {
        IEnumerable<int> generated = UnstampedModule.Gen();

        Builtins.Str(generated).Should().Be("[1, 2]");
        Builtins.Repr(generated).Should().Be("[1, 2]");
    }

    /// <summary>
    /// Negative control for the stamp test: <c>"abc".reverse()</c> is LINQ's select iterator,
    /// a CLR type nested in the static <c>System.Linq.Enumerable</c> — which the instance
    /// fallback's "nested in a static class" rule would call Sharpy-declared. It is the plain
    /// sequence #1453 renders as the list its semantic type says it is.
    /// </summary>
    [Fact]
    public void LinqIteratorNestedInStaticClass_KeepsThePlainSequenceRendering()
    {
        IEnumerable<string> reversed = System.Linq.Enumerable.Select(new[] { 'c', 'b', 'a' }, c => c.ToString());
        var declaring = reversed.GetType().DeclaringType;
        declaring.Should().NotBeNull();
        declaring!.IsAbstract.Should().BeTrue("the precondition is a type nested in a static class");
        declaring.IsSealed.Should().BeTrue();

        Builtins.Str(reversed).Should().Be("['c', 'b', 'a']");
        Builtins.Repr(reversed).Should().Be("['c', 'b', 'a']");
    }

    /// <summary>`def gen()`, `def repeat[T](v: T)`, and `def main()` declaring `def inner()`.</summary>
    public static class GeneratorModule
    {
        public static int BodyRuns;

        [SharpyGenerator("gen")]
        public static IEnumerable<int> Gen()
        {
            BodyRuns++;
            yield return 1;
            yield return 2;
        }

        [SharpyGenerator("repeat")]
        public static IEnumerable<T> Repeat<T>(T value)
        {
            BodyRuns++;
            yield return value;
        }

        public static IEnumerable<int> LocalGenerator()
        {
            [SharpyGenerator("main.<locals>.inner")]
            static IEnumerable<int> Inner()
            {
                BodyRuns++;
                yield return 3;
            }

            return Inner();
        }
    }

    /// <summary>`class Bag` with the generator method `def items(self)`.</summary>
    [SharpyModuleType("__main__", "Bag")]
    public class StampedGeneratorOwner
    {
        [SharpyGenerator("Bag.items")]
        public IEnumerable<int> Items()
        {
            GeneratorModule.BodyRuns++;
            yield return 4;
        }
    }

    public static class UnstampedModule
    {
        public static IEnumerable<int> Gen()
        {
            yield return 1;
            yield return 2;
        }
    }
}
