using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Which parts of one <c>except</c> handler can ever run, given the handlers before it (#2363).
/// </summary>
/// <param name="IsUnreachable">Every exception type the handler names is already caught by an
/// earlier handler of the same <c>try</c> that has no <c>when</c> filter: python never runs it.</param>
/// <param name="CaughtBy">For an unreachable handler, the earlier type that covers its first
/// exception type (the warning names it); null otherwise.</param>
/// <param name="TypeReachable">One flag per exception type the handler names, in source order: the
/// elements of <c>except (A, B):</c>, or the one type of <c>except A:</c>. Empty for a bare
/// <c>except:</c>. A type is unreachable when an earlier handler, or an earlier element of the same
/// tuple, already catches it.</param>
internal sealed record ExceptHandlerVerdict(
    bool IsUnreachable,
    SemanticType? CaughtBy,
    ImmutableArray<bool> TypeReachable)
{
    /// <summary>Whether anything of this handler is left out of the emitted <c>catch</c> list.</summary>
    public bool PrunesAnything => IsUnreachable || TypeReachable.Any(reachable => !reachable);
}

/// <summary>
/// The ONE classifier for except-handler reachability (#2363). CPython tries a <c>try</c>'s handlers
/// in source order and runs the first whose type matches, so a handler (or one type of a tuple
/// handler) that an earlier handler already covers is dead: python accepts it and never runs it. C#
/// refuses the same <c>catch</c> list (CS0160), so the dead part is decided here, from the handler
/// list and the exception types the checker recorded for it, and never reaches Roslyn: the validator
/// warns (SPY0488) and the lowering pass carries the verdict to the emitter, which omits the dead
/// catches. Both read this one classifier, so the warning and the omission cannot disagree.
/// </summary>
/// <remarks>
/// <para><b>Coverage</b> is subclass-or-same over the recorded exception types, judged on their
/// symbols (<see cref="TypeHierarchyService"/>), i.e. on the EMITTED hierarchy — the one C# judges
/// CS0160 on and the one Core mirrors from python (#2124). A generic exception type is covered only
/// by the identical closed type. A type the checker recorded nothing for neither covers nor is
/// covered.</para>
/// <para>A handler with a <c>when</c> filter covers nothing (it can decline), but it can itself be
/// covered. A bare <c>except:</c> catches every exception; it is covered once an earlier unfiltered
/// handler catches <see cref="System.Exception"/>, the root every raisable type derives from (there is
/// no <c>BaseException</c>). A bare <c>except:</c> that is not last is refused by the parser
/// (SPY0151), as python refuses it. <c>except*</c> handlers are dispatched by one
/// <c>catch (AggregateException)</c> and are not classified here.</para>
/// </remarks>
internal static class ExceptHandlerReachability
{
    /// <summary>
    /// One verdict per handler of <paramref name="tryStmt"/>, aligned with
    /// <see cref="TryStatement.Handlers"/>.
    /// </summary>
    public static ImmutableArray<ExceptHandlerVerdict> Classify(TryStatement tryStmt, SemanticInfo? info)
    {
        var handlers = tryStmt.Handlers;
        var verdicts = ImmutableArray.CreateBuilder<ExceptHandlerVerdict>(handlers.Length);

        if (info == null || handlers.Any(h => h.IsExceptStar))
        {
            foreach (var handler in handlers)
                verdicts.Add(AllReachable(handler));
            return verdicts.MoveToImmutable();
        }

        // The types caught by earlier handlers with no filter, and whether one of them catches all.
        var caught = new List<SemanticType>();
        SemanticType? catchesAll = null;

        foreach (var handler in handlers)
        {
            if (handler.ExceptionType == null)
            {
                // Bare `except:` — it catches everything, so it is covered only by a catch-all.
                verdicts.Add(new ExceptHandlerVerdict(catchesAll != null, catchesAll, ImmutableArray<bool>.Empty));
                continue;
            }

            var elements = ElementsOf(handler.ExceptionType);
            var reachable = ImmutableArray.CreateBuilder<bool>(elements.Length);
            var withinHandler = new List<SemanticType>();
            var allCoveredByEarlierHandlers = true;
            SemanticType? firstCover = null;

            foreach (var element in elements)
            {
                var type = RecordedType(element, info);
                var cover = type == null ? null : catchesAll ?? FindCover(type, caught);
                if (cover == null)
                    allCoveredByEarlierHandlers = false;
                else
                    firstCover ??= cover;

                var dead = cover != null || (type != null && FindCover(type, withinHandler) != null);
                reachable.Add(!dead);
                if (type != null && !dead)
                    withinHandler.Add(type);
            }

            var isUnreachable = elements.Length > 0 && allCoveredByEarlierHandlers;
            verdicts.Add(new ExceptHandlerVerdict(
                isUnreachable, isUnreachable ? firstCover : null, reachable.MoveToImmutable()));

            if (handler.Filter == null && !isUnreachable)
            {
                caught.AddRange(withinHandler);
                catchesAll ??= withinHandler.FirstOrDefault(IsExceptionRoot);
            }
        }

        return verdicts.MoveToImmutable();
    }

    /// <summary>
    /// The exception types a handler names, in source order: the elements of a tuple spelling, or the
    /// annotation itself. The tuple test is the emitter's own (an unbound tuple expands to one catch
    /// per element), so the indices here are the indices it iterates.
    /// </summary>
    public static ImmutableArray<TypeAnnotation> ElementsOf(TypeAnnotation annotation)
        => annotation.Name == BuiltinNames.Tuple && annotation.TypeArguments.Length > 0
            ? annotation.TypeArguments
            : ImmutableArray.Create(annotation);

    private static ExceptHandlerVerdict AllReachable(ExceptHandler handler)
    {
        var count = handler.ExceptionType == null ? 0 : ElementsOf(handler.ExceptionType).Length;
        return new ExceptHandlerVerdict(false, null, Enumerable.Repeat(true, count).ToImmutableArray());
    }

    /// <summary>The closed type the checker classified for one handler type (#1235), or null.</summary>
    private static SemanticType? RecordedType(TypeAnnotation element, SemanticInfo info)
        => info.GetTypeTestLowering(element) is { Kind: TypeTestLoweringKind.ClosedType, TestType: var type }
           && type is UserDefinedType { Symbol: not null } or GenericType
            ? type
            : null;

    /// <summary>The first of <paramref name="catchers"/> that catches every <paramref name="type"/>, or null.</summary>
    private static SemanticType? FindCover(SemanticType type, List<SemanticType> catchers)
    {
        foreach (var catcher in catchers)
        {
            if (Covers(catcher, type))
                return catcher;
        }
        return null;
    }

    /// <summary>
    /// Whether a handler for <paramref name="catcher"/> catches every <paramref name="type"/>: the
    /// same type or a subclass of it, by symbol identity and the symbol base chain.
    /// </summary>
    private static bool Covers(SemanticType catcher, SemanticType type)
    {
        if (catcher is UserDefinedType { Symbol: { } catcherSymbol } && type is UserDefinedType { Symbol: { } typeSymbol })
        {
            return TypeHierarchyService.IsSameType(typeSymbol, catcherSymbol)
                || TypeHierarchyService.InheritsFrom(typeSymbol, catcherSymbol);
        }
        return catcher is GenericType && catcher.Equals(type);
    }

    /// <summary>The root every raisable type derives from: the CLR <see cref="System.Exception"/>.</summary>
    private static bool IsExceptionRoot(SemanticType type)
        => type is UserDefinedType { Symbol.ClrType: { } clrType } && clrType == typeof(System.Exception);
}
