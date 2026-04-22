using Xunit;

namespace Sharpy.Core.Tests;

public class TemplateTests
{
    [Fact]
    public void Constructor_ValidArguments_Succeeds()
    {
        var template = new Template(
            new[] { "Hello ", "!" },
            new[] { new Interpolation("world", "name", "") });

        Assert.Equal(2, template.Strings.Length);
        Assert.Single(template.Interpolations);
    }

    [Fact]
    public void Constructor_MismatchedLengths_Throws()
    {
        Assert.Throws<System.ArgumentException>(() =>
            new Template(
                new[] { "Hello" },
                new[] { new Interpolation("world", "name", "") }));
    }

    [Fact]
    public void Constructor_NullStrings_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            new Template(null!, new Interpolation[0]));
    }

    [Fact]
    public void Constructor_NullInterpolations_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            new Template(new[] { "hello" }, null!));
    }

    [Fact]
    public void ToString_NoInterpolations_ReturnsLiteralString()
    {
        var template = new Template(
            new[] { "Hello world" },
            System.Array.Empty<Interpolation>());

        Assert.Equal("Hello world", template.ToString());
    }

    [Fact]
    public void ToString_SingleInterpolation_FormatsLikeFString()
    {
        var template = new Template(
            new[] { "Hello ", "" },
            new[] { new Interpolation("world", "name", "") });

        Assert.Equal("Hello world", template.ToString());
    }

    [Fact]
    public void ToString_MultipleInterpolations_FormatsCorrectly()
    {
        var template = new Template(
            new[] { "", " is ", " years old" },
            new[]
            {
                new Interpolation("Alice", "name", ""),
                new Interpolation(30, "age", "")
            });

        Assert.Equal("Alice is 30 years old", template.ToString());
    }

    [Fact]
    public void Values_ReturnsInterpolationValues()
    {
        var template = new Template(
            new[] { "", " + ", "" },
            new[]
            {
                new Interpolation(1, "a", ""),
                new Interpolation(2, "b", "")
            });

        Assert.Equal(new object[] { 1, 2 }, template.Values);
    }

    [Fact]
    public void Concat_TwoTemplates_MergesCorrectly()
    {
        var left = new Template(
            new[] { "Hello ", "" },
            new[] { new Interpolation("world", "name", "") });

        var right = new Template(
            new[] { "! You are ", "." },
            new[] { new Interpolation(42, "age", "") });

        var result = left + right;

        Assert.Equal("Hello world! You are 42.", result.ToString());
        Assert.Equal(3, result.Strings.Length);
        Assert.Equal(2, result.Interpolations.Length);
        Assert.Equal("Hello ", result.Strings[0]);
        Assert.Equal("! You are ", result.Strings[1]);
        Assert.Equal(".", result.Strings[2]);
    }

    [Fact]
    public void Concat_BothNoInterpolations_MergesStrings()
    {
        var left = new Template(new[] { "Hello " }, System.Array.Empty<Interpolation>());
        var right = new Template(new[] { "world" }, System.Array.Empty<Interpolation>());

        var result = left + right;

        Assert.Equal("Hello world", result.ToString());
        Assert.Single(result.Strings);
        Assert.Equal("Hello world", result.Strings[0]);
    }

    [Fact]
    public void Repr_NoInterpolations_FormatsCorrectly()
    {
        var template = new Template(
            new[] { "hello" },
            System.Array.Empty<Interpolation>());

        Assert.Equal("Template(strings=['hello'], interpolations=[])", template.Repr());
    }

    [Fact]
    public void Repr_WithInterpolation_FormatsCorrectly()
    {
        var template = new Template(
            new[] { "Hello ", "" },
            new[] { new Interpolation("world", "name", "") });

        Assert.Equal("Template(strings=['Hello ', ''], interpolations=[Interpolation(world, 'name')])", template.Repr());
    }

    [Fact]
    public void GetEnumerator_YieldsInterleavedParts()
    {
        var template = new Template(
            new[] { "Hello ", " and ", "" },
            new[]
            {
                new Interpolation("world", "name", ""),
                new Interpolation("goodbye", "farewell", "")
            });

        var parts = new System.Collections.Generic.List<object>();
        foreach (var part in template)
        {
            parts.Add(part);
        }

        Assert.Equal(4, parts.Count);
        Assert.Equal("Hello ", parts[0]);
        Assert.IsType<Interpolation>(parts[1]);
        Assert.Equal(" and ", parts[2]);
        Assert.IsType<Interpolation>(parts[3]);
    }

    [Fact]
    public void GetEnumerator_SkipsEmptyStrings()
    {
        var template = new Template(
            new[] { "", "" },
            new[] { new Interpolation("value", "x", "") });

        var parts = new System.Collections.Generic.List<object>();
        foreach (var part in template)
        {
            parts.Add(part);
        }

        // Empty strings at start and end are skipped
        Assert.Single(parts);
        Assert.IsType<Interpolation>(parts[0]);
    }
}

public class InterpolationTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var interp = new Interpolation(42, "x", ".2f");

        Assert.Equal(42, interp.Value);
        Assert.Equal("x", interp.Expression);
        Assert.Equal(".2f", interp.FormatSpec);
    }

    [Fact]
    public void Constructor_NullExpression_DefaultsToEmpty()
    {
        var interp = new Interpolation(42, null!, "");

        Assert.Equal(string.Empty, interp.Expression);
    }

    [Fact]
    public void Constructor_NullFormatSpec_DefaultsToEmpty()
    {
        var interp = new Interpolation(42, "x", null!);

        Assert.Equal(string.Empty, interp.FormatSpec);
    }

    [Fact]
    public void ToString_NoFormatSpec_ReturnsValueString()
    {
        var interp = new Interpolation(42, "x", "");

        Assert.Equal("42", interp.ToString());
    }

    [Fact]
    public void ToString_WithFormatSpec_FormatsValue()
    {
        var interp = new Interpolation(3.14159, "pi", "F2");

        Assert.Equal("3.14", interp.ToString());
    }

    [Fact]
    public void ToString_NullValue_ReturnsNone()
    {
        var interp = new Interpolation(null!, "x", "");

        Assert.Equal("None", interp.ToString());
    }

    [Fact]
    public void Repr_NoFormatSpec_FormatsCorrectly()
    {
        var interp = new Interpolation("hello", "greeting", "");

        Assert.Equal("Interpolation(hello, 'greeting')", interp.Repr());
    }

    [Fact]
    public void Repr_WithFormatSpec_IncludesFormatSpec()
    {
        var interp = new Interpolation(3.14, "pi", ".2f");

        Assert.Equal("Interpolation(3.14, 'pi', '.2f')", interp.Repr());
    }
}
