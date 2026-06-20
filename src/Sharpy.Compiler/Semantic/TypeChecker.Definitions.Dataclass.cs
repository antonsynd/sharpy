using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: @dataclass decorator processing and method synthesis
/// </summary>
internal partial class TypeChecker
{
    /// <summary>
    /// Processes @dataclass decorator on a class: extracts options, collects fields,
    /// validates field ordering, and sets IsDataclass/DataclassInfo/DataclassFields on the symbol.
    /// </summary>
    private void ProcessDataclassDecorator(TypeSymbol classSymbol, ClassDef classDef)
    {
        var dataclassDecorator = classDef.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.Dataclass);
        if (dataclassDecorator == null)
            return;

        // Extract options from keyword arguments
        bool frozen = false;
        bool eq = true;
        bool repr = true;

        foreach (var kwArg in dataclassDecorator.KeywordArguments)
        {
            if (kwArg.Value is BooleanLiteral boolLit)
            {
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
        }

        classSymbol.IsDataclass = true;
        classSymbol.DataclassInfo = new DataclassOptions(frozen, eq, repr);

        // Collect dataclass fields: typed field declarations (not properties, not methods)
        var fieldDecls = classDef.Body.OfType<VariableDeclaration>().ToList();
        var dataclassFields = new List<VariableSymbol>();

        // Check for Assignment nodes in class body — these are untyped field declarations
        // that need type annotations in a @dataclass context
        foreach (var assignment in classDef.Body.OfType<Assignment>())
        {
            if (assignment.Target is Identifier ident && assignment.Operator == AssignmentOperator.Assign)
            {
                AddError(
                    $"Dataclass field '{ident.Name}' in '{classDef.Name}' must have a type annotation " +
                    $"(use '{ident.Name}: type = ...' instead of '{ident.Name} = ...').",
                    assignment.LineStart,
                    assignment.ColumnStart,
                    code: DiagnosticCodes.Semantic.DataclassFieldNoType,
                    span: assignment.Span);
            }
        }

        // Collect inherited fields from parent @dataclass (parent fields first)
        if (classSymbol.BaseType is { IsDataclass: true, DataclassFields: { } parentFields })
        {
            dataclassFields.AddRange(parentFields);
        }

        bool seenDefault = dataclassFields.Any(f => f.HasDefaultValue);

        foreach (var fieldDecl in fieldDecls)
        {
            // Skip static fields — they're not instance fields for the dataclass
            if (fieldDecl.Decorators.Any(d => d.Name == DecoratorNames.Static))
                continue;

            // Dataclass fields must have type annotations
            if (fieldDecl.Type == null)
            {
                AddError(
                    $"Dataclass field '{fieldDecl.Name}' in '{classDef.Name}' must have a type annotation.",
                    fieldDecl.LineStart,
                    fieldDecl.ColumnStart,
                    code: DiagnosticCodes.Semantic.DataclassFieldNoType,
                    span: fieldDecl.Span);
                continue;
            }

            bool hasDefault = fieldDecl.InitialValue != null;

            // Enforce ordering: non-default fields before default fields
            if (!hasDefault && seenDefault)
            {
                AddError(
                    $"Non-default field '{fieldDecl.Name}' in dataclass '{classDef.Name}' " +
                    "cannot follow a field with a default value.",
                    fieldDecl.LineStart,
                    fieldDecl.ColumnStart,
                    code: DiagnosticCodes.Semantic.DataclassFieldOrdering,
                    span: fieldDecl.Span);
            }

            if (hasDefault)
                seenDefault = true;

            // Find the corresponding field symbol
            var fieldSymbol = classSymbol.Fields.FirstOrDefault(f => f.Name == fieldDecl.Name);
            if (fieldSymbol != null)
            {
                dataclassFields.Add(fieldSymbol);
            }
        }

        classSymbol.DataclassFields = dataclassFields;

        // Synthesize methods that don't have explicit definitions
        SynthesizeDataclassMethods(classSymbol, classDef, dataclassFields);
    }

    /// <summary>
    /// Synthesizes __init__, __eq__, __repr__, and __hash__ FunctionSymbols for a @dataclass
    /// if not explicitly defined by the user.
    /// </summary>
    private void SynthesizeDataclassMethods(
        TypeSymbol classSymbol,
        ClassDef classDef,
        List<VariableSymbol> dataclassFields)
    {
        var options = classSymbol.DataclassInfo!;
        var explicitMethods = classDef.Body.OfType<FunctionDef>().Select(f => f.Name).ToHashSet();

        // Synthesize __init__ if not explicitly defined
        if (!explicitMethods.Contains(DunderNames.Init))
        {
            var initParams = new List<ParameterSymbol>
            {
                new() { Name = PythonNames.Self, Type = new UserDefinedType { Name = classSymbol.Name, Symbol = classSymbol } }
            };

            foreach (var field in dataclassFields)
            {
                initParams.Add(new ParameterSymbol
                {
                    Name = field.Name,
                    Type = GetVariableType(field),
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

        // Synthesize __eq__ if eq=True and not explicitly defined
        if (options.Eq && !explicitMethods.Contains(DunderNames.Eq))
        {
            var eqSymbol = new FunctionSymbol
            {
                Name = DunderNames.Eq,
                Kind = SymbolKind.Function,
                ReturnType = SemanticType.Bool,
                Parameters = new List<ParameterSymbol>
                {
                    new() { Name = PythonNames.Self, Type = new UserDefinedType { Name = classSymbol.Name, Symbol = classSymbol } },
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

        // Synthesize __hash__ if eq=True and no explicit __hash__
        // .NET requires GetHashCode whenever Equals is overridden, regardless of frozen
        if (options.Eq && !explicitMethods.Contains(DunderNames.Hash))
        {
            var hashSymbol = new FunctionSymbol
            {
                Name = DunderNames.Hash,
                Kind = SymbolKind.Function,
                ReturnType = SemanticType.Int,
                Parameters = new List<ParameterSymbol>
                {
                    new() { Name = PythonNames.Self, Type = new UserDefinedType { Name = classSymbol.Name, Symbol = classSymbol } },
                },
                IsOverride = true,
                DeclarationLine = classDef.LineStart,
                DeclarationColumn = classDef.ColumnStart,
                NameDeclarationLine = classDef.LineStart,
                NameDeclarationColumn = classDef.ColumnStart,
            };

            classSymbol.ProtocolMethods[DunderNames.Hash] = new List<FunctionSymbol> { hashSymbol };
            classSymbol.Methods.Add(hashSymbol);
        }

        // Synthesize __repr__ if repr=True and not explicitly defined
        if (options.Repr && !explicitMethods.Contains(DunderNames.Repr))
        {
            var reprSymbol = new FunctionSymbol
            {
                Name = DunderNames.Repr,
                Kind = SymbolKind.Function,
                ReturnType = SemanticType.Str,
                Parameters = new List<ParameterSymbol>
                {
                    new() { Name = PythonNames.Self, Type = new UserDefinedType { Name = classSymbol.Name, Symbol = classSymbol } },
                },
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
}
