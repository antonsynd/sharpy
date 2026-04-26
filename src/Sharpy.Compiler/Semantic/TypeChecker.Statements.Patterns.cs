using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Pattern matching and related helpers
/// </summary>
internal partial class TypeChecker
{
    /// <summary>
    /// Extracts the TypeSymbol from a SemanticType, handling UserDefinedType,
    /// NullableType, OptionalType, and GenericType wrappers.
    /// </summary>
    private TypeSymbol? GetTypeSymbolFromSemanticType(SemanticType type)
    {
        return type switch
        {
            UserDefinedType udt => udt.Symbol,
            NullableType nullable => GetTypeSymbolFromSemanticType(nullable.UnderlyingType),
            OptionalType optional => GetTypeSymbolFromSemanticType(optional.UnderlyingType),
            GenericType gt => _symbolTable.Lookup(gt.Name) as TypeSymbol,
            _ => null
        };
    }

    private void CheckMatch(MatchStatement matchStmt)
    {
        var scrutineeType = CheckExpression(matchStmt.Scrutinee);

        foreach (var matchCase in matchStmt.Cases)
        {
            using (_narrowingContext.EnterScope())
            {
                _symbolTable.EnterScope("match-case");
                _controlFlowDepth++;

                CheckPattern(matchCase.Pattern, scrutineeType);

                if (matchCase.Guard != null)
                {
                    var guardType = CheckExpression(matchCase.Guard);
                    if (!IsTruthTestable(guardType))
                    {
                        AddError("Guard condition must be a boolean expression",
                            matchCase.Guard.LineStart, matchCase.Guard.ColumnStart,
                            code: DiagnosticCodes.Semantic.ConditionNotBoolean,
                            span: matchCase.Guard.Span);
                    }
                }

                foreach (var stmt in matchCase.Body)
                    CheckStatement(stmt);

                _controlFlowDepth--;
                _symbolTable.ExitScope();
            }
        }
    }

    private void CheckPattern(Pattern pattern, SemanticType scrutineeType)
    {
        switch (pattern)
        {
            case WildcardPattern:
                break;

            case BindingPattern binding:
                {
                    // RFC 3535: Check if the identifier resolves to a module-level
                    // constant (Final-annotated or IsConstant) before treating as capture.
                    var existingSymbol = _symbolTable.Lookup(binding.Name.Name, searchParents: true) as VariableSymbol;
                    if (existingSymbol is { IsConstant: true })
                    {
                        _diagnostics.AddWarning(
                            $"Pattern '{binding.Name.Name}' matches constant value, not a capture binding; use a different name to capture",
                            binding,
                            code: DiagnosticCodes.Validation.ConstantPatternShadow);

                        _semanticInfo.SetPatternConstantSymbol(binding, existingSymbol);
                        _semanticInfo.SetIdentifierSymbol(binding.Name, existingSymbol);

                        var constType = existingSymbol.Type;
                        if (constType != SemanticType.Unknown && !IsAssignable(scrutineeType, constType))
                        {
                            _diagnostics.AddError(
                                $"Constant pattern type '{constType}' is not compatible with match subject type '{scrutineeType}'",
                                binding,
                                code: DiagnosticCodes.Semantic.TypeMismatch);
                        }
                        break;
                    }

                    var newSymbol = new VariableSymbol
                    {
                        Name = binding.Name.Name,
                        Kind = SymbolKind.Variable,
                        Type = scrutineeType,
                        IsConstant = false,
                        DeclarationLine = binding.LineStart,
                        DeclarationColumn = binding.ColumnStart,
                        NameDeclarationLine = binding.Name.LineStart,
                        NameDeclarationColumn = binding.Name.ColumnStart,
                        AccessLevel = AccessLevel.Public
                    };

                    _symbolTable.Define(newSymbol);
                    SemanticBinding.SetVariableType(newSymbol, scrutineeType);
                    _semanticInfo.SetIdentifierSymbol(binding.Name, newSymbol);
                    break;
                }

            case LiteralPattern literal:
                {
                    // Handle None() pattern when matching against Optional[T]
                    if (literal.Literal is FunctionCall { Function: NoneLiteral } noneCall
                        && noneCall.Arguments.Length == 0
                        && scrutineeType is OptionalType)
                    {
                        // Record synthetic None union case for exhaustiveness checking
                        var synth = GetSyntheticOptionalUnion();
                        var noneCase = synth.UnionCases.First(c => c.Name == "None");
                        _semanticInfo.SetPatternUnionCase(literal, noneCase);
                        break;
                    }

                    // Handle bare None literal when matching against Optional[T]
                    if (literal.Literal is NoneLiteral && scrutineeType is OptionalType)
                    {
                        var synth = GetSyntheticOptionalUnion();
                        var noneCase = synth.UnionCases.First(c => c.Name == "None");
                        _semanticInfo.SetPatternUnionCase(literal, noneCase);
                        break;
                    }

                    var litType = CheckExpression(literal.Literal);
                    if (!IsAssignable(litType, scrutineeType) && !IsAssignable(scrutineeType, litType))
                    {
                        AddError(
                            $"Pattern type '{litType.GetDisplayName()}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                            literal.LineStart, literal.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: literal.Span);
                    }
                    break;
                }

            case TuplePattern tuplePattern:
                {
                    if (scrutineeType is TupleType tupleType)
                    {
                        if (tuplePattern.Elements.Length != tupleType.ElementTypes.Count)
                        {
                            AddError(
                                $"Tuple pattern has {tuplePattern.Elements.Length} elements but scrutinee has {tupleType.ElementTypes.Count}",
                                tuplePattern.LineStart, tuplePattern.ColumnStart,
                                code: DiagnosticCodes.Semantic.TuplePatternLengthMismatch,
                                span: tuplePattern.Span);
                        }
                        else
                        {
                            for (int i = 0; i < tuplePattern.Elements.Length; i++)
                                CheckPattern(tuplePattern.Elements[i], tupleType.ElementTypes[i]);
                        }
                    }
                    else
                    {
                        AddError(
                            $"Cannot destructure non-tuple type '{scrutineeType.GetDisplayName()}' with tuple pattern",
                            tuplePattern.LineStart, tuplePattern.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: tuplePattern.Span);
                    }
                    break;
                }

            case TypePattern typePattern:
                CheckTypePattern(typePattern, scrutineeType);
                break;

            case RelationalPattern relational:
                {
                    var valueType = CheckExpression(relational.Value);
                    if (!TypeUtils.IsNumericOrUnknown(scrutineeType))
                    {
                        AddError(
                            $"Relational patterns require a numeric scrutinee type, got '{scrutineeType.GetDisplayName()}'",
                            relational.LineStart, relational.ColumnStart,
                            code: DiagnosticCodes.Semantic.RelationalPatternTypeMismatch,
                            span: relational.Span);
                    }
                    if (!IsAssignable(valueType, scrutineeType) && !IsAssignable(scrutineeType, valueType)
                        && valueType is not UnknownType)
                    {
                        AddError(
                            $"Pattern value type '{valueType.GetDisplayName()}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                            relational.LineStart, relational.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: relational.Span);
                    }
                    break;
                }

            case OrPattern orPattern:
                {
                    bool hasMemberAccess = orPattern.Alternatives.Any(a => a is MemberAccessPattern);
                    foreach (var alt in orPattern.Alternatives)
                    {
                        // GuardPattern wraps an inner pattern — check the inner pattern normally
                        var effectiveAlt = alt is GuardPattern gp ? gp.Inner : alt;
                        if (effectiveAlt is BindingPattern)
                        {
                            AddError(
                                "Binding patterns are not allowed inside or-patterns",
                                effectiveAlt.LineStart, effectiveAlt.ColumnStart,
                                code: DiagnosticCodes.Semantic.BindingInOrPattern,
                                span: effectiveAlt.Span);
                        }
                        else if (hasMemberAccess && effectiveAlt is not MemberAccessPattern && effectiveAlt is not LiteralPattern && effectiveAlt is not WildcardPattern)
                        {
                            AddError(
                                "Only literal, member access, and wildcard patterns can be combined with member access patterns in or-patterns",
                                effectiveAlt.LineStart, effectiveAlt.ColumnStart,
                                code: DiagnosticCodes.Semantic.UnsupportedPatternInMemberAccessOr,
                                span: effectiveAlt.Span);
                        }
                        else
                        {
                            CheckPattern(alt, scrutineeType);
                        }
                    }
                    break;
                }

            case GuardPattern guardPattern:
                {
                    CheckPattern(guardPattern.Inner, scrutineeType);
                    var guardType = CheckExpression(guardPattern.Guard);
                    if (guardType != SemanticType.Bool && guardType != SemanticType.Unknown)
                    {
                        AddError(
                            $"Guard expression must be bool, got '{guardType.GetDisplayName()}'",
                            guardPattern.Guard.LineStart, guardPattern.Guard.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: guardPattern.Guard.Span);
                    }
                    break;
                }

            case PropertyPattern propertyPattern:
                CheckPropertyPattern(propertyPattern, scrutineeType);
                break;

            case PositionalPattern positionalPattern:
                CheckPositionalPattern(positionalPattern, scrutineeType);
                break;

            case MemberAccessPattern memberAccess:
                CheckMemberAccessPattern(memberAccess, scrutineeType);
                break;

            default:
                AddError(
                    $"Unsupported pattern type '{pattern.GetType().Name}'. This pattern is not yet implemented.",
                    pattern.LineStart, pattern.ColumnStart,
                    code: DiagnosticCodes.Semantic.UnsupportedFeature);
                break;
        }
    }

    /// <summary>
    /// Check a type pattern: resolve the type, handle union cases, validate compatibility,
    /// and register any binding variable.
    /// </summary>
    private void CheckTypePattern(TypePattern typePattern, SemanticType scrutineeType)
    {
        var resolvedType = _typeResolver.ResolveTypeAnnotation(typePattern.Type);
        if (resolvedType is UnknownType)
        {
            // Try to resolve as a union case (e.g., case Point(): when matching Shape)
            var unionCaseSymbol = TryResolveUnionCaseFromPattern(
                typePattern.Type.Name, scrutineeType);
            if (unionCaseSymbol != null)
            {
                _semanticInfo.SetPatternUnionCase(typePattern, unionCaseSymbol);
                resolvedType = new UserDefinedType { Name = unionCaseSymbol.Name, Symbol = unionCaseSymbol };
            }
            else
            {
                AddError(
                    $"Unknown type '{typePattern.Type.Name}' in type pattern",
                    typePattern.LineStart, typePattern.ColumnStart,
                    code: DiagnosticCodes.Semantic.UndefinedType,
                    span: typePattern.Span);
            }
        }
        else if (scrutineeType is not UnknownType
            && !IsAssignable(resolvedType, scrutineeType)
            && !IsAssignable(scrutineeType, resolvedType))
        {
            AddError(
                $"Type pattern '{resolvedType.GetDisplayName()}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                typePattern.LineStart, typePattern.ColumnStart,
                code: DiagnosticCodes.Semantic.TypePatternIncompatible,
                span: typePattern.Span);
        }
        if (typePattern.BindingName != null)
        {
            var newSymbol = new VariableSymbol
            {
                Name = typePattern.BindingName.Name,
                Kind = SymbolKind.Variable,
                Type = resolvedType,
                IsConstant = false,
                DeclarationLine = typePattern.BindingName.LineStart,
                DeclarationColumn = typePattern.BindingName.ColumnStart,
                NameDeclarationLine = typePattern.BindingName.LineStart,
                NameDeclarationColumn = typePattern.BindingName.ColumnStart,
                AccessLevel = AccessLevel.Public
            };
            _symbolTable.Define(newSymbol);
            SemanticBinding.SetVariableType(newSymbol, resolvedType);
            _semanticInfo.SetIdentifierSymbol(typePattern.BindingName, newSymbol);
        }
    }

    /// <summary>
    /// Check a property pattern: resolve the type, then validate each field sub-pattern.
    /// </summary>
    private void CheckPropertyPattern(PropertyPattern propertyPattern, SemanticType scrutineeType)
    {
        TypeSymbol? typeSymbol = null;
        if (propertyPattern.Type != null)
        {
            var resolvedType = _typeResolver.ResolveTypeAnnotation(propertyPattern.Type);
            if (resolvedType is UnknownType)
            {
                AddError(
                    $"Unknown type '{propertyPattern.Type.Name}' in property pattern",
                    propertyPattern.LineStart, propertyPattern.ColumnStart,
                    code: DiagnosticCodes.Semantic.UndefinedType,
                    span: propertyPattern.Span);
            }
            else if (resolvedType is UserDefinedType udt)
            {
                typeSymbol = udt.Symbol;
            }
        }

        foreach (var field in propertyPattern.Fields)
        {
            if (typeSymbol != null)
            {
                var fieldSymbol = typeSymbol.Fields.FirstOrDefault(f => f.Name == field.Name);
                if (fieldSymbol == null)
                {
                    AddError(
                        $"Type '{typeSymbol.Name}' has no field '{field.Name}'",
                        field.LineStart, field.ColumnStart,
                        code: DiagnosticCodes.Semantic.PropertyPatternUnknownField,
                        span: field.Span);
                }
                else
                {
                    CheckPattern(field.Pattern, fieldSymbol.Type);
                }
            }
            else
            {
                CheckPattern(field.Pattern, scrutineeType);
            }
        }
    }

    /// <summary>
    /// Check a positional pattern: resolve the type (including union cases),
    /// validate deconstruction support, and check element sub-patterns.
    /// </summary>
    private void CheckPositionalPattern(PositionalPattern positionalPattern, SemanticType scrutineeType)
    {
        TypeSymbol? typeSymbol = null;
        if (positionalPattern.Type != null)
        {
            // Try to resolve as a union case first when scrutinee is a union type
            var unionCaseSymbol = TryResolveUnionCaseFromPattern(
                positionalPattern.Type.Name, scrutineeType);

            if (unionCaseSymbol != null)
            {
                typeSymbol = unionCaseSymbol;
                _semanticInfo.SetPatternUnionCase(positionalPattern, unionCaseSymbol);
            }
            else
            {
                var resolvedType = _typeResolver.ResolveTypeAnnotation(positionalPattern.Type);
                if (resolvedType is UnknownType)
                {
                    AddError(
                        $"Unknown type '{positionalPattern.Type.Name}' in positional pattern",
                        positionalPattern.LineStart, positionalPattern.ColumnStart,
                        code: DiagnosticCodes.Semantic.UndefinedType,
                        span: positionalPattern.Span);
                }
                else if (resolvedType is UserDefinedType udt)
                {
                    typeSymbol = udt.Symbol;
                    // For non-union types, check if positional deconstruction is supported
                    if (typeSymbol != null
                        && typeSymbol.BaseType?.TypeKind != TypeKind.Union
                        && typeSymbol.TypeKind != TypeKind.Union)
                    {
                        bool hasDeconstruct = typeSymbol.Methods.Any(m => m.Name == "Deconstruct");
                        bool hasMatchingFields = typeSymbol.Fields.Count == positionalPattern.Elements.Length;
                        if (!hasDeconstruct && !hasMatchingFields)
                        {
                            AddError(
                                $"Type '{typeSymbol.Name}' does not support positional deconstruction (no Deconstruct method and field count {typeSymbol.Fields.Count} does not match pattern element count {positionalPattern.Elements.Length})",
                                positionalPattern.LineStart, positionalPattern.ColumnStart,
                                code: DiagnosticCodes.Semantic.PositionalPatternNoDeconstruct,
                                span: positionalPattern.Span);
                        }
                    }
                }
            }
        }

        if (typeSymbol != null)
        {
            // Get field types, substituting type parameters for generic unions
            var fieldTypes = GetUnionCaseFieldTypes(typeSymbol, scrutineeType);

            if (positionalPattern.Elements.Length != fieldTypes.Count)
            {
                AddError(
                    $"Positional pattern has {positionalPattern.Elements.Length} elements but type '{typeSymbol.Name}' has {fieldTypes.Count} fields",
                    positionalPattern.LineStart, positionalPattern.ColumnStart,
                    code: typeSymbol.BaseType is { TypeKind: TypeKind.Union }
                        ? DiagnosticCodes.Semantic.UnionCaseFieldMismatch
                        : DiagnosticCodes.Semantic.PositionalPatternCountMismatch,
                    span: positionalPattern.Span);
            }
            else
            {
                for (int i = 0; i < positionalPattern.Elements.Length; i++)
                {
                    CheckPattern(positionalPattern.Elements[i], fieldTypes[i]);
                }
            }
        }
        else
        {
            foreach (var element in positionalPattern.Elements)
            {
                CheckPattern(element, scrutineeType);
            }
        }
    }

    /// <summary>
    /// Check a member access pattern: resolve dotted paths for enum members,
    /// union cases, and field/property access chains.
    /// </summary>
    private void CheckMemberAccessPattern(MemberAccessPattern memberAccess, SemanticType scrutineeType)
    {
        // Resolve the dotted path as a member access (e.g., Color.RED).
        // Look up the first part as a type, then resolve subsequent parts as fields/members.
        var typeName = memberAccess.Parts[0];
        var typeSymbol = _symbolTable.Lookup(typeName) as TypeSymbol;
        if (typeSymbol == null)
        {
            AddError(
                $"Undefined type '{typeName}' in pattern",
                memberAccess.LineStart, memberAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedType,
                span: memberAccess.Span);
            return;
        }

        // Check if this is a union case pattern (e.g., Option.None, Result.Ok)
        if (typeSymbol.TypeKind == TypeKind.Union && memberAccess.Parts.Length == 2)
        {
            var caseName = memberAccess.Parts[1];
            var caseSymbol = typeSymbol.UnionCases.FirstOrDefault(c => c.Name == caseName);
            if (caseSymbol != null)
            {
                _semanticInfo.SetPatternUnionCase(memberAccess, caseSymbol);
                return;
            }
            else
            {
                AddError(
                    $"Union '{typeSymbol.Name}' has no case '{caseName}'",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.UnionCaseNotFound,
                    span: memberAccess.Span);
                return;
            }
        }

        // Check if this is an enum member pattern (e.g., Color.RED)
        if (typeSymbol.TypeKind == TypeKind.Enum && memberAccess.Parts.Length == 2)
        {
            var memberName = memberAccess.Parts[1];
            var enumField = typeSymbol.Fields.FirstOrDefault(f => f.Name == memberName);
            if (enumField != null)
            {
                // Verify the enum type matches the scrutinee type
                if (scrutineeType is UserDefinedType udt && udt.Symbol == typeSymbol)
                {
                    // Valid enum member pattern matching the scrutinee
                    return;
                }
                else
                {
                    AddError(
                        $"Enum member '{typeName}.{memberName}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                        memberAccess.LineStart, memberAccess.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: memberAccess.Span);
                    return;
                }
            }
            else
            {
                AddError(
                    $"Enum '{typeSymbol.Name}' has no member '{memberName}'",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.UndefinedMember,
                    span: memberAccess.Span);
                return;
            }
        }

        // Resolve remaining parts as field or property access
        SemanticType? resolvedType = null;
        for (int i = 1; i < memberAccess.Parts.Length; i++)
        {
            var fieldName = memberAccess.Parts[i];
            var field = typeSymbol.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (field != null)
            {
                resolvedType = field.Type;
            }
            else
            {
                var prop = typeSymbol.Properties.FirstOrDefault(p => p.Name == fieldName);
                if (prop != null)
                {
                    resolvedType = prop.Type;
                }
                else
                {
                    AddError(
                        $"Type '{typeName}' has no member '{fieldName}'",
                        memberAccess.LineStart, memberAccess.ColumnStart,
                        code: DiagnosticCodes.Semantic.UndefinedMember,
                        span: memberAccess.Span);
                    return;
                }
            }
        }

        if (resolvedType != null && !IsAssignable(resolvedType, scrutineeType) && !IsAssignable(scrutineeType, resolvedType))
        {
            AddError(
                $"Pattern type '{resolvedType.GetDisplayName()}' is incompatible with scrutinee type '{scrutineeType.GetDisplayName()}'",
                memberAccess.LineStart, memberAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.TypeMismatch,
                span: memberAccess.Span);
        }
    }

    /// <summary>
    /// Tries to resolve a pattern type name as a union case of the scrutinee type.
    /// Supports both short form (e.g., "Ok" when scrutinee is Result) and
    /// long form (e.g., "Result.Ok" via dotted name in TypeAnnotation).
    /// Returns the union case TypeSymbol if found, or null otherwise.
    /// </summary>
    private TypeSymbol? TryResolveUnionCaseFromPattern(string typeName, SemanticType scrutineeType)
    {
        var (unionSymbol, _) = GetUnionSymbolAndTypeArgs(scrutineeType);
        if (unionSymbol == null)
            return null;

        // Short form: name matches a union case directly (e.g., "Ok" for Result union)
        var caseSymbol = unionSymbol.UnionCases.FirstOrDefault(c => c.Name == typeName);
        if (caseSymbol != null)
            return caseSymbol;

        // Long form: "UnionName.CaseName" — the TypeAnnotation name includes the dot
        if (typeName.Contains('.', StringComparison.Ordinal))
        {
            var parts = typeName.Split('.');
            if (parts.Length == 2 && parts[0] == unionSymbol.Name)
            {
                return unionSymbol.UnionCases.FirstOrDefault(c => c.Name == parts[1]);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the field types for a type symbol, applying generic type substitution
    /// when the type is a union case with a generic parent union.
    /// </summary>
    private List<SemanticType> GetUnionCaseFieldTypes(TypeSymbol typeSymbol, SemanticType scrutineeType)
    {
        var fieldTypes = typeSymbol.Fields.Select(f => f.Type).ToList();

        // If this is a union case, substitute type parameters from the scrutinee
        if (typeSymbol.BaseType is { TypeKind: TypeKind.Union } unionParent
            && unionParent.TypeParameters.Count > 0)
        {
            var (_, typeArgs) = GetUnionSymbolAndTypeArgs(scrutineeType);
            if (typeArgs != null && typeArgs.Count == unionParent.TypeParameters.Count)
            {
                for (int i = 0; i < fieldTypes.Count; i++)
                {
                    fieldTypes[i] = SubstituteTypeParameters(
                        fieldTypes[i], unionParent.TypeParameters, typeArgs);
                }
            }
        }

        return fieldTypes;
    }

    /// <summary>
    /// Extracts the union TypeSymbol and type arguments from a scrutinee type.
    /// Handles both UserDefinedType (non-generic unions) and GenericType (generic unions).
    /// </summary>
    private (TypeSymbol? UnionSymbol, List<SemanticType>? TypeArgs) GetUnionSymbolAndTypeArgs(
        SemanticType scrutineeType)
    {
        if (scrutineeType is UserDefinedType udt
            && udt.Symbol?.TypeKind == TypeKind.Union)
        {
            return (udt.Symbol, null);
        }

        if (scrutineeType is GenericType gt
            && gt.GenericDefinition?.TypeKind == TypeKind.Union)
        {
            return (gt.GenericDefinition, gt.TypeArguments);
        }

        // OptionalType -> synthetic union with Some(T) and None() cases
        if (scrutineeType is OptionalType optionalType)
        {
            var synth = GetSyntheticOptionalUnion();
            return (synth, new List<SemanticType> { optionalType.UnderlyingType });
        }

        // ResultType -> synthetic union with Ok(T) and Err(E) cases
        if (scrutineeType is ResultType resultType)
        {
            var synth = GetSyntheticResultUnion();
            return (synth, new List<SemanticType> { resultType.OkType, resultType.ErrorType });
        }

        return (null, null);
    }

    private TypeSymbol? _syntheticOptionalUnion;
    private TypeSymbol? _syntheticResultUnion;

    /// <summary>
    /// Returns a synthetic union TypeSymbol for Optional[T] with cases Some(T) and None().
    /// The type parameter T is substituted at pattern-check time via GetUnionCaseFieldTypes.
    /// </summary>
    private TypeSymbol GetSyntheticOptionalUnion()
    {
        if (_syntheticOptionalUnion != null)
            return _syntheticOptionalUnion;

        var tParam = new TypeParameterType { Name = "T" };

        var someCase = new TypeSymbol
        {
            Name = "Some",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            Fields = new List<VariableSymbol>
            {
                new() { Name = "value", Kind = SymbolKind.Variable, Type = tParam, AccessLevel = AccessLevel.Public }
            }
        };

        var noneCase = new TypeSymbol
        {
            Name = "None",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            Fields = new List<VariableSymbol>()
        };

        var optionalUnion = new TypeSymbol
        {
            Name = "Optional",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Union,
            AccessLevel = AccessLevel.Public,
            TypeParameters = new List<TypeParameterDef>
            {
                new() { Name = "T" }
            },
            UnionCases = new List<TypeSymbol> { someCase, noneCase }
        };

        someCase.BaseType = optionalUnion;
        noneCase.BaseType = optionalUnion;

        _syntheticOptionalUnion = optionalUnion;
        return optionalUnion;
    }

    /// <summary>
    /// Returns a synthetic union TypeSymbol for Result[T, E] with cases Ok(T) and Err(E).
    /// The type parameters T and E are substituted at pattern-check time via GetUnionCaseFieldTypes.
    /// </summary>
    private TypeSymbol GetSyntheticResultUnion()
    {
        if (_syntheticResultUnion != null)
            return _syntheticResultUnion;

        var tParam = new TypeParameterType { Name = "T" };
        var eParam = new TypeParameterType { Name = "E" };

        var okCase = new TypeSymbol
        {
            Name = "Ok",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            Fields = new List<VariableSymbol>
            {
                new() { Name = "value", Kind = SymbolKind.Variable, Type = tParam, AccessLevel = AccessLevel.Public }
            }
        };

        var errCase = new TypeSymbol
        {
            Name = "Err",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            AccessLevel = AccessLevel.Public,
            Fields = new List<VariableSymbol>
            {
                new() { Name = "error", Kind = SymbolKind.Variable, Type = eParam, AccessLevel = AccessLevel.Public }
            }
        };

        var resultUnion = new TypeSymbol
        {
            Name = "Result",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Union,
            AccessLevel = AccessLevel.Public,
            TypeParameters = new List<TypeParameterDef>
            {
                new() { Name = "T" },
                new() { Name = "E" }
            },
            UnionCases = new List<TypeSymbol> { okCase, errCase }
        };

        okCase.BaseType = resultUnion;
        errCase.BaseType = resultUnion;

        _syntheticResultUnion = resultUnion;
        return resultUnion;
    }

    /// <summary>
    /// Recursively type-checks tuple unpacking target elements against their value types.
    /// Handles nested tuple targets like (a, b), c and (a, (b, c)), d.
    /// </summary>
    private void CheckTupleUnpackingElements(ImmutableArray<Expression> targets, IReadOnlyList<SemanticType> valueTypes)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            var targetElem = targets[i];
            var valueElemType = valueTypes[i];

            if (targetElem is Identifier tupleTargetId)
            {
                var existingSymbol = _symbolTable.Lookup(tupleTargetId.Name, searchParents: false);

                // Check if trying to reassign a constant
                if (existingSymbol is VariableSymbol varSymbol && varSymbol.IsConstant)
                {
                    AddError($"Cannot reassign constant variable '{tupleTargetId.Name}' in tuple unpacking",
                        tupleTargetId.LineStart, tupleTargetId.ColumnStart, code: DiagnosticCodes.Semantic.InvalidAssignmentTarget,
                        span: tupleTargetId.Span);
                    continue;
                }

                // In Sharpy, tuple unpacking creates new variable versions
                // Create/redefine with inferred type from tuple element
                var newSymbol = new VariableSymbol
                {
                    Name = tupleTargetId.Name,
                    Kind = SymbolKind.Variable,
                    Type = valueElemType,
                    IsConstant = false,
                    DeclarationLine = tupleTargetId.LineStart,
                    DeclarationColumn = tupleTargetId.ColumnStart,
                    NameDeclarationLine = tupleTargetId.LineStart,
                    NameDeclarationColumn = tupleTargetId.ColumnStart,
                    AccessLevel = AccessLevel.Public
                };
                _symbolTable.Define(newSymbol);
                SemanticBinding.SetVariableType(newSymbol, valueElemType);
                _semanticInfo.SetIdentifierSymbol(tupleTargetId, newSymbol);
                _semanticInfo.SetExpressionType(tupleTargetId, valueElemType);
                if (valueElemType is UnknownType)
                    MarkExpressionAsErrorRecovery(tupleTargetId);
            }
            else if (targetElem is TupleLiteral nestedTuple)
            {
                // Nested tuple unpacking: (a, b), c = expr
                if (valueElemType is not TupleType nestedTupleType)
                {
                    AddError($"Cannot unpack non-tuple type '{valueElemType.GetDisplayName()}' into nested tuple",
                        targetElem.LineStart, targetElem.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                        span: targetElem.Span);
                    continue;
                }

                if (nestedTuple.Elements.Length != nestedTupleType.ElementTypes.Count)
                {
                    AddError($"Cannot unpack {nestedTupleType.ElementTypes.Count} values into {nestedTuple.Elements.Length} variables",
                        targetElem.LineStart, targetElem.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                        span: targetElem.Span);
                    continue;
                }

                // Recurse into nested tuple
                CheckTupleUnpackingElements(nestedTuple.Elements, nestedTupleType.ElementTypes);
            }
            else
            {
                // For more complex targets (like attributes), just check type compatibility
                var targetElemType = CheckExpression(targetElem);
                if (!IsAssignable(valueElemType, targetElemType))
                {
                    AddError($"Cannot assign type '{valueElemType.GetDisplayName()}' to '{targetElemType.GetDisplayName()}' in tuple unpacking",
                        targetElem.LineStart, targetElem.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: targetElem.Span);
                }
            }
        }
    }

    /// <summary>
    /// Type-checks star unpacking patterns: first, *rest = items
    /// The RHS can be a list[T] or tuple[...].
    /// </summary>
    private void CheckStarUnpacking(TupleLiteral targetTuple, SemanticType valueType, Assignment assignment)
    {
        // Validate only one star expression
        int starCount = targetTuple.Elements.Count(e => e is StarExpression);
        if (starCount > 1)
        {
            AddError("Only one starred expression is allowed in an unpacking assignment",
                assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.MultipleStarExpressions,
                span: assignment.Span);
            return;
        }

        // Determine element type from the source
        SemanticType elementType;
        if (valueType is GenericType { Name: BuiltinNames.List } listType && listType.TypeArguments.Count > 0)
        {
            elementType = listType.TypeArguments[0];
        }
        else if (valueType is TupleType tupleType)
        {
            // For tuples, compute the starred variable's element type from the rest elements
            int starIdx = targetTuple.Elements.ToList().FindIndex(e => e is StarExpression);
            int nBefore = starIdx;
            int nAfter = targetTuple.Elements.Length - starIdx - 1;
            int tupleArity = tupleType.ElementTypes.Count;

            // Collect the types of elements that go into the rest variable
            var restTypes = new List<SemanticType>();
            for (int ri = nBefore; ri < tupleArity - nAfter; ri++)
            {
                if (ri >= 0 && ri < tupleArity)
                    restTypes.Add(tupleType.ElementTypes[ri]);
            }

            if (restTypes.Count == 0)
            {
                elementType = tupleType.ElementTypes.Count > 0 ? tupleType.ElementTypes[0] : SemanticType.Unknown;
            }
            else if (restTypes.All(t => t.Equals(restTypes[0])))
            {
                elementType = restTypes[0];
            }
            else
            {
                elementType = BuiltinType.Object;
            }
        }
        else
        {
            AddError($"Cannot use starred unpacking with type '{valueType.GetDisplayName()}'",
                assignment.LineStart, assignment.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                span: assignment.Span);
            return;
        }

        // Define variables for each target
        foreach (var targetElem in targetTuple.Elements)
        {
            if (targetElem is StarExpression starExpr && starExpr.Operand is Identifier starId)
            {
                // Starred variable gets list[T] type
                var listTypeForStar = new GenericType
                {
                    Name = BuiltinNames.List,
                    TypeArguments = new List<SemanticType> { elementType }
                };
                var starSymbol = new VariableSymbol
                {
                    Name = starId.Name,
                    Kind = SymbolKind.Variable,
                    Type = listTypeForStar,
                    IsConstant = false,
                    DeclarationLine = starId.LineStart,
                    DeclarationColumn = starId.ColumnStart,
                    NameDeclarationLine = starId.LineStart,
                    NameDeclarationColumn = starId.ColumnStart,
                    AccessLevel = AccessLevel.Public
                };
                _symbolTable.Define(starSymbol);
                SemanticBinding.SetVariableType(starSymbol, listTypeForStar);
                _semanticInfo.SetIdentifierSymbol(starId, starSymbol);
                _semanticInfo.SetExpressionType(starId, listTypeForStar);
                _semanticInfo.SetExpressionType(starExpr, listTypeForStar);
            }
            else if (targetElem is Identifier id)
            {
                var symbol = new VariableSymbol
                {
                    Name = id.Name,
                    Kind = SymbolKind.Variable,
                    Type = elementType,
                    IsConstant = false,
                    DeclarationLine = id.LineStart,
                    DeclarationColumn = id.ColumnStart,
                    NameDeclarationLine = id.LineStart,
                    NameDeclarationColumn = id.ColumnStart,
                    AccessLevel = AccessLevel.Public
                };
                _symbolTable.Define(symbol);
                SemanticBinding.SetVariableType(symbol, elementType);
                _semanticInfo.SetIdentifierSymbol(id, symbol);
                _semanticInfo.SetExpressionType(id, elementType);
            }
        }
    }
}
