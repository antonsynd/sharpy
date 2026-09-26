using System;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// A string-backed enum is CPython's <c>StrEnum</c>, a str subclass. python3 3.12 with
/// <c>class Mood(StrEnum): HAPPY = 'h'</c>: <c>format(Mood.HAPPY, 'd')</c> raises
/// <c>ValueError: Unknown format code 'd' for object of type 'Mood'</c>, <c>format(Mood.HAPPY, '&gt;3')</c>
/// is <c>'  h'</c>, and <c>str(type(Mood.HAPPY))</c> is <c>&lt;enum 'Mood'&gt;</c>. The integer enum
/// is a plain <c>Enum</c>, whose message names <c>'str'</c> (<c>format(Color.RED, 'd')</c> with
/// <c>class Color(Enum)</c>). <see cref="Mood"/> mirrors the class the compiler emits for
/// <c>enum Mood: HAPPY = "h"</c>, stamped with <see cref="SharpyStrEnumAttribute"/>.
/// </summary>
public class StrEnumFormatTests
{
    [SharpyStrEnum]
    [SharpyModuleType("__main__", "Mood")]
    public sealed class Mood : IFormattable, IRepr
    {
        private Mood(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public string Value { get; }

        public static readonly Mood HAPPY = new Mood("HAPPY", "h");
        public override string ToString() => Value;
        public string ToString(string? format, IFormatProvider? formatProvider) => PyFormat.Apply(Value, format ?? "");
        string IRepr.Repr() => "<" + PyFormat.PyTypeName(GetType()) + "." + Name + ": " + Builtins.Repr(Value) + ">";
        public static implicit operator string(Mood value) => value.Value;
    }

    public enum Color
    {
        RED = 1,
    }

    [Fact]
    public void StrEnum_RefusedSpec_NamesTheEnumClass()
    {
        FluentActions.Invoking(() => PyFormat.Apply(Mood.HAPPY, "d"))
            .Should().Throw<ValueError>().WithMessage("Unknown format code 'd' for object of type 'Mood'");
        PyFormat.FormatOperandTypeName(typeof(Mood)).Should().Be("Mood");
    }

    [Fact]
    public void StrEnum_AcceptedSpec_FormatsItsValueAsAStr()
    {
        PyFormat.Apply(Mood.HAPPY, ">3").Should().Be("  h");
        PyFormat.Apply(Mood.HAPPY, "").Should().Be("h");
    }

    [Fact]
    public void IntEnum_RefusedSpec_StillNamesStr()
    {
        FluentActions.Invoking(() => PyFormat.Apply(Color.RED, "d"))
            .Should().Throw<ValueError>().WithMessage("Unknown format code 'd' for object of type 'str'");
    }

    [Fact]
    public void StrEnumClass_RendersAsEnumType()
    {
        Builtins.Str(typeof(Mood)).Should().Be("<enum 'Mood'>");
        Builtins.Repr(typeof(Mood)).Should().Be("<enum 'Mood'>");
    }
}
