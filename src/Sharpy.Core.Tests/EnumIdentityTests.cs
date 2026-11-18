using System;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

// Ground truth: CPython 3.15.2, with `class Color(Enum): RED = 1; GREEN = 2` and `class Size(Enum): S = 1`.
//   Color.RED is Color.RED          -> True
//   Color.RED is Color.GREEN        -> False
//   Color.RED is Size.S             -> False (equal values, different enums)
//   loud is Color.RED               -> False, loud.__eq__ not called
//   None is Color.RED               -> False;  None is None -> True
// EnumHelpers.Identity is the lowering of `is` with an enum operand not paired with the same enum (#2669, #2680).
public class EnumIdentityTests
{
    private enum Color { Red = 1, Green = 2 }

    private enum Size { S = 1 }

    /// <summary>A class whose Equals answers True to everything and counts its calls — a user <c>__eq__</c>.</summary>
    private sealed class Loud
    {
        public int EqualsCalls { get; private set; }

        public override bool Equals(object? obj)
        {
            EqualsCalls++;
            return true;
        }

        public override int GetHashCode() => 0;
    }

    [Fact]
    public void SameMember_BoxedSeparately_IsIdentical()
    {
        EnumHelpers.Identity(Color.Red, (object)Color.Red).Should().BeTrue();
    }

    [Fact]
    public void OtherMember_IsNotIdentical()
    {
        EnumHelpers.Identity(Color.Red, Color.Green).Should().BeFalse();
    }

    [Fact]
    public void OtherEnumWithEqualValue_IsNotIdentical()
    {
        EnumHelpers.Identity(Color.Red, Size.S).Should().BeFalse();
        EnumHelpers.Identity(Size.S, Color.Red).Should().BeFalse();
    }

    [Fact]
    public void EnumAgainstItsUnderlyingInt_IsNotIdentical()
    {
        EnumHelpers.Identity(Color.Red, 1).Should().BeFalse();
        EnumHelpers.Identity(1, Color.Red).Should().BeFalse();
    }

    [Fact]
    public void UserEquals_OnEitherSide_IsNeverCalled()
    {
        var loud = new Loud();
        EnumHelpers.Identity(loud, Color.Red).Should().BeFalse();
        EnumHelpers.Identity(Color.Red, loud).Should().BeFalse();
        loud.EqualsCalls.Should().Be(0);

        // Positive control: object.Equals with the user object on the left calls it, which is the #2680 defect.
        object.Equals(loud, Color.Red).Should().BeTrue();
        loud.EqualsCalls.Should().Be(1);
    }

    [Fact]
    public void None_IsIdenticalOnlyToNone()
    {
        EnumHelpers.Identity(null, Color.Red).Should().BeFalse();
        EnumHelpers.Identity(Color.Red, null).Should().BeFalse();
        EnumHelpers.Identity(null, null).Should().BeTrue();
    }

    [Fact]
    public void NonEnumOperands_CompareByReference()
    {
        var loud = new Loud();
        EnumHelpers.Identity(loud, loud).Should().BeTrue();
        EnumHelpers.Identity(loud, new Loud()).Should().BeFalse();
        loud.EqualsCalls.Should().Be(0);
    }
}
