using System;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Pins the arithmetic exception hierarchy (#1189). CPython's MRO is
/// <c>InvalidOperation → DecimalException → ArithmeticError → Exception</c> and
/// <c>ZeroDivisionError/OverflowError → ArithmeticError → Exception</c>; Sharpy omits the
/// <c>DecimalException</c> layer deliberately, so both observable contracts
/// (<c>except InvalidOperation</c>, <c>except ArithmeticError</c>) must still hold.
/// The sibling relationship — <c>InvalidOperation</c> is NOT a <c>ZeroDivisionError</c> —
/// is what makes decimal <c>%</c> by zero uncatchable by <c>except ZeroDivisionError</c>,
/// matching CPython (verified: <c>Decimal(7) % Decimal(0)</c> raises
/// <c>decimal.InvalidOperation</c>, while <c>Decimal(7) // Decimal(0)</c> raises
/// <c>decimal.DivisionByZero</c>, a <c>ZeroDivisionError</c> subclass).
/// </summary>
public class ArithmeticExceptionHierarchy_Tests
{
    [Fact]
    public void InvalidOperation_IsArithmeticError()
    {
        new InvalidOperation("boom").Should().BeAssignableTo<ArithmeticError>();
    }

    [Fact]
    public void ZeroDivisionError_IsArithmeticError()
    {
        new ZeroDivisionError("boom").Should().BeAssignableTo<ArithmeticError>();
    }

    [Fact]
    public void OverflowError_IsArithmeticError()
    {
        new OverflowError("boom").Should().BeAssignableTo<ArithmeticError>();
    }

    [Fact]
    public void ArithmeticError_IsException()
    {
        new ArithmeticError("boom").Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void AllArithmeticExceptions_RemainExceptions()
    {
        // Transitive through ArithmeticError — existing `except Exception` catches are unaffected.
        new InvalidOperation("boom").Should().BeAssignableTo<Exception>();
        new ZeroDivisionError("boom").Should().BeAssignableTo<Exception>();
        new OverflowError("boom").Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void InvalidOperation_IsNotZeroDivisionError()
    {
        // CPython parity: decimal % by zero is InvalidOperation, a *sibling* of
        // ZeroDivisionError — `except ZeroDivisionError` must not catch it.
        typeof(ZeroDivisionError).IsAssignableFrom(typeof(InvalidOperation)).Should().BeFalse();
    }

    [Fact]
    public void ZeroDivisionError_IsNotInvalidOperation()
    {
        typeof(InvalidOperation).IsAssignableFrom(typeof(ZeroDivisionError)).Should().BeFalse();
    }

    [Fact]
    public void ArithmeticError_CatchesAllThreeSubclasses()
    {
        foreach (Exception ex in new Exception[]
        {
            new InvalidOperation("a"),
            new ZeroDivisionError("b"),
            new OverflowError("c")
        })
        {
            var caught = false;
            try
            {
                throw ex;
            }
            catch (ArithmeticError)
            {
                caught = true;
            }

            caught.Should().BeTrue();
        }
    }

    [Fact]
    public void ArithmeticExceptions_PreserveMessageAndInnerException()
    {
        var inner = new ValueError("inner");
        new ArithmeticError("msg").Message.Should().Be("msg");
        new InvalidOperation("msg", inner).InnerException.Should().BeSameAs(inner);
        new ZeroDivisionError("msg", inner).InnerException.Should().BeSameAs(inner);
        new OverflowError("msg", inner).InnerException.Should().BeSameAs(inner);
    }
}
