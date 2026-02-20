using System.IO;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Function calls, member access, index/slice access, lambdas,
/// module paths, tagged union constructors, single-evaluation helpers
/// </summary>
internal partial class RoslynEmitter
{
    private ExpressionSyntax GenerateCall(FunctionCall call)
    {
        // Handle generic type/function instantiation: Box[int](42) or identity[int](42)
        // This is parsed as FunctionCall(Function: IndexAccess(Object: Box/identity, Index: int), Arguments: [42])
        if (call.Function is IndexAccess indexAccess &&
            indexAccess.Object is Identifier genericName)
        {
            var symbol = _context.LookupSymbol(genericName.Name);

            // Map the type argument(s)
            var typeArgsSyntax = _typeMapper.MapTypeArgumentsFromExpression(indexAccess.Index);

            if (symbol is TypeSymbol genericTypeSymbol && genericTypeSymbol.IsGeneric)
            {
                // Generate: new GenericType<TypeArgs>(args)
                var csharpGenericTypeName = GetBuiltinCollectionTypeName(genericName.Name)
                    ?? NameMangler.ToPascalCase(genericName.Name);
                var genericTypeSyntax = TypeMapper.QualifiedGenericName(csharpGenericTypeName, typeArgsSyntax);

                // Generate arguments
                var positionalArgs = call.Arguments.Select(arg => Argument(GenerateExpression(arg)));
                var keywordArgs = call.KeywordArguments.Select(kwarg =>
                    Argument(GenerateExpression(kwarg.Value))
                        .WithNameColon(NameColon(IdentifierName(NameMangler.ToCamelCase(kwarg.Name)))));
                var allArgs = positionalArgs.Concat(keywordArgs).ToArray();

                return ObjectCreationExpression(genericTypeSyntax)
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
            }

            if (symbol is FunctionSymbol genericFuncSymbol && genericFuncSymbol.IsGeneric)
            {
                // Generate: GenericFunction<TypeArgs>(args)
                var genericFuncSyntax = GenericName(NameMangler.ToPascalCase(genericName.Name))
                    .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgsSyntax)));

                // Generate arguments
                var positionalArgs = call.Arguments.Select(arg => Argument(GenerateExpression(arg)));
                var keywordArgs = call.KeywordArguments.Select(kwarg =>
                    Argument(GenerateExpression(kwarg.Value))
                        .WithNameColon(NameColon(IdentifierName(NameMangler.ToCamelCase(kwarg.Name)))));
                var allArgs = positionalArgs.Concat(keywordArgs).ToArray();

                return InvocationExpression(genericFuncSyntax)
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
            }
        }

        if (call.Function is Identifier funcName)
        {
            // Check if this is a builtin function call (e.g., int(), str(), print(), len(), etc.)
            var isBuiltinFunc = _context.IsBuiltinFunction(funcName.Name);

            // User-defined functions shadow builtins (Python scoping rules):
            // A FunctionSymbol with CodeGenInfo was processed by semantic analysis (user-defined),
            // while builtin functions from the registry won't have CodeGenInfo set.
            var symbol = _context.LookupSymbol(funcName.Name);
            if (isBuiltinFunc && symbol is FunctionSymbol { CodeGenInfo: not null })
                isBuiltinFunc = false;

            // isinstance(expr, Type) → expr is Type
            // Must intercept BEFORE argument evaluation because the second argument
            // is a type identifier, not a value expression.
            if (funcName.Name == "isinstance"
                && call.Arguments.Length == 2
                && call.Arguments[1] is Identifier typeId)
            {
                var value = GenerateExpression(call.Arguments[0]);
                // Use TypeMapper to correctly resolve builtin types (str→string, int→int)
                // and user-defined types (dog→Dog) via MapType.
                var typeAnnotation = new TypeAnnotation { Name = typeId.Name };
                var checkType = _typeMapper.MapType(typeAnnotation);
                return BinaryExpression(SyntaxKind.IsExpression, value, checkType);
            }

            // Check if this is a type instantiation (calling a class or struct constructor)
            // We use the symbol table which is populated during semantic analysis.
            // This handles both local type definitions and imported types.
            // NOTE: Builtin functions are NOT type instantiations (e.g., int(x) is a conversion function)
            var isTypeInstantiation = !isBuiltinFunc &&
                                      symbol is TypeSymbol typeSymbol &&
                                      (typeSymbol.TypeKind == Semantic.TypeKind.Class ||
                                       typeSymbol.TypeKind == Semantic.TypeKind.Struct);

            // Generate positional arguments
            var positionalArgs = call.Arguments.Select(arg => Argument(GenerateExpression(arg)));

            // Generate keyword arguments with named syntax
            var keywordArgs = call.KeywordArguments.Select(kwarg =>
                Argument(GenerateExpression(kwarg.Value))
                    .WithNameColon(NameColon(IdentifierName(NameMangler.ToCamelCase(kwarg.Name)))));

            // Combine positional and keyword arguments
            var allArgs = positionalArgs.Concat(keywordArgs).ToArray();

            if (isBuiltinFunc)
            {
                // Use explicit AliasQualifiedName to handle all expression contexts (f-strings, etc.)
                var builtinName = MakeGlobalQualifiedName("Sharpy", "Builtins", NameMangler.ToPascalCase(funcName.Name));
                return InvocationExpression(builtinName)
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
            }

            if (isTypeInstantiation && symbol is TypeSymbol typeSymbolForName)
            {
                // For type instantiation, use fully qualified name if type is from another file
                var name = GetFullyQualifiedTypeName(typeSymbolForName, funcName.Name);

                // For generic types called without explicit type arguments (e.g., set()),
                // use the resolved expression type to supply type arguments.
                var exprType = _context.SemanticInfo?.GetExpressionType(call);
                if (exprType is GenericType resolvedGeneric && resolvedGeneric.TypeArguments.Count > 0
                    && resolvedGeneric.TypeArguments.All(t => t is not UnknownType))
                {
                    var typeArgsSyntax = resolvedGeneric.TypeArguments
                        .Select(t => _typeMapper.MapSemanticType(t));
                    var csharpCollectionName = GetBuiltinCollectionTypeName(funcName.Name)
                        ?? NameMangler.ToPascalCase(funcName.Name);
                    var genericTypeSyntax = TypeMapper.QualifiedGenericName(csharpCollectionName,
                            typeArgsSyntax.ToArray());
                    return ObjectCreationExpression(genericTypeSyntax)
                        .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
                }

                // For builtin collection types, use the fully-qualified Sharpy.X name
                var collectionName = GetBuiltinCollectionTypeName(funcName.Name);
                if (collectionName != null)
                {
                    return ObjectCreationExpression(ParseName(collectionName))
                        .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
                }

                return ObjectCreationExpression(ParseName(name))
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
            }

            // Regular function call — check if this is a local variable/parameter (callable)
            // before falling back to PascalCase for module-level functions
            var codeGenInfo = symbol != null ? GetCodeGenInfo(symbol) : null;
            string funcCSharpName;
            if (codeGenInfo?.CSharpName != null)
            {
                funcCSharpName = codeGenInfo.CSharpName;
            }
            else if (_variableVersions.ContainsKey(_nameResolutionService.GetBaseName(funcName.Name)))
            {
                // Parameter or local variable with callable type — use camelCase resolution
                funcCSharpName = GetMangledVariableName(funcName.Name, isNewDeclaration: false);
            }
            else
            {
                funcCSharpName = NameMangler.ToPascalCase(funcName.Name);
            }
            return InvocationExpression(ParseName(funcCSharpName))
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        // Handle method calls on objects: obj.method() or ClassName.static_method()
        if (call.Function is MemberAccess memberAccess)
        {
            var obj = GenerateExpression(memberAccess.Object);

            // Cross-dunder calls: transform operator dunders to C# operator expressions.
            // e.g., self.__lt__(other) → this < other, self.__neg__() → -this
            // This must happen BEFORE regular method name resolution so that operator dunders
            // emit operators instead of __PascalCase__ method calls.
            if (DunderMapping.IsDunderMethod(memberAccess.Member))
            {
                var binaryKind = DunderMapping.TryGetBinaryExpressionKind(memberAccess.Member);
                if (binaryKind != null && call.Arguments.Length == 1)
                {
                    var arg = GenerateExpression(call.Arguments[0]);
                    return BinaryExpression(binaryKind.Value, obj, arg);
                }

                var unaryKind = DunderMapping.TryGetUnaryExpressionKind(memberAccess.Member);
                if (unaryKind != null && call.Arguments.Length == 0)
                {
                    return PrefixUnaryExpression(unaryKind.Value, obj);
                }
            }

            // Apply name mangling to method name
            // First check for dunder methods, then Python list method mappings (append -> Add, etc.)
            var methodName = DunderMapping.ResolveCSharpName(memberAccess.Member)
                ?? NameMangler.GetListMethodMapping(memberAccess.Member)
                ?? NameMangler.ToPascalCase(memberAccess.Member);

            // Guard: super().__init__() outside constructor context would produce base.Constructor()
            // which is invalid C#. This should have been handled in GenerateConstructor.
            if (methodName == "Constructor" && memberAccess.Object is SuperExpression)
            {
                return EmitNotImplementedExpression(
                    "super().__init__() must be in __init__ method body to be converted to base constructor call",
                    DiagnosticCodes.CodeGen.UnsupportedFeature, call.LineStart, call.ColumnStart);
            }

            // Property-vs-method dispatch for Optional/Result:
            // is_some/is_none/is_ok/is_err are C# properties, not methods.
            // Emit property access instead of method invocation.
            if (call.Arguments.Length == 0 && call.KeywordArguments.Length == 0)
            {
                var objType = GetExpressionSemanticType(memberAccess.Object);
                if (objType is OptionalType && methodName is "IsSome" or "IsNone")
                {
                    return MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression, obj, IdentifierName(methodName));
                }
                if (objType is ResultType && methodName is "IsOk" or "IsErr")
                {
                    return MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression, obj, IdentifierName(methodName));
                }
            }

            // Generate positional arguments
            var positionalArgs = call.Arguments.Select(arg => Argument(GenerateExpression(arg)));

            // Generate keyword arguments with named syntax
            var keywordArgs = call.KeywordArguments.Select(kwarg =>
                Argument(GenerateExpression(kwarg.Value))
                    .WithNameColon(NameColon(IdentifierName(NameMangler.ToCamelCase(kwarg.Name)))));

            // Combine positional and keyword arguments
            var allArgs = positionalArgs.Concat(keywordArgs).ToArray();

            // Handle null conditional method calls: obj?.Method(args)
            if (memberAccess.IsNullConditional)
            {
                // For Optional<T>: lower to ternary since ?.  doesn't work on structs
                if (GetExpressionSemanticType(memberAccess.Object) is OptionalType)
                {
                    // Ensure obj is only evaluated once for complex expressions
                    var (safeObj, capture) = EnsureSingleEvaluation(obj, memberAccess.Object);
                    // safeObj.IsSome ? safeObj.Unwrap().Method(args) : Optional<T>.None
                    var methodCall = InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    safeObj, IdentifierName("Unwrap")))
                                .WithArgumentList(ArgumentList()),
                            IdentifierName(methodName)))
                        .WithArgumentList(ArgumentList(SeparatedList(allArgs)));

                    ExpressionSyntax cond = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        ParenthesizedExpression(safeObj), IdentifierName("IsSome"));
                    if (capture != null)
                        cond = BinaryExpression(SyntaxKind.LogicalAndExpression, capture, cond);

                    // Use Optional<T>.None for the false branch so C# resolves the ternary
                    // as Optional<T> (via implicit conversion on the true branch if needed)
                    var falseExpr = GetExpressionSemanticType(call) is OptionalType optCallType
                        ? (ExpressionSyntax)GenerateOptionalNone(optCallType)
                        : (ExpressionSyntax)LiteralExpression(SyntaxKind.DefaultLiteralExpression);

                    return ConditionalExpression(cond, methodCall, falseExpr);
                }

                // Generate: obj?.Method(args)
                // Uses ConditionalAccessExpression with MemberBindingExpression for the method
                // followed by InvocationExpression for the call
                var memberBinding = MemberBindingExpression(IdentifierName(methodName));
                var invocation = InvocationExpression(memberBinding)
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));

                return ConditionalAccessExpression(obj, invocation);
            }

            // Interface default method promotion: if the method is a default method
            // on an interface (not overridden by the class), call through an interface cast.
            // In C#, default interface methods can only be called through interface-typed refs.
            var defaultMethodInterface = TryGetDefaultMethodInterface(memberAccess.Object, memberAccess.Member);
            if (defaultMethodInterface != null)
            {
                var castExpr = ParenthesizedExpression(
                    CastExpression(IdentifierName(defaultMethodInterface), obj));
                var castMethodAccess = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    castExpr,
                    IdentifierName(methodName));
                return InvocationExpression(castMethodAccess)
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
            }

            // Generate: obj.Method(args)
            var methodAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                obj,
                IdentifierName(methodName));

            return InvocationExpression(methodAccess)
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        return EmitNotImplementedExpression(
            "Unsupported expression type in code generation: complex function expressions are not yet supported",
            DiagnosticCodes.CodeGen.UnsupportedExpressionType, call.LineStart, call.ColumnStart);
    }

    private ExpressionSyntax GenerateMemberAccess(MemberAccess memberAccess)
    {
        // Check for nested module access (e.g., lib.math.add -> Lib.Math.Add)
        // This must be checked before enum handling to ensure module paths take precedence
        if (TryExtractModulePath(memberAccess, out var modulePath))
        {
            return BuildModuleAccessExpression(modulePath);
        }

        // Check for enum member access (e.g., Color.RED -> Color.Red)
        if (memberAccess.Object is Identifier enumTypeIdentifier)
        {
            var symbol = _context.LookupSymbol(enumTypeIdentifier.Name);

            // If this is an enum type, handle member access specially
            if (symbol is TypeSymbol enumSymbol && enumSymbol.TypeKind == Semantic.TypeKind.Enum)
            {
                // Enum member access: Color.RED -> Color.Red
                // Types are nested in the module class, accessible via unqualified names
                var enumTypeName = NameMangler.ToPascalCase(enumTypeIdentifier.Name);

                // Use the enum type directly (nested types are accessible within the module class)
                var enumType = IdentifierName(enumTypeName);

                // Check if this is a string enum (string enums are generated as classes, not C# enums)
                if (IsStringEnumSymbol(enumSymbol))
                {
                    // String enums use CONSTANT_CASE field names (same as NameContext.Constant)
                    var fieldName = NameMangler.Transform(memberAccess.Member, NameContext.Constant);
                    var enumMemberAccess = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        enumType,
                        IdentifierName(fieldName));
                    // String enums: Color.RED already returns the string value from the static field
                    return enumMemberAccess;
                }
                else
                {
                    // Integer enums use PascalCase member names
                    var enumMemberName = NameMangler.ToEnumMemberName(memberAccess.Member);
                    var enumMemberAccess = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        enumType,
                        IdentifierName(enumMemberName));
                    // Return the enum member directly (not cast to int)
                    // The .value property is used to get the underlying int value
                    return enumMemberAccess;
                }
            }
        }

        var obj = GenerateExpression(memberAccess.Object);

        // Handle special .value and .name properties for enum instances
        if (memberAccess.Member is "value" or "name" && IsEnumInstance(memberAccess.Object))
        {
            if (memberAccess.Member == "value")
            {
                // enum_instance.value -> (int)enum_instance
                return CastExpression(
                    PredefinedType(Token(SyntaxKind.IntKeyword)),
                    obj);
            }

            // enum_instance.name -> enum_instance.ToString()
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    obj,
                    IdentifierName("ToString")));
        }

        // Named tuple element access: keep element names as-is (no PascalCase)
        if (GetExpressionSemanticType(memberAccess.Object) is Semantic.TupleType namedTupleType
            && namedTupleType.IsNamed)
        {
            var names = namedTupleType.ElementNames!.Value;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == memberAccess.Member)
                {
                    return MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        obj,
                        IdentifierName(memberAccess.Member));
                }
            }
        }

        // Apply name mangling to member names:
        // - Dunder methods use DunderMapping
        // - ALL_CAPS names (Python-style constants) use CONSTANT_CASE
        // - Other names use PascalCase
        var mangledMemberName = DunderMapping.ResolveCSharpName(memberAccess.Member)
            ?? (NameFormDetector.IsConstantCaseName(memberAccess.Member)
                ? NameMangler.ToConstantCase(memberAccess.Member)
                : NameMangler.ToPascalCase(memberAccess.Member));
        var member = IdentifierName(mangledMemberName);

        ExpressionSyntax result;

        if (memberAccess.IsNullConditional)
        {
            // For Optional<T>: lower to ternary since ?. doesn't work on structs
            if (GetExpressionSemanticType(memberAccess.Object) is OptionalType)
            {
                // Ensure obj is only evaluated once for complex expressions
                var (safeObj, capture) = EnsureSingleEvaluation(obj, memberAccess.Object);
                // safeObj.IsSome ? safeObj.Unwrap().Member : Optional<T>.None
                ExpressionSyntax cond = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    ParenthesizedExpression(safeObj), IdentifierName("IsSome"));
                if (capture != null)
                    cond = BinaryExpression(SyntaxKind.LogicalAndExpression, capture, cond);

                var trueExpr = (ExpressionSyntax)MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            safeObj, IdentifierName("Unwrap")))
                        .WithArgumentList(ArgumentList()),
                    member);

                // Use Optional<T>.None for the false branch so C# resolves the ternary
                // as Optional<T> (via implicit conversion on the true branch if needed)
                var falseExpr = GetExpressionSemanticType(memberAccess) is OptionalType optExprType
                    ? (ExpressionSyntax)GenerateOptionalNone(optExprType)
                    : LiteralExpression(SyntaxKind.DefaultLiteralExpression);

                result = ConditionalExpression(cond, trueExpr, falseExpr);
            }
            else
            {
                // obj?.member
                result = ConditionalAccessExpression(obj,
                    MemberBindingExpression(member));
            }
        }
        else
        {
            // obj.member
            result = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                obj,
                member);
        }

        // If this member access path has been narrowed by isinstance(),
        // wrap with cast: ((Dog)this.Animal) so further member access works
        var dottedPath = TryBuildDottedPath(memberAccess);
        if (dottedPath != null && IsInstanceNarrowed(dottedPath))
        {
            var narrowedType = GetIsInstanceNarrowedType(dottedPath)!;
            result = ParenthesizedExpression(
                CastExpression(
                    ParseTypeName(narrowedType),
                    result));
        }

        // Optional<T> narrowing for member access paths (self.field is not None)
        if (dottedPath != null && IsNarrowed(dottedPath))
        {
            if (IsNullableNarrowed(dottedPath))
            {
                result = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression, result, IdentifierName("Value"));
            }
            else
            {
                result = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression, result, IdentifierName("Unwrap")))
                    .WithArgumentList(ArgumentList());
            }
        }

        return result;
    }

    /// <summary>
    /// Attempts to extract a module path from a member access chain.
    /// For example, lib.math.add becomes ["lib", "math", "add"].
    /// Returns true if the entire chain represents module access, false otherwise.
    /// </summary>
    private bool TryExtractModulePath(MemberAccess memberAccess, out List<string> modulePath)
    {
        modulePath = new List<string>();

        // Build the path by traversing the member access chain
        Expression current = memberAccess;
        while (current is MemberAccess ma)
        {
            // Add the member name to the front of the list
            modulePath.Insert(0, ma.Member);
            current = ma.Object;
        }

        // The base should be an identifier
        if (current is not Identifier identifier)
        {
            modulePath.Clear();
            return false;
        }

        // Add the base identifier to the front
        modulePath.Insert(0, identifier.Name);

        // Now check if this path represents module access
        // We need at least 2 parts (e.g., lib.math)
        if (modulePath.Count < 2)
        {
            modulePath.Clear();
            return false;
        }

        // Check if the base is a module symbol
        var baseSymbol = _context.LookupSymbol(modulePath[0]);
        if (baseSymbol is not ModuleSymbol)
        {
            modulePath.Clear();
            return false;
        }

        // Verify that the path exists in the module hierarchy
        var currentModule = (ModuleSymbol)baseSymbol;  // Safe cast - we already checked it's a ModuleSymbol
        for (int i = 1; i < modulePath.Count; i++)
        {
            var memberName = modulePath[i];

            // Check if this member exists in the current module's exports
            if (!currentModule.Exports.TryGetValue(memberName, out var exportedSymbol))
            {
                modulePath.Clear();
                return false;
            }

            // If this is not the last element, it should be a nested module
            if (i < modulePath.Count - 1)
            {
                if (exportedSymbol is not ModuleSymbol nestedModule)
                {
                    modulePath.Clear();
                    return false;
                }
                currentModule = nestedModule;
            }
            // The last element can be any symbol (function, variable, or module)
        }

        return true;
    }

    /// <summary>
    /// Builds a C# member access expression from a module path.
    /// For example, ["lib", "math", "add"] becomes Lib.Math.Add.
    /// Special handling for imported modules: if the base is an imported module with a using alias,
    /// use the alias directly. For example, ["config", "MAX_SIZE"] with "import config" becomes
    /// "config.MaxSize" (using the alias created by the using directive).
    /// </summary>
    private ExpressionSyntax BuildModuleAccessExpression(List<string> modulePath)
    {
        if (modulePath.Count == 0)
        {
            throw new ArgumentException("Module path cannot be empty", nameof(modulePath));
        }

        // Check if the base is an imported module symbol
        var baseSymbol = _context.LookupSymbol(modulePath[0]);
        if (baseSymbol is ModuleSymbol)
        {
            // For imported modules, we need to check if we have a using alias
            // For "import parent.child", the alias is "parent_child"
            // For accessing "parent.child.member", we use "parent_child.Member"

            // Find the longest module path prefix that matches an import
            // For example, if we have "import parent.child" and access "parent.child.child_func",
            // we want to find "parent.child" as the import and "child_func" as the member

            ModuleSymbol currentModule = (ModuleSymbol)baseSymbol;
            int modulePartCount = 1;

            // Try to traverse the module hierarchy to find how deep the imported module goes
            for (int i = 1; i < modulePath.Count; i++)
            {
                var memberName = modulePath[i];

                // Check if this is a nested module in the current module's exports
                if (currentModule.Exports.TryGetValue(memberName, out var exportedSymbol)
                    && exportedSymbol is ModuleSymbol nestedModule)
                {
                    currentModule = nestedModule;
                    modulePartCount++;
                }
                else
                {
                    // Not a nested module - this is a member access
                    break;
                }
            }

            // Build the import alias from the module path parts
            // Also escape C# keywords like "base" -> "@base"
            var moduleParts = modulePath.Take(modulePartCount);
            var aliasName = EscapeCSharpKeyword(string.Join("_", moduleParts));

            // If the entire path is just the module (no member access), return the alias
            if (modulePartCount == modulePath.Count)
            {
                return IdentifierName(aliasName);
            }

            // Build member access: alias.Member1.Member2...
            ExpressionSyntax expr = IdentifierName(aliasName);
            for (int i = modulePartCount; i < modulePath.Count; i++)
            {
                // Use CONSTANT_CASE for ALL_CAPS names (Python-style constants)
                var memberPart = modulePath[i];
                var mangledMemberName = NameFormDetector.IsConstantCaseName(memberPart)
                    ? NameMangler.ToConstantCase(memberPart)
                    : NameMangler.ToPascalCase(memberPart);
                expr = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    expr,
                    IdentifierName(mangledMemberName));
            }

            return expr;
        }

        // For multi-part module paths (e.g., lib.math.add) or other cases,
        // build the full qualified path (e.g., Lib.Math.Add)
        ExpressionSyntax currentExpr = IdentifierName(NameMangler.ToPascalCase(modulePath[0]));

        // Chain the rest of the path
        for (int i = 1; i < modulePath.Count; i++)
        {
            // Use CONSTANT_CASE for ALL_CAPS names (Python-style constants)
            var memberPart = modulePath[i];
            var memberName = NameFormDetector.IsConstantCaseName(memberPart)
                ? NameMangler.ToConstantCase(memberPart)
                : NameMangler.ToPascalCase(memberPart);
            currentExpr = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                currentExpr,
                IdentifierName(memberName));
        }

        return currentExpr;
    }

    private ExpressionSyntax GenerateIndexAccess(IndexAccess indexAccess)
    {
        // Tuple positional indexing: t[0] -> t.Item1, t[1] -> t.Item2, etc.
        // C# ValueTuples don't support [] indexing, so we emit .ItemN member access.
        if (GetExpressionSemanticType(indexAccess.Object) is Semantic.TupleType
            && TryGetConstantIntIndex(indexAccess.Index, out var tupleIndex))
        {
            var obj = GenerateExpression(indexAccess.Object);
            var itemName = $"Item{tupleIndex + 1}";
            return MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                obj,
                IdentifierName(itemName));
        }

        var objExpr = GenerateExpression(indexAccess.Object);
        var index = GenerateExpression(indexAccess.Index);

        var elementAccess = ElementAccessExpression(objExpr)
            .AddArgumentListArguments(Argument(index));

        // String indexing: C# string[int] returns char, but Sharpy types it as str.
        // Wrap with .ToString() to bridge the type gap.
        var objectType = GetExpressionSemanticType(indexAccess.Object);
        if (objectType == Semantic.SemanticType.Str)
        {
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    elementAccess,
                    IdentifierName("ToString")));
        }

        return elementAccess;
    }

    /// <summary>
    /// Tries to extract a constant integer value from an expression.
    /// Delegates to <see cref="AstHelper.TryGetConstantIntIndex"/>.
    /// </summary>
    private static bool TryGetConstantIntIndex(Expression expr, out int value)
        => AstHelper.TryGetConstantIntIndex(expr, out value);

    private ExpressionSyntax GenerateSliceAccess(SliceAccess sliceAccess)
    {
        // arr[start:stop:step]
        // Translates to: global::Sharpy.Slice.GetSlice(obj, start, stop, step)
        // where omitted bounds pass null (matching the nullable int? parameters)
        var obj = GenerateExpression(sliceAccess.Object);
        var start = sliceAccess.Start != null
            ? (ExpressionSyntax)CastExpression(
                NullableType(PredefinedType(Token(SyntaxKind.IntKeyword))),
                GenerateExpression(sliceAccess.Start))
            : LiteralExpression(SyntaxKind.NullLiteralExpression);
        var stop = sliceAccess.Stop != null
            ? (ExpressionSyntax)CastExpression(
                NullableType(PredefinedType(Token(SyntaxKind.IntKeyword))),
                GenerateExpression(sliceAccess.Stop))
            : LiteralExpression(SyntaxKind.NullLiteralExpression);
        var step = sliceAccess.Step != null
            ? (ExpressionSyntax)CastExpression(
                NullableType(PredefinedType(Token(SyntaxKind.IntKeyword))),
                GenerateExpression(sliceAccess.Step))
            : LiteralExpression(SyntaxKind.NullLiteralExpression);

        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                MakeGlobalQualifiedName("Sharpy", "Slice"),
                IdentifierName("GetSlice")))
            .AddArgumentListArguments(
                Argument(obj),
                Argument(start),
                Argument(stop),
                Argument(step));
    }

    private ExpressionSyntax GenerateLambdaExpression(LambdaExpression lambda)
    {
        // lambda x, y: x + y → (x, y) => x + y
        var parameters = lambda.Parameters
            .Select(p => Parameter(Identifier(NameMangler.ToCamelCase(p.Name))))
            .ToArray();

        var body = GenerateExpression(lambda.Body);

        if (parameters.Length == 0)
        {
            return ParenthesizedLambdaExpression()
                .WithExpressionBody(body);
        }
        else if (parameters.Length == 1)
        {
            return SimpleLambdaExpression(parameters[0])
                .WithExpressionBody(body);
        }
        else
        {
            return ParenthesizedLambdaExpression()
                .WithParameterList(ParameterList(SeparatedList(parameters)))
                .WithExpressionBody(body);
        }
    }

    /// <summary>
    /// Gets the fully qualified C# type name for a type, handling cross-file references.
    /// Types are nested inside the module class, so cross-file references use
    /// Namespace.ModuleClass.TypeName.
    /// </summary>
    private string GetFullyQualifiedTypeName(TypeSymbol typeSymbol, string sharpyTypeName)
    {
        // Check if type is from a different file (cross-file reference)
        if (!string.IsNullOrEmpty(typeSymbol.DefiningFilePath) &&
            !string.IsNullOrEmpty(_context.SourceFilePath) &&
            !string.Equals(typeSymbol.DefiningFilePath, _context.SourceFilePath, StringComparison.OrdinalIgnoreCase))
        {
            var moduleNamespace = GetModuleNameFromFilePath(typeSymbol.DefiningFilePath);
            var typeName = NameMangler.ToPascalCase(sharpyTypeName);

            return BuildQualifiedTypeName(moduleNamespace, typeName);
        }

        // Check if type is from an external module (imported via DefiningModule)
        if (!string.IsNullOrEmpty(typeSymbol.DefiningModule))
        {
            var moduleNamespace = ConvertModuleToNamespace(typeSymbol.DefiningModule);
            var typeName = NameMangler.ToPascalCase(sharpyTypeName);

            return BuildQualifiedTypeName(moduleNamespace, typeName);
        }

        // Type is in current file - use simple name
        return NameMangler.ToPascalCase(sharpyTypeName);
    }

    /// <summary>
    /// Builds a fully qualified type name, handling collision cases where the type IS
    /// the module class (e.g., animal.spy defining class Animal).
    /// </summary>
    private string BuildQualifiedTypeName(string moduleNamespace, string typeName)
    {
        // Check for collision: when the module name matches the type name,
        // the type IS the module class, not nested inside it.
        var lastSegment = moduleNamespace.Contains('.')
            ? moduleNamespace.Split('.').Last()
            : moduleNamespace;

        if (string.Equals(lastSegment, typeName, StringComparison.Ordinal))
        {
            // Type IS the module class — module path is the type path
            if (!string.IsNullOrEmpty(_context.ProjectNamespace))
            {
                return $"{_context.ProjectNamespace}.{moduleNamespace}";
            }
            return moduleNamespace;
        }

        // Type is nested inside the module class
        if (!string.IsNullOrEmpty(_context.ProjectNamespace))
        {
            return $"{_context.ProjectNamespace}.{moduleNamespace}.{typeName}";
        }
        return $"{moduleNamespace}.{typeName}";
    }

    /// <summary>
    /// Derives a module namespace from a file path, computing the full package path
    /// relative to the project root.
    /// E.g., for project root "/temp" and file "/temp/mypackage/submodule.spy" -> "Mypackage.Submodule"
    /// </summary>
    private string GetModuleNameFromFilePath(string filePath)
    {
        // If we have a project root, compute relative path for proper namespace
        if (!string.IsNullOrEmpty(_context.ProjectRootPath))
        {
            var relativePath = Path.GetRelativePath(_context.ProjectRootPath, filePath);
            var relativeDir = Path.GetDirectoryName(relativePath) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            var namespaceParts = new List<string>();

            // Add directory parts (package hierarchy)
            if (!string.IsNullOrEmpty(relativeDir) && relativeDir != ".")
            {
                var dirParts = relativeDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (var part in dirParts)
                {
                    if (!string.IsNullOrEmpty(part) && part != ".")
                    {
                        namespaceParts.Add(NameMangler.ToPascalCase(part));
                    }
                }
            }

            // Add file name part (skip __init__ as it represents the package itself)
            if (!string.Equals(fileName, DunderNames.Init, StringComparison.OrdinalIgnoreCase))
            {
                namespaceParts.Add(NameMangler.ToPascalCase(fileName));
            }

            if (namespaceParts.Count > 0)
            {
                return string.Join(".", namespaceParts);
            }
        }

        // Fallback: just use file name
        var fallbackFileName = Path.GetFileNameWithoutExtension(filePath);
        return NameMangler.ToPascalCase(fallbackFileName);
    }

    /// <summary>
    /// Converts a module path (e.g., "animal" or "lib.animal") to a C# namespace segment.
    /// </summary>
    private static string ConvertModuleToNamespace(string modulePath)
    {
        var parts = modulePath.Split('.');
        return string.Join(".", parts.Select(p => NameMangler.ToPascalCase(p)));
    }

    // ============================================================
    // Helper: Single-evaluation capture for complex expressions
    // ============================================================

    /// <summary>
    /// Returns true if the AST expression is side-effect-free (safe to evaluate multiple times).
    /// Simple identifiers, self, and literals are safe; everything else may have side effects.
    /// </summary>
    private static bool IsSideEffectFree(Expression expr)
        => expr is Parser.Ast.Identifier or NoneLiteral or BooleanLiteral or IntegerLiteral
                 or FloatLiteral or StringLiteral or SuperExpression;

    /// <summary>
    /// Ensures an expression is only evaluated once. For simple identifiers, returns the
    /// expression as-is. For complex expressions, captures the value using an inline
    /// <c>is var</c> pattern: <c>expr is var __temp &amp;&amp; __temp.Check ? __temp.Access : default</c>.
    /// Returns the safe-to-reuse expression and an optional capture condition to prepend.
    /// </summary>
    private (ExpressionSyntax SafeExpr, ExpressionSyntax? CaptureCondition) EnsureSingleEvaluation(
        ExpressionSyntax generated, Expression astExpr)
    {
        if (IsSideEffectFree(astExpr))
            return (generated, null);

        var tempName = GenerateTempVarName("opt");
        var tempIdent = IdentifierName(tempName);
        var capture = IsPatternExpression(
            generated,
            VarPattern(SingleVariableDesignation(Identifier(tempName))));
        return (tempIdent, capture);
    }

    // ============================================================
    // Tagged Union Constructor Generation (Some/Ok/Err)
    // ============================================================

    /// <summary>
    /// Generates code for a tagged union constructor call (Some, Ok, Err).
    /// Some(v) generates Optional&lt;T&gt;.Some(v).
    /// Ok(v)/Err(e) generate Result&lt;T,E&gt;.Ok(v)/Err(e).
    /// </summary>
    private ExpressionSyntax GenerateTaggedUnionConstructor(FunctionCall call)
    {
        var id = (Identifier)call.Function;
        var exprType = GetExpressionSemanticType(call)!;

        return (id.Name, exprType) switch
        {
            ("Some", OptionalType opt) => GenerateSomeExpression(call, opt),
            ("Ok", ResultType res) => GenerateOkExpression(call, res),
            ("Err", ResultType res) => GenerateErrExpression(call, res),
            _ => throw new InvalidOperationException($"Unexpected tagged union constructor: {id.Name}")
        };
    }

    /// <summary>
    /// Generates: Optional&lt;T&gt;.None (static property access)
    /// </summary>
    private ExpressionSyntax GenerateOptionalNone(OptionalType opt)
    {
        var underlyingType = _typeMapper.MapSemanticType(opt.UnderlyingType);

        return MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            GenericName("Optional")
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(underlyingType))),
            IdentifierName("None"));
    }

    /// <summary>
    /// Generates: Optional&lt;T&gt;.Some(value)
    /// </summary>
    private ExpressionSyntax GenerateSomeExpression(FunctionCall call, OptionalType opt)
    {
        var underlyingType = _typeMapper.MapSemanticType(opt.UnderlyingType);
        var arg = GenerateExpression(call.Arguments[0]);

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                GenericName("Optional")
                    .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(underlyingType))),
                IdentifierName("Some")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(arg))));
    }

    /// <summary>
    /// Generates: Result&lt;T, E&gt;.Ok(value)
    /// </summary>
    private ExpressionSyntax GenerateOkExpression(FunctionCall call, ResultType res)
    {
        var okType = _typeMapper.MapSemanticType(res.OkType);
        var errType = _typeMapper.MapSemanticType(res.ErrorType);
        var arg = GenerateExpression(call.Arguments[0]);

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                GenericName("Result")
                    .WithTypeArgumentList(TypeArgumentList(SeparatedList(new[] { okType, errType }))),
                IdentifierName("Ok")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(arg))));
    }

    /// <summary>
    /// Generates: Result&lt;T, E&gt;.Err(error)
    /// </summary>
    private ExpressionSyntax GenerateErrExpression(FunctionCall call, ResultType res)
    {
        var okType = _typeMapper.MapSemanticType(res.OkType);
        var errType = _typeMapper.MapSemanticType(res.ErrorType);
        var arg = GenerateExpression(call.Arguments[0]);

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                GenericName("Result")
                    .WithTypeArgumentList(TypeArgumentList(SeparatedList(new[] { okType, errType }))),
                IdentifierName("Err")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(arg))));
    }

    /// <summary>
    /// Checks whether a method call on an object expression targets a default interface method
    /// that the concrete class doesn't override. Returns the mangled C# interface name if so,
    /// or null if the method is defined directly on the class.
    /// </summary>
    private string? TryGetDefaultMethodInterface(Parser.Ast.Expression objectExpr, string methodName)
    {
        var objType = GetExpressionSemanticType(objectExpr);
        if (objType is not UserDefinedType udt || udt.Symbol == null)
            return null;

        var typeSymbol = udt.Symbol;

        // Only applies to class types (not interfaces, enums, structs)
        if (typeSymbol.TypeKind != Semantic.TypeKind.Class)
            return null;

        // Check if the class itself defines this method (including inherited concrete methods)
        if (HasMethodDefined(typeSymbol, methodName))
            return null;

        // Search interfaces for a default method with this name
        foreach (var ifaceRef in typeSymbol.Interfaces)
        {
            if (HasDefaultMethod(ifaceRef.Definition, methodName))
                return NameMangler.Transform(ifaceRef.Definition.Name, NameContext.Interface);
        }

        return null;
    }

    /// <summary>
    /// Checks whether a type (or its base classes) defines a method with the given name.
    /// </summary>
    private static bool HasMethodDefined(TypeSymbol typeSymbol, string methodName)
    {
        var current = typeSymbol;
        while (current != null)
        {
            if (current.Methods.Any(m => m.Name == methodName))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Checks whether an interface has a default (non-abstract) method with the given name.
    /// Uses symbol metadata (IsAbstract) rather than inspecting AST body shape.
    /// </summary>
    private static bool HasDefaultMethod(TypeSymbol interfaceSymbol, string methodName)
    {
        return interfaceSymbol.Methods.Any(m => m.Name == methodName && !m.IsAbstract);
    }

    /// <summary>
    /// Returns the fully-qualified C# type name for builtin collection types
    /// (list, dict, set), or null for non-builtin types.
    /// </summary>
    private static string? GetBuiltinCollectionTypeName(string sharpyName) => sharpyName switch
    {
        "list" => CSharpTypeNames.SharpyList,
        "dict" => CSharpTypeNames.SharpyDict,
        "set" => CSharpTypeNames.SharpySet,
        _ => null
    };
}
