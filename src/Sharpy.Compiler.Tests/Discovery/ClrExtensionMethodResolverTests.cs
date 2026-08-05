extern alias SharpyRT;
using System;
using System.Collections.Generic;
using System.Linq;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Semantic;
using Xunit;

using Shape = Sharpy.Compiler.Discovery.ClrExtensionMethodResolver.ExtensionArgumentShape;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// #1206: the no-type-args entry point closes a <c>System.Linq.Enumerable</c> candidate as far as the
/// RECEIVER determines it and reports the rest open, so the semantic side can infer those from the
/// arguments and hand the answer back.
///
/// <para>
/// The cases below are the measured partition of the acceptance surface (Phase 1): of the 183 generic
/// extension methods on <c>Enumerable</c>, 65 close from the receiver and its non-lambda arguments
/// alone, 112 need one generation of lambda returns, and exactly six need two — <c>SelectMany</c> (2
/// overloads), <c>GroupBy</c> (3) and <c>AggregateBy</c> (1). Nothing needs three. Note that
/// <c>Join</c>, <c>GroupJoin</c> and 3-argument <c>Zip</c> are TWO-stage, not three: their extra
/// sequence type parameter comes from a non-lambda argument, which lands in the same stage as the
/// receiver.
/// </para>
///
/// <para>
/// Precedence is deliberately NOT this class's job — the collision guards at the bottom pin that this
/// entry point closes <c>reverse</c>/<c>append</c>/<c>count</c>/<c>contains</c> just as readily as
/// <c>select</c>, so it is provably the caller's gate, not the resolver, that keeps an instance member
/// winning (#1206 D3a).
/// </para>
/// </summary>
public class ClrExtensionMethodResolverTests
{
    private static readonly Type IntList = typeof(System.Collections.Generic.List<int>);
    private static readonly Type StrList = typeof(System.Collections.Generic.List<string>);

    private static ClrExtensionMethodResolver.PartialResolution? Resolve(
        string member, params Shape[] shapes)
        => ClrExtensionMethodResolver.TryResolveFromReceiver(IntList, member, shapes);

    private static string Render(Type t)
    {
        if (t.IsGenericParameter) return t.Name;
        if (t.IsArray) return Render(t.GetElementType()!) + "[]";
        if (!t.IsGenericType) return t.Name;
        return global::Sharpy.Compiler.Shared.ClrNameHelper.StripArity(t.Name)
            + "<" + string.Join(",", t.GetGenericArguments().Select(Render)) + ">";
    }

    // ---------------------------------------------------------------
    // Receiver-only closure — the flagship two-stage shapes
    // ---------------------------------------------------------------

    [Fact]
    public void Select_BindsTSourceFromReceiver_AndLeavesTResultOpen()
    {
        var partial = Resolve("select", Shape.Lambda(1));

        Assert.NotNull(partial);
        Assert.Equal("Select", partial!.ClrMethodName);
        // TSource came from List<int>; TResult is what the lambda's RETURN will supply.
        Assert.Equal(new[] { "TResult" }, partial.OpenTypeParameterNames);
        // The formal the lambda is checked against: int in, still-open TResult out.
        Assert.Equal("Func<Int32,TResult>", Render(Assert.Single(partial.ParameterTypes)));
        Assert.Equal("IEnumerable<TResult>", Render(partial.ReturnType));
    }

    [Fact]
    public void Select_OpenParameterSurvivesAsTypeParameterType_NotUnknownOrObject()
    {
        // D5: the partially-closed formal handed to CheckLambda must keep the open parameter as a
        // TypeParameterType so SubstituteExpectedLambdaType can fill it. Collapsing it to Unknown or
        // object would defeat both the deferral pass and CheckLambda's ContainsTypeParameterType guard.
        var partial = Resolve("select", Shape.Lambda(1));
        var mapped = new ClrTypeBridge().MapClrTypeToSemanticType(Assert.Single(partial!.ParameterTypes));

        var function = Assert.IsType<FunctionType>(mapped);
        Assert.Equal(SemanticType.Int, Assert.Single(function.ParameterTypes));
        Assert.Equal("TResult", Assert.IsType<TypeParameterType>(function.ReturnType).Name);
    }

    [Fact]
    public void Where_ClosesEntirelyFromTheReceiver()
    {
        var partial = Resolve("where", Shape.Lambda(1));

        Assert.NotNull(partial);
        Assert.Equal("Where", partial!.ClrMethodName);
        Assert.Empty(partial.OpenTypeParameterNames);
        Assert.Equal("Func<Int32,Boolean>", Render(Assert.Single(partial.ParameterTypes)));
        Assert.Equal("IEnumerable<Int32>", Render(partial.ReturnType));
    }

    [Theory]
    // The one-stage rows: everything the receiver alone determines.
    [InlineData("first", "IEnumerable<Int32>", "Int32")]
    [InlineData("last", "IEnumerable<Int32>", "Int32")]
    [InlineData("single", "IEnumerable<Int32>", "Int32")]
    [InlineData("count", "IEnumerable<Int32>", "Int32")]
    [InlineData("long_count", "IEnumerable<Int32>", "Int64")]
    [InlineData("distinct", "IEnumerable<Int32>", "IEnumerable<Int32>")]
    [InlineData("reverse", "IEnumerable<Int32>", "IEnumerable<Int32>")]
    [InlineData("to_array", "IEnumerable<Int32>", "Int32[]")]
    [InlineData("to_list", "IEnumerable<Int32>", "List<Int32>")]
    public void ZeroArgumentShapes_CloseFromTheReceiverAlone(string member, string _, string returnType)
    {
        var partial = Resolve(member);

        Assert.NotNull(partial);
        Assert.Empty(partial!.OpenTypeParameterNames);
        Assert.Empty(partial.ParameterTypes);
        Assert.Equal(returnType, Render(partial.ReturnType));
    }

    // ---------------------------------------------------------------
    // The six three-stage shapes measured in Phase 1
    // ---------------------------------------------------------------

    [Fact]
    public void SelectMany_ThreeStage_LeavesBothLambdaDeterminedParametersOpen()
    {
        // TSource from the receiver; TCollection recovered STRUCTURALLY from lambda 1's
        // IEnumerable<TCollection> return; only then can lambda 2 be typed to yield TResult.
        var partial = Resolve("select_many", Shape.Lambda(1), Shape.Lambda(2));

        Assert.NotNull(partial);
        Assert.Equal("SelectMany", partial!.ClrMethodName);
        Assert.Equal(new[] { "TCollection", "TResult" }, partial.OpenTypeParameterNames);
        Assert.Equal("Func<Int32,IEnumerable<TCollection>>", Render(partial.ParameterTypes[0]));
        Assert.Equal("Func<Int32,TCollection,TResult>", Render(partial.ParameterTypes[1]));
    }

    [Fact]
    public void GroupBy_ThreeStage_ResultSelectorDependsOnTwoEarlierLambdaReturns()
    {
        var partial = Resolve("group_by", Shape.Lambda(1), Shape.Lambda(1), Shape.Lambda(2));

        Assert.NotNull(partial);
        Assert.Equal("GroupBy", partial!.ClrMethodName);
        Assert.Equal(new[] { "TKey", "TElement", "TResult" }, partial.OpenTypeParameterNames);
        Assert.Equal("Func<Int32,TKey>", Render(partial.ParameterTypes[0]));
        Assert.Equal("Func<Int32,TElement>", Render(partial.ParameterTypes[1]));
        // Generation 2: both of this lambda's parameters are bound by the two above.
        Assert.Equal("Func<TKey,IEnumerable<TElement>,TResult>", Render(partial.ParameterTypes[2]));
    }

    [Fact]
    public void ThreeStageShapes_HaveDependencyOrderEqualToSourceOrder()
    {
        // Why the substitution fold-back needs no reordering: in every one of the six measured
        // three-stage shapes, a lambda whose parameters depend on an earlier lambda's return is
        // written AFTER it. Folding each checked lambda back into the substitutions while walking
        // positions in source order is therefore sufficient.
        var selectMany = Resolve("select_many", Shape.Lambda(1), Shape.Lambda(2))!;
        Assert.Equal("Func<Int32,IEnumerable<TCollection>>", Render(selectMany.ParameterTypes[0]));
        Assert.Contains("TCollection", Render(selectMany.ParameterTypes[1]));

        var groupBy = Resolve("group_by", Shape.Lambda(1), Shape.Lambda(2))!;
        Assert.Equal("Func<Int32,TKey>", Render(groupBy.ParameterTypes[0]));
        Assert.Equal("Func<TKey,IEnumerable<Int32>,TResult>", Render(groupBy.ParameterTypes[1]));
    }

    [Fact]
    public void Join_IsTwoStage_ItsExtraSequenceParameterComesFromAnArgument()
    {
        // The plan called Join a three-round shape. It is not: TInner sits in a NON-LAMBDA argument,
        // so it lands in the same stage as the receiver and all three lambdas become checkable at once.
        var partial = Resolve("join", Shape.Value, Shape.Lambda(1), Shape.Lambda(1), Shape.Lambda(2));

        Assert.NotNull(partial);
        Assert.Equal("Join", partial!.ClrMethodName);
        Assert.Equal(new[] { "TInner", "TKey", "TResult" }, partial.OpenTypeParameterNames);
        Assert.Equal("IEnumerable<TInner>", Render(partial.ParameterTypes[0]));
        Assert.Equal("Func<TInner,TKey>", Render(partial.ParameterTypes[2]));
    }

    [Fact]
    public void ThreeArgumentZip_IsTwoStage_Likewise()
    {
        var partial = Resolve("zip", Shape.Value, Shape.Lambda(2));

        Assert.NotNull(partial);
        Assert.Equal("Zip", partial!.ClrMethodName);
        Assert.Equal(new[] { "TSecond", "TResult" }, partial.OpenTypeParameterNames);
        Assert.Equal("Func<Int32,TSecond,TResult>", Render(partial.ParameterTypes[1]));
    }

    // ---------------------------------------------------------------
    // Overload discrimination and the cases that must decline
    // ---------------------------------------------------------------

    [Fact]
    public void WrittenLambdaArity_PicksBetweenSameArityOverloads()
    {
        // Select ships a plain and an index-taking selector at identical arity. The written lambda's
        // parameter count is the only thing that separates them, and it must, because they hand the
        // lambda different expected types.
        Assert.Equal("Func<Int32,TResult>", Render(Resolve("select", Shape.Lambda(1))!.ParameterTypes[0]));
        Assert.Equal("Func<Int32,Int32,TResult>", Render(Resolve("select", Shape.Lambda(2))!.ParameterTypes[0]));
    }

    [Fact]
    public void NonLambdaArgumentAgainstAmbiguousOverloads_Declines()
    {
        // A variable holding a selector fits both Select overloads and they differ in the formal, so
        // there is no single answer. Declining leaves the call exactly as permissive as it is today (D2).
        Assert.Null(Resolve("select", Shape.Value));
    }

    [Fact]
    public void OverloadsThatDisagreeOnTheOpenSet_Decline()
    {
        // max(lambda) is eleven candidates: one leaves TResult open, ten close it to a specific numeric
        // type. That is a genuine disagreement, not two overloads that became indistinguishable.
        Assert.Null(Resolve("max", Shape.Lambda(1)));
    }

    [Fact]
    public void WrongArgumentCount_Declines()
    {
        // Enumerable.Index takes no arguments beyond the receiver.
        Assert.Null(Resolve("index", Shape.Value));
        Assert.NotNull(Resolve("index"));
    }

    [Fact]
    public void NameNotOnTheSurface_Declines()
    {
        Assert.Null(Resolve("no_such_extension"));
        Assert.Null(Resolve("add", Shape.Value));
    }

    [Fact]
    public void ReceiverThatIsNotASequence_Declines()
    {
        // Gates 1-4 do not prove the receiver is a sequence; TryBindThisParameter does.
        Assert.Null(ClrExtensionMethodResolver.TryResolveFromReceiver(
            typeof(int), "select", new[] { Shape.Lambda(1) }));
        Assert.Null(ClrExtensionMethodResolver.TryResolveFromReceiver(
            typeof(System.Text.StringBuilder), "select", new[] { Shape.Lambda(1) }));
    }

    [Fact]
    public void CastAndOfType_NeverClose_TheirResultIsDeterminedByNothing()
    {
        // The two methods on the surface whose type parameter is neither receiver-, argument- nor
        // lambda-determined: their `this` parameter is the non-generic IEnumerable. D2 applies.
        var cast = Resolve("cast");
        Assert.NotNull(cast);
        Assert.Equal(new[] { "TResult" }, cast!.OpenTypeParameterNames);
        Assert.Null(ClrExtensionMethodResolver.TryCompleteFromInferredTypeArguments(
            cast, new Dictionary<string, Type>()));
    }

    // ---------------------------------------------------------------
    // Array `this` parameters (.NET 10 added Enumerable.Reverse<TSource>(this TSource[]))
    // ---------------------------------------------------------------

    [Fact]
    public void ArrayThisParameter_DoesNotBindAgainstANonArrayReceiver()
    {
        // Both Reverse overloads face a List<int>. The array one must DECLINE rather than succeed
        // having bound nothing — otherwise TSource stays open, the two candidates disagree, and
        // `lst.reverse()` resolves to nothing at all.
        var partial = Resolve("reverse");

        Assert.NotNull(partial);
        Assert.Empty(partial!.OpenTypeParameterNames);
        Assert.Equal("IEnumerable<Int32>", Render(partial.ReturnType));
    }

    [Fact]
    public void ArrayThisParameter_BindsAgainstAnArrayReceiver_AndAgreesWithTheSequenceOverload()
    {
        // An int[] reaches both overloads, they close identically, and agreeing overloads are not
        // ambiguity.
        var partial = ClrExtensionMethodResolver.TryResolveFromReceiver(
            typeof(int[]), "reverse", Array.Empty<Shape>());

        Assert.NotNull(partial);
        Assert.Equal("Reverse", partial!.ClrMethodName);
        Assert.Empty(partial.OpenTypeParameterNames);
        Assert.Equal("IEnumerable<Int32>", Render(partial.ReturnType));
    }

    [Fact]
    public void ArrayThisParameter_ExplicitPath_NoLongerResolvesAgainstAWrongElementType()
    {
        // Before the array arm, `lst.reverse[str]()` on a List<int> resolved to Reverse<string> via the
        // array overload (the sequence overload correctly refused, having bound TSource = int). An
        // un-computable vector must stay un-computable.
        Assert.Null(ClrExtensionMethodResolver.TryResolveWithExplicitTypeArguments(
            IntList, "reverse", new[] { typeof(string) }));
        Assert.NotNull(ClrExtensionMethodResolver.TryResolveWithExplicitTypeArguments(
            IntList, "reverse", new[] { typeof(int) }));
    }

    // ---------------------------------------------------------------
    // Completion
    // ---------------------------------------------------------------

    [Fact]
    public void Completion_ClosesTheVectorFromTheInferredOpenParameters()
    {
        var partial = Resolve("select", Shape.Lambda(1))!;

        var resolution = ClrExtensionMethodResolver.TryCompleteFromInferredTypeArguments(
            partial, new Dictionary<string, Type> { ["TResult"] = typeof(string) });

        Assert.NotNull(resolution);
        Assert.Equal("Select", resolution!.ClrMethodName);
        Assert.Equal(new[] { typeof(int), typeof(string) }, resolution.TypeArguments);
        Assert.Equal(typeof(IEnumerable<string>), resolution.ClosedMethod.ReturnType);
    }

    [Fact]
    public void Completion_WithNothingLeftOpen_NeedsNoInference()
    {
        var partial = Resolve("where", Shape.Lambda(1))!;

        var resolution = ClrExtensionMethodResolver.TryCompleteFromInferredTypeArguments(
            partial, new Dictionary<string, Type>());

        Assert.NotNull(resolution);
        Assert.Equal(typeof(IEnumerable<int>), resolution!.ClosedMethod.ReturnType);
    }

    [Fact]
    public void Completion_DeclinesWhenAnOpenParameterIsMissingOrStillOpen()
    {
        var partial = Resolve("select", Shape.Lambda(1))!;

        Assert.Null(ClrExtensionMethodResolver.TryCompleteFromInferredTypeArguments(
            partial, new Dictionary<string, Type>()));
        Assert.Null(ClrExtensionMethodResolver.TryCompleteFromInferredTypeArguments(
            partial, new Dictionary<string, Type> { ["TResult"] = typeof(List<>).GetGenericArguments()[0] }));
    }

    [Fact]
    public void Completion_AgreesWithTheExplicitPathOnTheSameCall()
    {
        // lst.select[str](f) and lst.select(f) must produce the same vector and the same closed method,
        // or the two spellings would emit differently (#1195 must not regress).
        var explicitly = ClrExtensionMethodResolver.TryResolveWithExplicitTypeArguments(
            IntList, "select", new[] { typeof(string) });
        var staged = ClrExtensionMethodResolver.TryCompleteFromInferredTypeArguments(
            Resolve("select", Shape.Lambda(1))!, new Dictionary<string, Type> { ["TResult"] = typeof(string) });

        Assert.NotNull(explicitly);
        Assert.NotNull(staged);
        Assert.Equal(explicitly!.ClrMethodName, staged!.ClrMethodName);
        Assert.Equal(explicitly.TypeArguments, staged.TypeArguments);
        Assert.Equal(explicitly.ClosedMethod, staged.ClosedMethod);
    }

    [Fact]
    public void ReceiverElementTypeFlowsThrough_NotJustInt()
    {
        var partial = ClrExtensionMethodResolver.TryResolveFromReceiver(
            StrList, "select", new[] { Shape.Lambda(1) });

        Assert.Equal("Func<String,TResult>", Render(Assert.Single(partial!.ParameterTypes)));
    }

    [Fact]
    public void SharpyCollectionReceiversBindTheSameWay()
    {
        // Sharpy.List<T> : IList<T>, so TryBindThisParameter reaches IEnumerable<T> from either
        // receiver kind — which is exactly why the caller's precedence gate has to exist.
        var partial = ClrExtensionMethodResolver.TryResolveFromReceiver(
            typeof(SharpyRT::Sharpy.List<int>), "select", new[] { Shape.Lambda(1) });

        Assert.Equal("Func<Int32,TResult>", Render(Assert.Single(partial!.ParameterTypes)));
    }

    // ---------------------------------------------------------------
    // Precedence is the CALLER's job — these guards say so out loud
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("reverse", 0)]
    [InlineData("append", 1)]
    [InlineData("count", 1)]
    [InlineData("contains", 1)]
    [InlineData("index", 0)]
    [InlineData("to_list", 0)]
    [InlineData("union", 1)]
    public void CollisionNames_CloseHereToo_SoThePrecedenceGateIsWhatProtectsThem(string member, int argCount)
    {
        // Every one of these names is BOTH an Enumerable extension and a real instance member on some
        // receiver (measured: Sharpy list/dict/set/str/bytes, CLR List/Dictionary/HashSet). This entry
        // point closes them without hesitation. Nothing here is wrong — an instance member beats an
        // extension method, and proving that is done at the call site, before this is ever reached
        // (#1206 D3a). If this test ever starts failing because the resolver "declines" a collision,
        // the protection has silently moved to the wrong layer.
        var shapes = Enumerable.Repeat(Shape.Value, argCount).ToArray();

        Assert.NotNull(ClrExtensionMethodResolver.TryResolveFromReceiver(IntList, member, shapes));
    }
}
