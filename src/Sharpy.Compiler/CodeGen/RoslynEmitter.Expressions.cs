using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Expression generation dispatch and small utilities.
/// Sub-partials: Operators, Literals, Access
/// </summary>
internal partial class RoslynEmitter
{
    private ExpressionSyntax GenerateExpression(Sharpy.Compiler.Parser.Ast.Expression expr)
    {
        return expr switch
        {
            // Literals
            IntegerLiteral intLit => GenerateIntegerLiteral(intLit),
            FloatLiteral floatLit => GenerateFloatLiteral(floatLit),
            StringLiteral strLit => GenerateStringLiteral(strLit),
            BytesLiteralExpression bytesLit => GenerateBytesLiteral(bytesLit),
            BooleanLiteral boolLit => LiteralExpression(boolLit.Value ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
            NoneLiteral => LiteralExpression(SyntaxKind.DefaultLiteralExpression),
            EllipsisLiteral => GenerateEllipsisLiteral(),

            // Collections
            ListLiteral listLit => GenerateListLiteral(listLit),
            DictLiteral dictLit => GenerateDictLiteral(dictLit),
            SetLiteral setLit => GenerateSetLiteral(setLit),
            TupleLiteral tupleLit => GenerateTupleLiteral(tupleLit),

            // Comprehensions
            ListComprehension listComp => GenerateListComprehension(listComp),
            SetComprehension setComp => GenerateSetComprehension(setComp),
            DictComprehension dictComp => GenerateDictComprehension(dictComp),
            DictSpreadComprehension dictSpreadComp => GenerateDictSpreadComprehension(dictSpreadComp),

            // Primary expressions
            // Handle 'self' -> 'this' conversion for instance methods
            // When _selfReplacementIdentifier is set (inlined operator body), map to that instead
            Identifier name when string.Equals(name.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase) =>
                _selfReplacementIdentifier != null
                    ? IdentifierName(_selfReplacementIdentifier)
                    : ThisExpression(),
            Identifier name => GenerateIdentifierExpression(name),
            SuperExpression => BaseExpression(),  // super() -> base
            MemberAccess memberAccess => GenerateMemberAccess(memberAccess),
            IndexAccess indexAccess => GenerateIndexAccess(indexAccess),
            SliceAccess sliceAccess => GenerateSliceAccess(sliceAccess),
            // Handle None() -> Optional<T>.None
            FunctionCall call when call.Function is NoneLiteral
                && call.Arguments.Length == 0
                && GetExpressionSemanticType(call) is OptionalType optNone
                => GenerateOptionalNone(optNone),
            // Handle Some/Ok/Err -> Optional/Result factory calls (tagged union constructors)
            FunctionCall call when IsTaggedUnionConstructorCall(call) => GenerateTaggedUnionConstructor(call),
            FunctionCall call => GenerateCall(call),

            // Operators
            UnaryOp unaryOp => GenerateUnaryOp(unaryOp),
            BinaryOp binOp => GenerateBinaryOp(binOp),
            ComparisonChain chain => GenerateComparisonChain(chain),

            // Advanced expressions
            ConditionalExpression cond => GenerateConditionalExpression(cond),
            LambdaExpression lambda => GenerateLambdaExpression(lambda),
            TypeCoercion coercion => GenerateTypeCoercion(coercion),
            TypeCheck check => GenerateTypeCheck(check),
            Parenthesized paren => ParenthesizedExpression(GenerateExpression(paren.Expression)),

            // F-strings and T-strings
            FStringLiteral fstring => GenerateFString(fstring),
            TStringLiteral tstring => GenerateTString(tstring),

            // Try/Maybe expressions
            TryExpression tryExpr => GenerateTryExpression(tryExpr),
            MaybeExpression maybeExpr => GenerateMaybeExpression(maybeExpr),

            // Await expression
            Parser.Ast.AwaitExpression awaitExpr => GenerateAwaitExpression(awaitExpr),

            // Walrus operator
            WalrusExpression walrus => GenerateWalrusExpression(walrus),

            // Match expression
            MatchExpression matchExpr => GenerateMatchExpression(matchExpr),

            // Spread/star — normally handled by collection literal and assignment codegen.
            // If reached here, emit a diagnostic — this is an unsupported context.
            SpreadElement spread => EmitNotImplementedExpression(
                "Spread expression (*) is not supported in this context",
                DiagnosticCodes.CodeGen.UnsupportedExpressionType,
                spread.LineStart, spread.ColumnStart),
            StarExpression star => EmitNotImplementedExpression(
                "Star expression (*) is not supported in this context",
                DiagnosticCodes.CodeGen.UnsupportedExpressionType,
                star.LineStart, star.ColumnStart),

            // ModifiedArgument — handled at call-site level in GeneratePositionalArguments.
            // If reached here (standalone context), just emit the inner expression.
            ModifiedArgument modArg => GenerateExpression(modArg.Argument),

            _ => EmitNotImplementedExpression(
                $"Unsupported expression type in code generation: '{expr.GetType().Name}'",
                DiagnosticCodes.CodeGen.UnsupportedExpressionType, expr.LineStart, expr.ColumnStart)
        };
    }

    /// <summary>
    /// Generates an identifier expression, with Optional narrowing support.
    /// When a variable has been narrowed from Optional&lt;T&gt; to T (via an is-not-None check),
    /// emits identifier.Unwrap() to extract the underlying value.
    /// </summary>
    private ExpressionSyntax GenerateIdentifierExpression(Identifier name)
    {
        // In event accessor bodies, rewrite the explicit handler parameter to C#'s implicit 'value'
        if (_eventHandlerParamName != null
            && string.Equals(name.Name, _eventHandlerParamName, StringComparison.Ordinal))
        {
            return IdentifierName("value");
        }

        // Builtin function references (e.g., key=len, map(int, items)) need full qualification.
        // Shadowing check: if the semantic analysis resolved this identifier to a VariableSymbol,
        // it's a local variable shadowing the builtin — skip the builtin emission path.
        var resolvedSymbol = _context.SemanticInfo?.GetIdentifierSymbol(name);

        // Inline CLR namespace identifier (e.g., `System` resolved by semantic analysis
        // to a synthetic .NET ModuleSymbol). Emit the namespace name verbatim so chained
        // access produces valid C# (e.g., `System`.Console.WriteLine → System.Console.WriteLine).
        // Bypasses GetMangledVariableName which would otherwise camel-case it to "system".
        if (resolvedSymbol is ModuleSymbol { IsNetModule: true, NetNamespaceName: { } netNamespaceName })
        {
            return IdentifierName(netNamespaceName);
        }

        if (resolvedSymbol is not VariableSymbol)
        {
            var symbol = _context.LookupSymbol(name.Name);
            if (symbol is FunctionSymbol { CodeGenInfo: null }
                && _context.IsBuiltinFunction(name.Name))
            {
                return MakeGlobalQualifiedName("Sharpy", "Builtins",
                    NameCasing.ResolveMethod(name.Name, name.IsNameBacktickEscaped));
            }

            // Type-name builtin function references (e.g., map(int, items) → Builtins.Int)
            // C# method group conversion handles overload selection automatically.
            if (symbol is TypeSymbol && _context.IsBuiltinFunction(name.Name))
            {
                return MakeGlobalQualifiedName("Sharpy", "Builtins",
                    NameCasing.ResolveMethod(name.Name, name.IsNameBacktickEscaped));
            }

            // Discovered TypeSymbols with CLR type info must resolve through the CLR type name.
            // Sharpy namespace: datetime → global::Sharpy.DateTime
            // System namespace (when inside a user namespace): Math → global::System.Math
            {
                var ts = (resolvedSymbol as TypeSymbol) ?? (symbol as TypeSymbol);
                if (ts?.ClrType != null)
                {
                    if (ts.ClrType.Namespace == "Sharpy")
                    {
                        var clrName = ClrNameHelper.StripArity(ts.ClrType.Name);
                        return MakeGlobalQualifiedName("Sharpy", clrName);
                    }

                    if (!string.IsNullOrEmpty(_context.ProjectNamespace))
                    {
                        var fullName = ClrNameHelper.StripArity(ts.ClrType.FullName!);
                        return MakeGlobalQualifiedName(fullName.Split('.'));
                    }
                }
            }
        }

        var mangledName = GetMangledVariableName(name.Name, isNewDeclaration: false);
        ExpressionSyntax expr = IdentifierName(mangledName);

        // If this variable has been narrowed from Optional<T>/Nullable<T> to T,
        // emit .Unwrap() for Optional, .Value for value-type Nullable, or ! for reference-type Nullable
        if (_narrowing.IsNarrowed(name.Name))
        {
            if (_narrowing.IsNullableNarrowed(name.Name))
            {
                // Value-type nullable (int?, bool?, etc.) → .Value
                expr = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    expr,
                    IdentifierName("Value"));
            }
            else if (_narrowing.IsReferenceNullableNarrowed(name.Name))
            {
                // Reference-type nullable (string?, MyClass?, etc.) → !
                // C# only auto-narrows locals after null checks, not fields.
                expr = PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression, expr);
            }
            else
            {
                // Optional<T> → .Unwrap()
                expr = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        expr,
                        IdentifierName(ProtocolConstants.Unwrap)))
                    .WithArgumentList(ArgumentList());
            }
        }

        // If this variable has been narrowed by isinstance(), wrap with cast
        // Wraps in parentheses: ((Dog)animal) so member access works: ((Dog)animal).Breed
        if (_narrowing.IsInstanceNarrowed(name.Name))
        {
            var narrowedType = _narrowing.GetIsInstanceNarrowedType(name.Name)!;
            expr = ParenthesizedExpression(
                CastExpression(
                    ParseTypeName(narrowedType),
                    expr));
        }

        return expr;
    }

    private ExpressionSyntax GenerateIntegerLiteral(IntegerLiteral literal)
    {
        var text = literal.Value.Replace("_", "", StringComparison.Ordinal);

        long value;
        try
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                value = long.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            else if (text.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
                value = Convert.ToInt64(text[2..], 8);
            else if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
                value = Convert.ToInt64(text[2..], 2);
            else
                value = long.Parse(text, CultureInfo.InvariantCulture);
        }
        catch (OverflowException)
        {
            _context.Diagnostics.AddError(
                $"Integer literal '{literal.Value}' is too large for a 64-bit integer",
                literal.LineStart, literal.ColumnStart,
                code: DiagnosticCodes.CodeGen.EmitError);
            return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0));
        }

        if (value >= int.MinValue && value <= int.MaxValue)
            return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((int)value));
        else
            return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));
    }

    private ExpressionSyntax GenerateFloatLiteral(FloatLiteral literal)
    {
        var value = double.Parse(literal.Value, CultureInfo.InvariantCulture);

        if (literal.Suffix != null)
        {
            var text = literal.Value + literal.Suffix;
            if (literal.Suffix.Equals("f", StringComparison.OrdinalIgnoreCase))
                return LiteralExpression(SyntaxKind.NumericLiteralExpression,
                    Literal(text, (float)value));
            if (literal.Suffix.Equals("d", StringComparison.OrdinalIgnoreCase))
                return LiteralExpression(SyntaxKind.NumericLiteralExpression,
                    Literal(text, value));
            if (literal.Suffix.Equals("m", StringComparison.OrdinalIgnoreCase))
            {
                var decimalValue = decimal.Parse(literal.Value, CultureInfo.InvariantCulture);
                return LiteralExpression(SyntaxKind.NumericLiteralExpression,
                    Literal(text, decimalValue));
            }
        }

        // Append 'd' suffix to force Roslyn to preserve double literal semantics.
        // Without it, Roslyn may normalize whole-number doubles (e.g., 5.0 -> 5).
        var literalText = literal.Value.Contains('.', StringComparison.Ordinal) || literal.Value.Contains('e', StringComparison.Ordinal)
            || literal.Value.Contains('E', StringComparison.Ordinal)
            ? literal.Value + "d"
            : literal.Value + ".0d";
        return LiteralExpression(SyntaxKind.NumericLiteralExpression,
            Literal(literalText, value));
    }

    private ExpressionSyntax GenerateStringLiteral(StringLiteral literal)
    {
        return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(literal.Value));
    }

    private ExpressionSyntax GenerateBytesLiteral(BytesLiteralExpression literal)
    {
        // Convert each character to a byte literal: b"hello" -> new Sharpy.Bytes(new byte[] { 104, 101, 108, 108, 111 })
        var byteValues = literal.Value.Select(c =>
            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal((byte)c)));

        var byteArrayInit = ArrayCreationExpression(
            ArrayType(PredefinedType(Token(SyntaxKind.ByteKeyword)))
                .WithRankSpecifiers(SingletonList(
                    ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(
                        OmittedArraySizeExpression())))))
            .WithInitializer(
                InitializerExpression(
                    SyntaxKind.ArrayInitializerExpression,
                    SeparatedList<ExpressionSyntax>(byteValues)));

        return ObjectCreationExpression(ParseTypeName(CSharpTypeNames.SharpyBytes))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(byteArrayInit))));
    }

    /// <summary>
    /// Gets the semantic type of an expression from SemanticInfo, if available.
    /// </summary>
    private SemanticType? GetExpressionSemanticType(Sharpy.Compiler.Parser.Ast.Expression expr)
    {
        return _context.SemanticInfo?.GetExpressionType(expr);
    }

    /// <summary>
    /// Returns true if the type represents <c>object</c> (System.Object).
    /// Uses name-based comparison because SemanticType.Object is a UserDefinedType
    /// and may not be reference-equal to discovery-loaded instances.
    /// </summary>
    private static bool IsObjectType(SemanticType? type) =>
        type != null && type.GetDisplayName() == "object";

    /// <summary>
    /// Extracts the element type from an iterable semantic type.
    /// E.g., GenericType("list", [int]) → int; GenericType("IEnumerable", [string]) → string.
    /// Returns null if the element type cannot be determined.
    /// </summary>
    private static SemanticType? ExtractElementType(SemanticType? type)
    {
        if (type is GenericType gt && gt.TypeArguments.Count >= 1
            && gt.TypeArguments[0] is not UnknownType)
        {
            return gt.TypeArguments[0];
        }
        return null;
    }

    /// <summary>
    /// Tries to infer the element type for a collection constructor call like <c>list(d.keys())</c>
    /// by examining the argument's AST structure. When the semantic type of the argument is
    /// unknown (e.g., property access on discovery-loaded types), this extracts the element type
    /// from the receiver's generic type arguments (#555).
    /// </summary>
    private SemanticType? TryInferElementTypeFromArg(Parser.Ast.Expression arg)
    {
        // First try the direct semantic type (skip if it resolved to object — too generic)
        var directType = ExtractElementType(GetExpressionSemanticType(arg));
        if (directType != null && !IsObjectType(directType))
            return directType;

        // For list(inner) / set(inner) wrapping, recurse on the inner argument
        if (arg is FunctionCall { Function: Identifier wrapperId } wrapperCall
            && wrapperCall.Arguments.Length == 1
            && wrapperId.Name is BuiltinNames.List or BuiltinNames.Set)
        {
            return TryInferElementTypeFromArg(wrapperCall.Arguments[0]);
        }

        // For d.keys() / d.values() on a dict-like type, extract from the receiver's generic args
        if (arg is FunctionCall { Function: MemberAccess ma })
        {
            var receiverType = GetExpressionSemanticType(ma.Object);
            if (receiverType is GenericType receiverGeneric && receiverGeneric.TypeArguments.Count >= 2)
            {
                return ma.Member switch
                {
                    "keys" => receiverGeneric.TypeArguments[0] is not UnknownType
                        ? receiverGeneric.TypeArguments[0] : null,
                    "values" => receiverGeneric.TypeArguments[1] is not UnknownType
                        ? receiverGeneric.TypeArguments[1] : null,
                    _ => null
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Wraps an expression used in a boolean context with truthiness conversion if needed.
    /// For strings: emits <c>s.Length > 0</c> (empty string is falsy).
    /// Other types pass through unchanged (bool is already valid, UDTs use operator true).
    /// </summary>
    private ExpressionSyntax WrapTruthinessIfNeeded(ExpressionSyntax expr, Parser.Ast.Expression astExpr)
    {
        var type = GetExpressionSemanticType(astExpr);
        if (type == SemanticType.Str)
        {
            // s.Length > 0
            return BinaryExpression(SyntaxKind.GreaterThanExpression,
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    expr,
                    IdentifierName("Length")),
                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));
        }
        return expr;
    }

    /// <summary>
    /// Checks if a function call is a tagged union constructor (Some, Ok, Err)
    /// by checking the expression's semantic type from SemanticInfo.
    /// </summary>
    private bool IsTaggedUnionConstructorCall(FunctionCall call)
    {
        if (call.Function is not Identifier id)
            return false;

        if (id.Name is not ("Some" or "Ok" or "Err"))
            return false;

        var exprType = GetExpressionSemanticType(call);
        return exprType is OptionalType or ResultType;
    }

    private ExpressionSyntax GenerateAwaitExpression(Parser.Ast.AwaitExpression awaitExpr)
    {
        var operand = GenerateExpression(awaitExpr.Operand);
        return SyntaxFactory.AwaitExpression(operand);
    }
}
