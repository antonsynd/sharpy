using System;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Pins the runtime-checked-local primitives the compiler emits for a local whose assignment a
/// suppression-capable <c>with</c> block can skip (#1839, R-AI): <see cref="UnboundLocalError"/>
/// with Python 3.12's exact wording, <see cref="Builtins.Assigned{T}"/> (set-flag-and-forward), and
/// <see cref="Builtins.CheckedLocal{T}"/> (throw-if-unset).
/// </summary>
public class RuntimeCheckedLocalsTests
{
    [Fact]
    public void UnboundLocalError_ForName_MatchesPython312Wording()
    {
        // python3 3.12: str(UnboundLocalError) for a local `n` read before assignment.
        UnboundLocalError.ForName("n").Message.Should().Be(
            "cannot access local variable 'n' where it is not associated with a value");
    }

    [Fact]
    public void UnboundLocalError_IsAnException()
    {
        UnboundLocalError.ForName("x").Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void Assigned_SetsTheFlagAndForwardsTheValue()
    {
        bool flag = false;
        var result = Builtins.Assigned(ref flag, 5);
        result.Should().Be(5);
        flag.Should().BeTrue();
    }

    [Fact]
    public void Assigned_WorksInExpressionPosition()
    {
        // The walrus/expression use: the flag is set as a side effect of yielding the value.
        bool flag = false;
        int captured = Builtins.CheckedLocal(true, Builtins.Assigned(ref flag, 7), "n") + 1;
        captured.Should().Be(8);
        flag.Should().BeTrue();
    }

    [Fact]
    public void CheckedLocal_ReturnsTheValueWhenTheFlagIsSet()
    {
        Builtins.CheckedLocal(true, 42, "n").Should().Be(42);
    }

    [Fact]
    public void CheckedLocal_ThrowsUnboundLocalErrorNamingTheVariableWhenUnset()
    {
        var act = () => Builtins.CheckedLocal(false, 0, "n");
        act.Should().Throw<UnboundLocalError>()
            .WithMessage("cannot access local variable 'n' where it is not associated with a value");
    }
}
