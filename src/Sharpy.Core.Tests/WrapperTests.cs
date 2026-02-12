using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for the Wrapper test helper class.
/// Note: Interface checks have been removed since the Sharpy interfaces have been migrated
/// to standard .NET interfaces.
/// </summary>
public class Wrapper_Tests
{
    [Fact]
    public void Wrapper_Constructor()
    {
        // If
        Wrapper<int>.ResetId();
        var wrapper = new Wrapper<int>(1);

        // When/then
        wrapper.Id.Should().Be(0);
        wrapper.Value.Should().Be(1);
    }

    [Fact]
    public void Wrapper_Implicit_Conversion()
    {
        // If
        Wrapper<int>.ResetId();
        Wrapper<int> wrapper = 1;

        // When/then
        wrapper.Id.Should().Be(0);
        wrapper.Value.Should().Be(1);
    }

    [Fact]
    public void Wrapper_Bool_Convertible()
    {
        // If
        var wrapper = new Wrapper<int>(1);
        // When/then
        wrapper.IsTrue.Should().BeTrue();

        // If
        wrapper = new Wrapper<int>(0);
        // When/then
        wrapper.IsTrue.Should().BeFalse();
    }

    [Fact]
    public void Wrapper_Implicit_True_False()
    {
        // If/when/then
        (new Wrapper<int>(0) ? true : false).Should().BeFalse();
        (new Wrapper<int>(1) ? true : false).Should().BeTrue();
    }

    [Fact]
    public void Wrapper_Hashable()
    {
        // If
        var wrapper0 = new Wrapper<int>(0);
        var wrapper1 = new Wrapper<int>(1);

        // When/then
        wrapper0.__Hash__().Should().NotBe(wrapper1.__Hash__());
        wrapper0.__Hash__().Should().Be(wrapper0.GetHashCode());
        wrapper1.__Hash__().Should().Be(wrapper1.GetHashCode());
    }

    [Fact]
    public void Wrapper_Equatable()
    {
        // If
        typeof(Wrapper<int>).IsAssignableTo(typeof(IEquatable<Wrapper<int>>)).Should().BeTrue();
        var wrapper0 = new Wrapper<int>(0);
        var wrapper1_0 = new Wrapper<int>(1);
        var wrapper1_1 = new Wrapper<int>(1);

        wrapper1_0.__Hash__().Should().NotBe(wrapper1_1.__Hash__());

        // When/then
        wrapper0.Equals(wrapper1_0).Should().BeFalse();
        wrapper0.Equals(wrapper1_1).Should().BeFalse();
        wrapper0.Equals((Wrapper<int>)null!).Should().BeFalse();

        // Identity
        wrapper0.Equals(wrapper0).Should().BeTrue();
        wrapper1_0.Equals(wrapper1_0).Should().BeTrue();
        wrapper1_1.Equals(wrapper1_1).Should().BeTrue();

        // Symmetric
        wrapper1_0.Equals(wrapper1_1).Should().BeTrue();
        wrapper1_1.Equals(wrapper1_0).Should().BeTrue();
    }

    [Fact]
    public void Wrapper_Inequatable()
    {
        // If
        var wrapper0 = new Wrapper<int>(0);
        var wrapper1_0 = new Wrapper<int>(1);
        var wrapper1_1 = new Wrapper<int>(1);

        wrapper1_0.__Hash__().Should().NotBe(wrapper1_1.__Hash__());

        // When/then
        wrapper0.__Ne__(wrapper1_0).Should().BeTrue();
        wrapper0.__Ne__(wrapper1_1).Should().BeTrue();
        wrapper0.__Ne__(null!).Should().BeTrue();

        // Identity
        wrapper0.__Ne__(wrapper0).Should().BeFalse();
        wrapper1_0.__Ne__(wrapper1_0).Should().BeFalse();
        wrapper1_1.__Ne__(wrapper1_1).Should().BeFalse();

        // Symmetric
        wrapper1_0.__Ne__(wrapper1_1).Should().BeFalse();
        wrapper1_1.__Ne__(wrapper1_0).Should().BeFalse();
    }

    [Fact]
    public void Wrapper_IEquatable()
    {
        // If
        typeof(Wrapper<int>).IsAssignableTo(typeof(IEquatable<Wrapper<int>>)).Should().BeTrue();

        var wrapper0 = new Wrapper<int>(0);
        var wrapper1_0 = new Wrapper<int>(1);
        var wrapper1_1 = new Wrapper<int>(1);

        wrapper1_0.__Hash__().Should().NotBe(wrapper1_1.__Hash__());

        // When/then
        wrapper0.Equals(wrapper1_0).Should().BeFalse();
        wrapper0.Equals(wrapper1_1).Should().BeFalse();
        wrapper0.Equals(null).Should().BeFalse();


        // Identity
        wrapper0.Equals(wrapper0).Should().BeTrue();
        wrapper1_0.Equals(wrapper1_0).Should().BeTrue();
        wrapper1_1.Equals(wrapper1_1).Should().BeTrue();

        // Symmetric
        wrapper1_0.Equals(wrapper1_1).Should().BeTrue();
        wrapper1_1.Equals(wrapper1_0).Should().BeTrue();
    }

    [Fact]
    public void Wrapper_Identifiable()
    {
        // If
        var wrapper0 = new Wrapper<int>(0);
        var wrapper1 = new Wrapper<int>(1);

        // When/then
        wrapper0.__Id__().Should().NotBe(wrapper0.__Hash__());
        wrapper1.__Id__().Should().NotBe(wrapper1.__Hash__());
        wrapper0.__Id__().Should().NotBe(wrapper1.__Id__());
    }

    [Fact]
    public void Wrapper_Representable()
    {
        // If
        var wrapper0 = new Wrapper<int>(0);
        var wrapper1 = new Wrapper<int>(1);

        // When/then
        wrapper0.__Repr__().Length.Should().BeGreaterThan(0);
        wrapper0.__Repr__().Should().NotBe(wrapper1.__Repr__());
    }
}
