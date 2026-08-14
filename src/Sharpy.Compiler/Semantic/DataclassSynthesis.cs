using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// What <c>@dataclass</c> MEANS, in one place: the decorator's options, the ordered field vector it
/// implies, and the four members it synthesizes.
/// </summary>
/// <remarks>
/// <para>
/// Two passes need this answer. The <c>TypeChecker</c> needs it for a class the compilation
/// declares, and <c>ModuleLoader</c> needs it for a class the compilation only IMPORTS — and having
/// it in the checker alone meant an imported <c>@dataclass</c> arrived as an ordinary class:
/// <c>IsDataclass</c> false, no <c>__eq__</c>, no <c>__hash__</c>, no <c>__repr__</c>, so two equal
/// values compared unequal and printing one produced a type name (#1442).
/// </para>
/// <para>
/// The type each field CONTRIBUTES is the one thing the two callers genuinely disagree about, so it
/// is a parameter rather than a branch: the checker reads its own resolved
/// <c>SemanticBinding</c> entry, the extractor reads the annotation it just converted. Everything
/// else — which fields count, in what order, which members are synthesized and with what
/// signatures — is shared, because a difference there would be a difference in what the decorator
/// means.
/// </para>
/// <para>
/// Diagnostics are the caller's, passed in as reporters: an imported module is not compiled, so the
/// extractor must reach the same answer without reporting anything about a file the user is not
/// building. Keeping the reporters optional lets ONE traversal carry both the rule and the
/// complaint rather than splitting them into two that can drift.
/// </para>
/// </remarks>
internal static class DataclassSynthesis
{
    /// <summary>
    /// The <c>@dataclass</c> options for <paramref name="classDef"/>, or null when it carries no
    /// <c>@dataclass</c> decorator. Unrecognized keyword arguments and non-boolean values leave
    /// their option at its default, exactly as before.
    /// </summary>
    public static DataclassOptions? ReadOptions(ClassDef classDef)
    {
        var decorator = classDef.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.Dataclass);
        if (decorator == null)
            return null;

        bool frozen = false;
        bool eq = true;
        bool repr = true;

        foreach (var kwArg in decorator.KeywordArguments)
        {
            if (kwArg.Value is not BooleanLiteral boolLit)
                continue;

            // Option names must match DataclassOptionNames.KnownOptions
            switch (kwArg.Name)
            {
                case DataclassOptionNames.Frozen:
                    frozen = boolLit.Value;
                    break;
                case DataclassOptionNames.Eq:
                    eq = boolLit.Value;
                    break;
                case DataclassOptionNames.Repr:
                    repr = boolLit.Value;
                    break;
            }
        }

        return new DataclassOptions(frozen, eq, repr);
    }

    /// <summary>
    /// The ordered dataclass field vector: an inherited <c>@dataclass</c> base's fields first, then
    /// this class's own non-static annotated fields in declaration order. That order IS the
    /// synthesized constructor's parameter order, so it is a semantic fact rather than a listing
    /// convenience.
    /// </summary>
    /// <param name="onUntypedField">
    /// Reports a field with no type annotation (SPY error at the declaration). Null for the
    /// extraction path, which is reading a module this compilation does not build.
    /// </param>
    /// <param name="onOrderingViolation">Reports a non-default field following a defaulted one.</param>
    public static List<VariableSymbol> CollectFields(
        TypeSymbol classSymbol,
        ClassDef classDef,
        Action<VariableDeclaration>? onUntypedField = null,
        Action<VariableDeclaration>? onOrderingViolation = null)
    {
        var fields = new List<VariableSymbol>();

        if (classSymbol.BaseType is { IsDataclass: true, DataclassFields: { } parentFields })
            fields.AddRange(parentFields);

        bool seenDefault = fields.Any(f => f.HasDefaultValue);

        foreach (var fieldDecl in classDef.Body.OfType<VariableDeclaration>())
        {
            // Static fields are not instance fields, so they are not dataclass fields.
            if (fieldDecl.Decorators.Any(d => d.Name == DecoratorNames.Static))
                continue;

            if (fieldDecl.Type == null)
            {
                onUntypedField?.Invoke(fieldDecl);
                continue;
            }

            bool hasDefault = fieldDecl.InitialValue != null;
            if (!hasDefault && seenDefault)
                onOrderingViolation?.Invoke(fieldDecl);
            if (hasDefault)
                seenDefault = true;

            var fieldSymbol = classSymbol.Fields.FirstOrDefault(f => f.Name == fieldDecl.Name);
            if (fieldSymbol != null)
                fields.Add(fieldSymbol);
        }

        return fields;
    }

    /// <summary>
    /// Adds <c>__init__</c>, <c>__eq__</c>, <c>__hash__</c> and <c>__repr__</c> to
    /// <paramref name="classSymbol"/> where the class does not declare them itself and the options
    /// call for them.
    /// </summary>
    /// <param name="typeOf">
    /// The type a field contributes to the synthesized constructor's parameter list. See the class
    /// remarks: this is the one thing the declaring and importing passes read differently.
    /// </param>
    public static void SynthesizeMembers(
        TypeSymbol classSymbol,
        ClassDef classDef,
        IReadOnlyList<VariableSymbol> fields,
        DataclassOptions options,
        Func<VariableSymbol, SemanticType> typeOf)
    {
        var explicitMethods = classDef.Body.OfType<FunctionDef>().Select(f => f.Name).ToHashSet();

        if (!explicitMethods.Contains(DunderNames.Init))
        {
            var initParams = new List<ParameterSymbol> { SelfParameter(classSymbol) };

            foreach (var field in fields)
            {
                initParams.Add(new ParameterSymbol
                {
                    Name = field.Name,
                    Type = typeOf(field),
                    HasDefault = field.HasDefaultValue,
                });
            }

            var initSymbol = new FunctionSymbol
            {
                Name = DunderNames.Init,
                Kind = SymbolKind.Function,
                ReturnType = SemanticType.Void,
                Parameters = initParams,
                DeclarationLine = classDef.LineStart,
                DeclarationColumn = classDef.ColumnStart,
                NameDeclarationLine = classDef.LineStart,
                NameDeclarationColumn = classDef.ColumnStart,
            };

            classSymbol.Constructors.Add(initSymbol);
            classSymbol.ProtocolMethods[DunderNames.Init] = new List<FunctionSymbol> { initSymbol };
        }

        // __eq__ and __hash__ travel together: .NET requires GetHashCode wherever Equals is
        // overridden, regardless of frozen.
        if (options.Eq && !explicitMethods.Contains(DunderNames.Eq))
        {
            var eqSymbol = new FunctionSymbol
            {
                Name = DunderNames.Eq,
                Kind = SymbolKind.Function,
                ReturnType = SemanticType.Bool,
                Parameters = new List<ParameterSymbol>
                {
                    SelfParameter(classSymbol),
                    new() { Name = "other", Type = SemanticType.Object },
                },
                IsOverride = true,
                DeclarationLine = classDef.LineStart,
                DeclarationColumn = classDef.ColumnStart,
                NameDeclarationLine = classDef.LineStart,
                NameDeclarationColumn = classDef.ColumnStart,
            };

            classSymbol.OperatorMethods[DunderNames.Eq] = new List<FunctionSymbol> { eqSymbol };
            classSymbol.Methods.Add(eqSymbol);
        }

        if (options.Eq && !explicitMethods.Contains(DunderNames.Hash))
        {
            var hashSymbol = new FunctionSymbol
            {
                Name = DunderNames.Hash,
                Kind = SymbolKind.Function,
                ReturnType = SemanticType.Int,
                Parameters = new List<ParameterSymbol> { SelfParameter(classSymbol) },
                IsOverride = true,
                DeclarationLine = classDef.LineStart,
                DeclarationColumn = classDef.ColumnStart,
                NameDeclarationLine = classDef.LineStart,
                NameDeclarationColumn = classDef.ColumnStart,
            };

            classSymbol.ProtocolMethods[DunderNames.Hash] = new List<FunctionSymbol> { hashSymbol };
            classSymbol.Methods.Add(hashSymbol);
        }

        if (options.Repr && !explicitMethods.Contains(DunderNames.Repr))
        {
            var reprSymbol = new FunctionSymbol
            {
                Name = DunderNames.Repr,
                Kind = SymbolKind.Function,
                ReturnType = SemanticType.Str,
                Parameters = new List<ParameterSymbol> { SelfParameter(classSymbol) },
                IsOverride = true,
                DeclarationLine = classDef.LineStart,
                DeclarationColumn = classDef.ColumnStart,
                NameDeclarationLine = classDef.LineStart,
                NameDeclarationColumn = classDef.ColumnStart,
            };

            classSymbol.ProtocolMethods[DunderNames.Repr] = new List<FunctionSymbol> { reprSymbol };
            classSymbol.Methods.Add(reprSymbol);
        }
    }

    private static ParameterSymbol SelfParameter(TypeSymbol classSymbol) => new()
    {
        Name = PythonNames.Self,
        Type = new UserDefinedType { Name = classSymbol.Name, Symbol = classSymbol },
    };
}
