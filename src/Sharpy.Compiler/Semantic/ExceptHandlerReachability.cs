using System.Collections.Immutable;
using Sharpy.Compiler.Discovery;
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
/// <param name="ElementTypes">The recorded type of each element (null where the checker recorded
/// none), aligned with <paramref name="TypeReachable"/>; empty for a bare <c>except:</c>.</param>
/// <param name="PythonLiveTypes">The handler's types that only <c>except Exception:</c> covers and that
/// CPython roots at <c>BaseException</c> (<c>SystemExit</c>): CPython would run the handler for them,
/// Sharpy cannot (no <c>BaseException</c>). Empty when there are none.</param>
/// <param name="BareStillRunsInPython">A bare <c>except:</c> covered by <c>except Exception:</c> while no
/// earlier handler names a <c>BaseException</c>-rooted type: CPython would still run it for
/// <c>SystemExit</c>.</param>
/// <param name="PythonLiveElement">The index of the first of <paramref name="PythonLiveTypes"/> among the
/// handler's elements (the diagnostic is reported on it); -1 when there is none.</param>
/// <param name="PythonLiveCover">The earlier handler's type that covers that element: the
/// <see cref="System.Exception"/> root, which is not necessarily the cover of the first element.</param>
internal sealed record ExceptHandlerVerdict(
    bool IsUnreachable,
    SemanticType? CaughtBy,
    ImmutableArray<bool> TypeReachable,
    ImmutableArray<SemanticType?> ElementTypes,
    ImmutableArray<SemanticType> PythonLiveTypes,
    bool BareStillRunsInPython = false,
    int PythonLiveElement = -1,
    SemanticType? PythonLiveCover = null)
{
    /// <summary>Every type the handler names is BaseException-rooted in CPython: moving the whole
    /// handler before <c>except Exception:</c> changes nothing else.</summary>
    public bool NamesOnlyPythonLiveTypes => !PythonLiveTypes.IsEmpty && PythonLiveTypes.Length == ElementTypes.Length;

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
/// symbols, i.e. on the EMITTED hierarchy — the one C# judges CS0160 on and the one Core mirrors
/// from python (#2124). A closed generic type is judged on its definition against a non-generic
/// catcher (<c>TypeChecker.IsExceptionSubtype</c>'s rule), and against a generic catcher on the
/// instantiated base chain (<see cref="GenericInstantiationWalker.EnumerateBaseChain"/>), so
/// <c>except Box[int]:</c> covers <c>except SubBox[int]:</c> but not <c>except Box[str]:</c>. A type
/// the checker recorded nothing for neither covers nor is covered.</para>
/// <para>The one input besides the handler list is the <c>TypeTestType</c> the checker recorded
/// for each handler type; <c>SemanticInfo.MergeFrom</c> copies it verbatim (a conflicting value
/// throws), so the per-file validator and the post-merge lowering read the same facts.</para>
/// <para>Sharpy has no <c>BaseException</c> (exception_handling.md): <c>except Exception:</c> catches
/// <c>SystemExit</c> and everything a bare <c>except:</c> would, so those handlers after it are
/// unreachable in Sharpy though CPython runs them: <see cref="ExceptHandlerVerdict.PythonLiveTypes"/> and
/// <see cref="ExceptHandlerVerdict.BareStillRunsInPython"/> mark them (deviation
/// <c>except-exception-catches-systemexit</c>). A more specific earlier catcher is preferred over the
/// root, so <c>except SystemExit:</c> / <c>except Exception:</c> / <c>except SystemExit:</c> is a plain
/// dead handler, as in CPython.</para>
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
                // Bare `except:` — it catches everything, so it is covered only by a catch-all. CPython's
                // still catches SystemExit there unless an earlier handler names a BaseException-rooted type.
                verdicts.Add(new ExceptHandlerVerdict(catchesAll != null, catchesAll, ImmutableArray<bool>.Empty,
                    ImmutableArray<SemanticType?>.Empty, ImmutableArray<SemanticType>.Empty,
                    BareStillRunsInPython: catchesAll != null && !caught.Any(IsPythonBaseExceptionRoot)));
                continue;
            }

            var elements = ElementsOf(handler.ExceptionType);
            var reachable = ImmutableArray.CreateBuilder<bool>(elements.Length);
            var elementTypes = ImmutableArray.CreateBuilder<SemanticType?>(elements.Length);
            var pythonLive = ImmutableArray.CreateBuilder<SemanticType>();
            var withinHandler = new List<SemanticType>();
            var allCoveredByEarlierHandlers = true;
            SemanticType? firstCover = null;
            var liveElement = -1;
            SemanticType? liveCover = null;

            foreach (var element in elements)
            {
                var type = RecordedType(element, info);
                elementTypes.Add(type);
                var cover = type == null ? null : FindCover(type, caught);
                if (cover == null)
                    allCoveredByEarlierHandlers = false;
                else
                {
                    firstCover ??= cover;
                    if (IsExceptionRoot(cover) && IsPythonBaseExceptionType(type!))
                    {
                        if (liveElement < 0)
                        {
                            liveElement = elementTypes.Count - 1;
                            liveCover = cover;
                        }
                        pythonLive.Add(type!);
                    }
                }

                var dead = cover != null || (type != null && FindCover(type, withinHandler) != null);
                reachable.Add(!dead);
                if (type != null && !dead)
                    withinHandler.Add(type);
            }

            var isUnreachable = elements.Length > 0 && allCoveredByEarlierHandlers;
            verdicts.Add(new ExceptHandlerVerdict(
                isUnreachable, isUnreachable ? firstCover : null, reachable.MoveToImmutable(),
                elementTypes.MoveToImmutable(), pythonLive.ToImmutable(),
                PythonLiveElement: liveElement, PythonLiveCover: liveCover));

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
        return new ExceptHandlerVerdict(false, null, Enumerable.Repeat(true, count).ToImmutableArray(),
            Enumerable.Repeat<SemanticType?>(null, count).ToImmutableArray(), ImmutableArray<SemanticType>.Empty);
    }

    /// <summary>The closed type the checker classified for one handler type (#1235), or null.</summary>
    private static SemanticType? RecordedType(TypeAnnotation element, SemanticInfo info)
        => info.GetTypeTestType(element) is { } type and (UserDefinedType { Symbol: not null } or GenericType)
            ? type
            : null;

    /// <summary>
    /// The earlier catcher that catches every <paramref name="type"/>, or null: a specific one before the
    /// <see cref="System.Exception"/> root, so the answer names the handler CPython would run.
    /// </summary>
    private static SemanticType? FindCover(SemanticType type, List<SemanticType> catchers)
    {
        foreach (var catcher in catchers)
        {
            if (!IsExceptionRoot(catcher) && Covers(catcher, type))
                return catcher;
        }
        foreach (var catcher in catchers)
        {
            if (IsExceptionRoot(catcher) && Covers(catcher, type))
                return catcher;
        }
        return null;
    }

    /// <summary>
    /// Whether a handler for <paramref name="catcher"/> catches every <paramref name="type"/>: the
    /// same type or a subclass of it. A non-generic catcher is judged on the type's definition by
    /// <see cref="TypeChecker.IsExceptionSubtype"/>, the checker's own derivation rule; a generic
    /// catcher is matched against the type itself and its instantiated base chain.
    /// </summary>
    private static bool Covers(SemanticType catcher, SemanticType type)
    {
        if (catcher is UserDefinedType { Symbol: { } catcherSymbol })
            return TypeChecker.IsExceptionSubtype(type, catcherSymbol);

        if (catcher is not GenericType { GenericDefinition: { } catcherDefinition } genericCatcher)
            return false;
        if (catcher.Equals(type))
            return true;

        var (definition, arguments) = type switch
        {
            GenericType { GenericDefinition: { } d } g => (d, (IReadOnlyList<SemanticType>)g.TypeArguments),
            UserDefinedType { Symbol: { } s } => (s, Array.Empty<SemanticType>()),
            _ => (null, Array.Empty<SemanticType>()),
        };
        if (definition == null)
            return false;

        foreach (var ancestor in GenericInstantiationWalker.EnumerateBaseChain(definition, arguments))
        {
            if (TypeHierarchyService.IsSameType(ancestor.Definition, catcherDefinition)
                && ancestor.TypeArguments.SequenceEqual(genericCatcher.TypeArguments))
                return true;
        }
        return false;
    }

    /// <summary>Whether CPython roots the type at <c>BaseException</c>: it, or a base, carries Core's marker.</summary>
    /// <summary>A catcher for a marked type itself (<c>except SystemExit:</c>), which catches every instance of it.</summary>
    private static bool IsPythonBaseExceptionRoot(SemanticType catcher)
        => catcher is UserDefinedType { Symbol.ClrType: { } clrType } && ClrTypeHelper.IsPythonBaseExceptionType(clrType);

    private static bool IsPythonBaseExceptionType(SemanticType type)
    {
        var symbol = type switch
        {
            UserDefinedType { Symbol: { } s } => s,
            GenericType { GenericDefinition: { } d } => d,
            _ => null,
        };
        var visited = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);
        for (var current = symbol; current != null && visited.Add(current); current = current.BaseType)
        {
            if (current.ClrType is { } clrType && ClrTypeHelper.IsPythonBaseExceptionType(clrType))
                return true;
        }
        return false;
    }

    /// <summary>The root every raisable type derives from: the CLR <see cref="System.Exception"/>.</summary>
    private static bool IsExceptionRoot(SemanticType type)
        => type is UserDefinedType { Symbol.ClrType: { } clrType } && clrType == typeof(System.Exception);
}
