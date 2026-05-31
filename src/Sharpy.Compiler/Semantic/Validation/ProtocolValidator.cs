using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates protocol usage in Sharpy code:
/// - Iteration protocols (__iter__ for 'for' loops)
/// - Membership protocols (__contains__ for 'in' operator)
/// - Indexing protocols (__getitem__/__setitem__ for subscript access)
/// - Len protocol (__len__ for len() calls)
///
/// Post-pass validation of protocol usage. TypeInferenceService handles
/// type inference during type-checking; this validator catches missing
/// protocol implementations after types are resolved.
/// </summary>
internal class ProtocolValidator : ValidatingAstWalker
{
    public override string Name => "ProtocolValidator";
    public override int Order => 500; // After access validation (450)

    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _logger = context.Logger;
        _logger.LogDebug("Starting protocol validation");
        base.Validate(module, context);
    }

    public override void VisitForStatement(ForStatement node)
    {
        ValidateIteration(node);
        base.VisitForStatement(node);
    }

    public override void VisitForClause(ForClause node)
    {
        ValidateIteratorExpression(node.Iterator);
        base.VisitForClause(node);
    }

    public override void VisitIndexAccess(IndexAccess node)
    {
        ValidateIndexAccess(node);
        base.VisitIndexAccess(node);
    }

    public override void VisitBinaryOp(BinaryOp node)
    {
        if (node.Operator is BinaryOperator.In or BinaryOperator.NotIn)
        {
            ValidateMembership(node);
        }
        base.VisitBinaryOp(node);
    }

    public override void VisitFunctionCall(FunctionCall node)
    {
        ValidateFunctionCall(node);
        base.VisitFunctionCall(node);
    }

    private void ValidateIteration(ForStatement forStmt)
    {
        var iterableType = Context.SemanticInfo.GetExpressionType(forStmt.Iterator);
        if (iterableType == null || iterableType is UnknownType)
            return;

        // Async for loops use IAsyncEnumerable<T> protocol, not __iter__.
        // Skip the __iter__ check and trust the C# compiler to validate.
        if (!forStmt.IsAsync && !HasProtocol(iterableType, DunderNames.Iter))
        {
            AddError(
                $"Type '{iterableType.GetDisplayName()}' is not iterable " +
                "(missing '__iter__' method).",
                forStmt.Iterator.LineStart, forStmt.Iterator.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: forStmt.Iterator.Span);
        }
    }

    private void ValidateIteratorExpression(Expression iterator)
    {
        var iterableType = Context.SemanticInfo.GetExpressionType(iterator);
        if (iterableType == null || iterableType is UnknownType)
            return;

        if (!HasProtocol(iterableType, DunderNames.Iter))
        {
            AddError(
                $"Type '{iterableType.GetDisplayName()}' is not iterable " +
                "(missing '__iter__' method).",
                iterator.LineStart, iterator.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: iterator.Span);
        }
    }

    private void ValidateIndexAccess(IndexAccess indexAccess)
    {
        var containerType = Context.SemanticInfo.GetExpressionType(indexAccess.Object);
        if (containerType == null || containerType is UnknownType)
            return;

        if (!HasProtocol(containerType, DunderNames.GetItem))
        {
            AddError(
                $"Type '{containerType.GetDisplayName()}' does not support indexing " +
                "(missing '__getitem__' method).",
                indexAccess.LineStart, indexAccess.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: indexAccess.Span);
        }
    }

    private void ValidateMembership(BinaryOp binOp)
    {
        var containerType = Context.SemanticInfo.GetExpressionType(binOp.Right);
        if (containerType == null || containerType is UnknownType)
            return;

        if (!HasProtocol(containerType, DunderNames.Contains))
        {
            AddError(
                $"Type '{containerType.GetDisplayName()}' does not support membership testing " +
                "(missing '__contains__' method).",
                binOp.LineStart, binOp.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: binOp.Span);
        }
    }

    private void ValidateFunctionCall(FunctionCall call)
    {
        // Check for len() calls
        if (call.Function is Identifier id && id.Name == "len" && call.Arguments.Length == 1)
        {
            var argType = Context.SemanticInfo.GetExpressionType(call.Arguments[0]);
            if (argType == null || argType is UnknownType)
                return;

            if (!HasProtocol(argType, DunderNames.Len))
            {
                AddError(
                    $"Type '{argType.GetDisplayName()}' does not support len() " +
                    "(missing '__len__' method). Consider implementing ISized interface.",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                    span: call.Span);
            }
        }
    }

    /// <summary>
    /// Checks if a type has a specific protocol dunder method.
    /// This duplicates logic from ProtocolValidator for post-pass validation.
    /// </summary>
    private bool HasProtocol(SemanticType type, string dunderName)
    {
        // Check Sharpy built-in types first
        if (type == SemanticType.Str)
        {
            return dunderName is DunderNames.Len or DunderNames.Iter or DunderNames.Contains or DunderNames.GetItem;
        }

        // Check TupleType
        if (type is TupleType)
        {
            return dunderName is DunderNames.Len or DunderNames.Iter or DunderNames.GetItem;
        }

        // Check generic container types — use TypeSymbol metadata (populated by discovery)
        if (type is GenericType generic)
        {
            // Arrays support __len__, __iter__, __getitem__, __setitem__, __contains__
            if (generic.Name == BuiltinNames.Array)
            {
                return dunderName is DunderNames.Len or DunderNames.Iter
                    or DunderNames.GetItem or DunderNames.SetItem or DunderNames.Contains;
            }

            // For defaultdict, check dict protocols since it inherits from Dict
            var lookupName = string.Equals(generic.Name, BuiltinNames.DefaultDict, StringComparison.OrdinalIgnoreCase)
                ? BuiltinNames.Dict : generic.Name;
            var typeSymbol = Context.Builtins.GetType(lookupName);
            if (typeSymbol != null)
                return typeSymbol.ProtocolMethods.ContainsKey(dunderName);

            // Fallback: check SymbolTable for discovery-loaded generic types (e.g., Counter, DefaultDict)
            // Try the original name first, then PascalCase, then case-insensitive match
            // for Python-style names that don't split cleanly (e.g., "defaultdict" → "DefaultDict")
            var symTableType = Context.SymbolTable.Lookup(generic.Name) as TypeSymbol
                ?? Context.SymbolTable.Lookup(NameMangler.ToPascalCase(generic.Name)) as TypeSymbol
                ?? Context.SymbolTable.LookupCaseInsensitive(generic.Name) as TypeSymbol;
            if (symTableType != null)
            {
                if (symTableType.ProtocolMethods.ContainsKey(dunderName))
                    return true;

                if (symTableType.Methods.Any(m => m.Name == dunderName))
                    return true;

                if (symTableType.ClrType != null && HasClrProtocol(symTableType.ClrType, dunderName))
                    return true;
            }
        }

        // Enum types support __iter__ (via Enum.GetValues<T>())
        if (type is UserDefinedType { Symbol.TypeKind: TypeKind.Enum } && dunderName == DunderNames.Iter)
            return true;

        // Check Sharpy user-defined types
        if (type is UserDefinedType udt)
        {
            // If Symbol is null (e.g., return type from CLR module discovery),
            // resolve it from the SymbolTable by name
            var symbol = udt.Symbol ?? Context.SymbolTable.Lookup(udt.Name) as TypeSymbol;
            if (symbol != null)
            {
                if (symbol.ProtocolMethods.ContainsKey(dunderName))
                    return true;

                if (symbol.Methods.Any(m => m.Name == dunderName))
                    return true;

                // Check CLR type if available
                if (symbol.ClrType != null && HasClrProtocol(symbol.ClrType, dunderName))
                    return true;
            }
        }

        // Check builtin types with CLR backing
        if (type is BuiltinType builtin && builtin.ClrType != null)
        {
            if (HasClrProtocol(builtin.ClrType, dunderName))
                return true;
        }

        // For other types (including int, bool, etc.), default to false for most protocols
        // except __str__ and __hash__ which all objects have
        if (dunderName is DunderNames.Str or DunderNames.Hash)
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a CLR type supports a protocol by examining its interfaces.
    /// </summary>
    private bool HasClrProtocol(System.Type clrType, string dunderName)
    {
        // Check for IEnumerable<T> or IEnumerable -> __iter__
        if (dunderName == DunderNames.Iter)
        {
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(clrType))
                return true;

            // Check for Sharpy.Iterator<T> base class
            if (ClrTypeHelper.GetIteratorElementType(clrType) != null)
                return true;
        }

        // ICollection -> __len__, __contains__
        if (dunderName is DunderNames.Len or DunderNames.Contains)
        {
            if (typeof(System.Collections.ICollection).IsAssignableFrom(clrType))
                return true;
        }

        // IList -> __getitem__, __setitem__
        if (dunderName is DunderNames.GetItem or DunderNames.SetItem)
        {
            if (typeof(System.Collections.IList).IsAssignableFrom(clrType))
                return true;
        }

        // IDictionary -> __getitem__, __setitem__, __contains__, __len__
        if (dunderName is DunderNames.GetItem or DunderNames.SetItem or DunderNames.Contains or DunderNames.Len)
        {
            if (typeof(System.Collections.IDictionary).IsAssignableFrom(clrType))
                return true;

            // Also check generic IDictionary<,> (Sharpy's Dict<K,V> implements this but not the non-generic)
            if (clrType.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IDictionary<,>)))
                return true;
        }

        // IReadOnlyDictionary<,> -> __getitem__, __contains__, __len__ (read-only mapping; no __setitem__)
        if (dunderName is DunderNames.GetItem or DunderNames.Contains or DunderNames.Len)
        {
            if (clrType.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IReadOnlyDictionary<,>)))
                return true;
        }

        // Check Sharpy protocol interfaces (mirrors OverloadIndexBuilder.DiscoverTypeProtocols)
        var interfaces = clrType.GetInterfaces();

        if (dunderName == DunderNames.Len)
        {
            if (interfaces.Any(i => i.FullName == "Sharpy.ISized"))
                return true;
        }

        if (dunderName == DunderNames.Bool)
        {
            if (interfaces.Any(i => i.FullName == "Sharpy.IBoolConvertible"))
                return true;
        }

        if (dunderName == DunderNames.Reversed)
        {
            if (interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == "Sharpy.IReverseEnumerable`1"))
                return true;
        }

        return false;
    }
}
