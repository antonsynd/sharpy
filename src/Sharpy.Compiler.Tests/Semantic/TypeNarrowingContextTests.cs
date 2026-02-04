using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for the TypeNarrowingContext class which manages type narrowing
/// within conditional contexts using a stack-based scope model.
/// </summary>
public class TypeNarrowingContextTests
{
    #region Basic Operations

    [Fact]
    public void Constructor_CreatesContextWithRootScope()
    {
        var context = new TypeNarrowingContext();

        context.ScopeDepth.Should().Be(1);
    }

    [Fact]
    public void Narrow_AndGetNarrowedType_Roundtrip()
    {
        var context = new TypeNarrowingContext();

        context.Narrow("x", SemanticType.Str);

        var result = context.GetNarrowedType("x");
        result.Should().Be(SemanticType.Str);
    }

    [Fact]
    public void GetNarrowedType_ReturnsNull_ForUnknownName()
    {
        var context = new TypeNarrowingContext();

        var result = context.GetNarrowedType("unknown");

        result.Should().BeNull();
    }

    [Fact]
    public void TryGetNarrowedType_ReturnsTrue_WhenFound()
    {
        var context = new TypeNarrowingContext();
        context.Narrow("x", SemanticType.Int);

        var found = context.TryGetNarrowedType("x", out var type);

        found.Should().BeTrue();
        type.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void TryGetNarrowedType_ReturnsFalse_WhenNotFound()
    {
        var context = new TypeNarrowingContext();

        var found = context.TryGetNarrowedType("unknown", out var type);

        found.Should().BeFalse();
        type.Should().BeNull();
    }

    #endregion

    #region Scope Isolation

    [Fact]
    public void EnterScope_IncreasesScopeDepth()
    {
        var context = new TypeNarrowingContext();
        var initialDepth = context.ScopeDepth;

        using (context.EnterScope())
        {
            context.ScopeDepth.Should().Be(initialDepth + 1);
        }

        context.ScopeDepth.Should().Be(initialDepth);
    }

    [Fact]
    public void Dispose_RestoresPreviousScope()
    {
        var context = new TypeNarrowingContext();
        context.Narrow("outer", SemanticType.Int);

        using (context.EnterScope())
        {
            context.Narrow("inner", SemanticType.Str);
            context.GetNarrowedType("inner").Should().Be(SemanticType.Str);
        }

        // Inner narrowing should be gone after scope exit
        context.GetNarrowedType("inner").Should().BeNull();
        // Outer narrowing should still exist
        context.GetNarrowedType("outer").Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InnerScope_DoesNotAffectOuterScope()
    {
        var context = new TypeNarrowingContext();
        context.Narrow("x", SemanticType.Int);

        using (context.EnterScope())
        {
            // Inner scope can see outer narrowing
            context.GetNarrowedType("x").Should().Be(SemanticType.Int);

            // Inner scope adds its own narrowing (different variable)
            context.Narrow("y", SemanticType.Str);
        }

        // x should still be narrowed in outer scope
        context.GetNarrowedType("x").Should().Be(SemanticType.Int);
        // y should not exist in outer scope
        context.GetNarrowedType("y").Should().BeNull();
    }

    [Fact]
    public void Shadowing_InnerScopeOverridesOuter()
    {
        var context = new TypeNarrowingContext();
        context.Narrow("x", SemanticType.Int);

        using (context.EnterScope())
        {
            // Shadow the outer narrowing
            context.Narrow("x", SemanticType.Str);

            // Inner scope sees the shadowed type
            context.GetNarrowedType("x").Should().Be(SemanticType.Str);
        }

        // After exiting, outer narrowing is restored
        context.GetNarrowedType("x").Should().Be(SemanticType.Int);
    }

    [Fact]
    public void NestedScopes_ProperlyUnwind()
    {
        var context = new TypeNarrowingContext();
        context.Narrow("level0", SemanticType.Int);

        using (context.EnterScope())
        {
            context.Narrow("level1", SemanticType.Str);

            using (context.EnterScope())
            {
                context.Narrow("level2", SemanticType.Bool);

                context.GetNarrowedType("level0").Should().Be(SemanticType.Int);
                context.GetNarrowedType("level1").Should().Be(SemanticType.Str);
                context.GetNarrowedType("level2").Should().Be(SemanticType.Bool);
            }

            context.GetNarrowedType("level0").Should().Be(SemanticType.Int);
            context.GetNarrowedType("level1").Should().Be(SemanticType.Str);
            context.GetNarrowedType("level2").Should().BeNull();
        }

        context.GetNarrowedType("level0").Should().Be(SemanticType.Int);
        context.GetNarrowedType("level1").Should().BeNull();
        context.GetNarrowedType("level2").Should().BeNull();
    }

    #endregion

    #region Clear Operations

    [Fact]
    public void ClearNarrowings_ClearsOnlyCurrentScope()
    {
        var context = new TypeNarrowingContext();
        context.Narrow("outer", SemanticType.Int);

        using (context.EnterScope())
        {
            context.Narrow("inner", SemanticType.Str);
            context.ClearNarrowings();

            // Inner narrowing should be cleared
            context.GetNarrowedType("inner").Should().BeNull();
            // Outer narrowing should still be visible (inherited from outer scope)
            context.GetNarrowedType("outer").Should().Be(SemanticType.Int);
        }
    }

    [Fact]
    public void ClearAllNarrowings_ResetsToCleanState()
    {
        var context = new TypeNarrowingContext();
        context.Narrow("x", SemanticType.Int);

        using (context.EnterScope())
        {
            context.Narrow("y", SemanticType.Str);
            context.ClearAllNarrowings();

            // All narrowings should be gone
            context.GetNarrowedType("x").Should().BeNull();
            context.GetNarrowedType("y").Should().BeNull();
            // Should be back to single root scope
            context.ScopeDepth.Should().Be(1);
        }

        // Note: The using statement's dispose is now a no-op since ClearAllNarrowings reset the stack
    }

    #endregion

    #region ApplyNarrowings

    [Fact]
    public void ApplyNarrowings_AddsMultipleNarrowings()
    {
        var context = new TypeNarrowingContext();
        var narrowings = new Dictionary<string, SemanticType>
        {
            ["x"] = SemanticType.Int,
            ["y"] = SemanticType.Str
        };

        context.ApplyNarrowings(narrowings);

        context.GetNarrowedType("x").Should().Be(SemanticType.Int);
        context.GetNarrowedType("y").Should().Be(SemanticType.Str);
    }

    [Fact]
    public void ApplyNarrowings_OverwritesExisting()
    {
        var context = new TypeNarrowingContext();
        context.Narrow("x", SemanticType.Int);

        var narrowings = new Dictionary<string, SemanticType>
        {
            ["x"] = SemanticType.Str
        };

        context.ApplyNarrowings(narrowings);

        context.GetNarrowedType("x").Should().Be(SemanticType.Str);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RootScope_CannotBePopped()
    {
        var context = new TypeNarrowingContext();

        // Try to exit the root scope (should be a no-op)
        using (context.EnterScope())
        {
            // In inner scope
        }

        // Back to root scope
        context.ScopeDepth.Should().Be(1);

        // Narrowings in root scope should persist
        context.Narrow("x", SemanticType.Int);
        context.GetNarrowedType("x").Should().Be(SemanticType.Int);
    }

    [Fact]
    public void Narrow_WithNullableType()
    {
        var context = new TypeNarrowingContext();
        var narrowedType = new NullableType { UnderlyingType = SemanticType.Int };

        context.Narrow("maybeNull", narrowedType);

        context.GetNarrowedType("maybeNull").Should().Be(narrowedType);
    }

    [Fact]
    public void Narrow_WithUserDefinedType()
    {
        var context = new TypeNarrowingContext();
        var typeSymbol = new TypeSymbol { Name = "Dog", Kind = SymbolKind.Type };
        var userType = new UserDefinedType { Name = "Dog", Symbol = typeSymbol };

        context.Narrow("animal", userType);

        var result = context.GetNarrowedType("animal");
        result.Should().BeOfType<UserDefinedType>();
        ((UserDefinedType)result!).Name.Should().Be("Dog");
    }

    [Fact]
    public void Narrow_WithSubscriptKey()
    {
        // Test that subscript expressions like arr[i] can be used as narrowing keys
        var context = new TypeNarrowingContext();

        context.Narrow("arr[0]", SemanticType.Int);

        context.GetNarrowedType("arr[0]").Should().Be(SemanticType.Int);
        context.GetNarrowedType("arr").Should().BeNull(); // Different key
    }

    [Fact]
    public void MultipleDispose_IsIdempotent()
    {
        var context = new TypeNarrowingContext();

        var scope = context.EnterScope();
        context.Narrow("x", SemanticType.Int);

        scope.Dispose();
        scope.Dispose(); // Should not throw

        context.ScopeDepth.Should().Be(1);
    }

    #endregion
}
