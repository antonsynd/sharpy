using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Helper for the <c>functools.partial</c> compatibility shim.
/// <para>
/// <c>functools.partial(f, fixed_args..., kw=val, ...)</c> is desugared to an
/// equivalent <c>_</c>-placeholder lambda. The shim performs no runtime work;
/// codegen emits a lambda that calls the target function directly.
/// </para>
/// <para>
/// The idiomatic Sharpy form is the <c>_</c> placeholder syntax (e.g., <c>add(5, _)</c>).
/// An SPY1010 info diagnostic encourages users to migrate to the placeholder form.
/// </para>
/// </summary>
internal static class FunctoolsPartialHelper
{
    /// <summary>
    /// The canonical module name registered for <c>functools</c> in Sharpy.Core.
    /// </summary>
    public const string FunctoolsModuleName = "functools";

    /// <summary>
    /// The function name within the <c>functools</c> module.
    /// </summary>
    public const string PartialMemberName = "partial";

    /// <summary>
    /// Determines whether a call expression syntactically matches <c>functools.partial(...)</c>
    /// against the current symbol table.
    /// </summary>
    /// <param name="call">The function call to inspect.</param>
    /// <param name="symbolTable">The symbol table for resolving the module identifier.</param>
    /// <returns>True if the call's callee resolves to <c>functools.partial</c>.</returns>
    public static bool IsFunctoolsPartialCall(FunctionCall call, SymbolTable? symbolTable)
    {
        if (symbolTable == null)
        {
            return false;
        }

        // Match: functools.partial(...) where 'functools' resolves to a ModuleSymbol
        // for the Sharpy.Core functools module. The local identifier (e.g., 'functools'
        // or an alias from `import functools as f`) is looked up; the module's canonical
        // name is what we check against.
        if (call.Function is not MemberAccess memberAccess)
        {
            return false;
        }

        if (memberAccess.Member != PartialMemberName)
        {
            return false;
        }

        if (memberAccess.Object is not Identifier moduleId)
        {
            return false;
        }

        var moduleSymbol = symbolTable.Lookup(moduleId.Name) as ModuleSymbol;
        if (moduleSymbol == null)
        {
            return false;
        }

        return IsFunctoolsModule(moduleSymbol);
    }

    /// <summary>
    /// Returns true when the given module symbol refers to the Sharpy.Core
    /// <c>functools</c> module.
    /// </summary>
    public static bool IsFunctoolsModule(ModuleSymbol moduleSymbol)
    {
        return string.Equals(moduleSymbol.Name, FunctoolsModuleName, System.StringComparison.Ordinal)
            || string.Equals(moduleSymbol.CanonicalModuleName, FunctoolsModuleName, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Computes the remaining (unfixed) parameters of a target function's <see cref="FunctionType"/>
    /// after fixing the given number of leading positional arguments. Keyword fixing is not
    /// represented here because <see cref="FunctionType"/> does not preserve parameter names —
    /// callers that know parameter names (FunctionSymbol path) should consume them before invoking
    /// this method.
    /// </summary>
    /// <param name="targetType">The target function's type.</param>
    /// <param name="fixedPositionalCount">Number of leading positional arguments fixed by partial.</param>
    /// <returns>A new FunctionType with the fixed parameters removed, or null if the fix count exceeds the target's arity.</returns>
    public static FunctionType? ComputeResultTypeFromFunctionType(FunctionType targetType, int fixedPositionalCount)
    {
        if (fixedPositionalCount < 0 || fixedPositionalCount > targetType.ParameterTypes.Count)
        {
            return null;
        }

        var remaining = new List<SemanticType>(targetType.ParameterTypes.Count - fixedPositionalCount);
        for (var i = fixedPositionalCount; i < targetType.ParameterTypes.Count; i++)
        {
            remaining.Add(targetType.ParameterTypes[i]);
        }

        var remainingOptional = System.Math.Max(0, targetType.OptionalParameterCount - System.Math.Max(0,
            fixedPositionalCount - (targetType.ParameterTypes.Count - targetType.OptionalParameterCount)));

        return new FunctionType
        {
            ParameterTypes = remaining,
            ReturnType = targetType.ReturnType,
            OptionalParameterCount = remainingOptional,
        };
    }
}
