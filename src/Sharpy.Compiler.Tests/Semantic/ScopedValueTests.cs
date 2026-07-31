using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Direct tests for <see cref="ScopedValue{T}"/> (#1218), the scoped-restore primitive the
/// <see cref="TypeChecker"/>'s <c>_current*</c> expression-context tracker family is written against.
///
/// <para>The exception test is the reason this type exists. The hand-rolled
/// <c>save / set / restore</c> idiom it replaces has no <c>try</c>/<c>finally</c>, so an exception
/// thrown between the assignment and the restore leaves the tracker holding a node from an
/// abandoned expression. The scoped form restores on every exit path, including that one.</para>
///
/// <para>The tracker under test is a <b>field</b> rather than a local because
/// <see cref="ScopedValue{T}"/> is a <c>ref struct</c>: it cannot be captured by a lambda, so the
/// natural <c>Assert.Throws(() =&gt; { … })</c> over a local does not compile. The scopes are opened
/// in the test body and the exception is caught with a plain <c>try</c>/<c>catch</c>.</para>
/// </summary>
public class ScopedValueTests
{
    /// <summary>
    /// Stands in for one of the checker's tracker fields (<c>_typeTestOperand</c>,
    /// <c>_currentBindingValue</c>, …). The real fields hold AST node references; a string carries
    /// the same identity semantics for these tests and reads better in failure messages.
    /// </summary>
    private string? _tracker;

    [Fact]
    public void Push_RestoresThePreviousValue_OnNormalExit()
    {
        _tracker = "enclosing";

        using (ScopedValue.Push(ref _tracker, "scoped"))
        {
            _tracker.Should().Be("scoped", "the scope assigns on construction");
        }

        _tracker.Should().Be("enclosing", "disposal restores the value the field held on entry");
    }

    /// <summary>
    /// The property the hand-rolled sites lack: an exception thrown inside the scope still restores,
    /// through every scope it unwinds, and the exception itself is not swallowed. Two stacked scopes
    /// are used because the checker nests them — <c>Calls.cs</c>' argument scope sits lexically inside
    /// the type-test scope — and a restore that only ran for the innermost frame would still leave a
    /// stale tracker behind.
    /// </summary>
    [Fact]
    public void Push_RestoresThroughEveryScope_WhenTheBodyThrows()
    {
        _tracker = "enclosing";
        InvalidOperationException? caught = null;

        try
        {
            using (ScopedValue.Push(ref _tracker, "outer"))
            {
                _tracker.Should().Be("outer");

                using (ScopedValue.Push(ref _tracker, "inner"))
                {
                    _tracker.Should().Be("inner");
                    throw new InvalidOperationException("checker threw mid-scope");
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull("the scope must let the exception propagate, not swallow it");
        caught!.Message.Should().Be("checker threw mid-scope");
        _tracker.Should().Be(
            "enclosing",
            "unwinding through both scopes restores the enclosing value; the hand-rolled "
            + "save/set/restore idiom this replaces would have left 'inner' behind");
    }

    [Fact]
    public void Push_Nests_InnerRestoresToOuter_AndOuterRestoresToTheOriginal()
    {
        _tracker = "original";

        using (ScopedValue.Push(ref _tracker, "outer"))
        {
            _tracker.Should().Be("outer");

            using (ScopedValue.Push(ref _tracker, "inner"))
            {
                _tracker.Should().Be("inner");
            }

            _tracker.Should().Be(
                "outer", "the inner scope restores to the outer value, not to the original");
        }

        _tracker.Should().Be("original");
    }

    /// <summary>
    /// Pushing the field's current value is a no-op round-trip. This is the conditional-site pattern:
    /// <c>Calls.cs</c>' type-test scopes and <c>Operators.cs</c>' operand scope assign only when the
    /// callee really is a type test, and pass the field's current value otherwise so the
    /// <b>enclosing</b> context survives the scope instead of being cleared.
    /// </summary>
    [Fact]
    public void Push_OfTheFieldsCurrentValue_LeavesTheEnclosingValueIntact()
    {
        _tracker = "enclosing type test operand";

        // Spelled the way the conditional sites spell it, so the shape under test is the shape in
        // the checker rather than a literal self-assignment.
        var callee = "print";
        var isTypeTest = callee == "isinstance";

        using (ScopedValue.Push(ref _tracker, isTypeTest ? "isinstance operand" : _tracker))
        {
            _tracker.Should().Be(
                "enclosing type test operand",
                "a false condition must not clear the context the enclosing type test established");
        }

        _tracker.Should().Be("enclosing type test operand");
    }
}
