using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates struct-specific rules:
/// - All struct constructors must initialize all fields (fields with defaults are exempt)
/// - Non-default fields must precede fields with defaults in declaration order
/// - No struct holds itself by value through its field graph (SPY0616, #2354)
/// </summary>
internal class StructRulesValidator : ValidatingAstWalker
{
    public override string Name => "StructRulesValidator";
    public override int Order => 145;

    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _logger = context.Logger;
        base.Validate(module, context);
    }

    public override void VisitStructDef(StructDef node)
    {
        var structSymbol = Context.LookupDeclaredType(node, node.Name);
        if (structSymbol != null)
        {
            ValidateStructFieldOrdering(structSymbol, node);
            ValidateStructRules(structSymbol, node);
            ValidateStructLayoutIsAcyclic(structSymbol, node);
        }
        base.VisitStructDef(node);
    }

    /// <summary>
    /// Validates that non-default fields precede fields with defaults in declaration order.
    /// </summary>
    private void ValidateStructFieldOrdering(TypeSymbol structSymbol, StructDef structDef)
    {
        bool seenDefault = false;

        foreach (var stmt in structDef.Body)
        {
            if (stmt is not VariableDeclaration varDecl)
                continue;

            // Class-level storage (a `const`, a `@static` field) is not an instance field, so it
            // is neither ordered nor defaulted-ness-checked. A `const` always carries an
            // initializer, so counting it as "a field with a default" made every instance field
            // declared after it SPY0435 (#1794).
            if (!MemberClassification.IsInstanceField(varDecl))
                continue;

            bool hasDefault = varDecl.InitialValue != null;

            if (!hasDefault && seenDefault)
            {
                AddError(
                    $"Non-default field '{varDecl.Name}' in struct '{structSymbol.Name}' " +
                    "cannot follow a field with a default value.",
                    varDecl.LineStart,
                    varDecl.ColumnStart,
                    code: DiagnosticCodes.Validation.StructFieldOrdering,
                    span: varDecl.Span);
            }

            if (hasDefault)
                seenDefault = true;
        }
    }

    /// <summary>
    /// A struct's layout is finite only when no instance field holds the struct itself BY VALUE,
    /// however many value types lie in between (Roslyn's CS0523, which used to surface as SPY0908).
    /// Each instance field is walked through the types a value holds inline — user structs (their
    /// instance fields, with a generic struct's type arguments substituted), tuples, <c>T?</c>
    /// (<c>Optional&lt;T&gt;</c>), <c>T | None</c> over a value type, and <c>Result[T, E]</c> — and a
    /// field whose walk reaches this struct's definition again starts a cycle. Reference types
    /// (classes, collections, <c>str</c>) end the walk: they hold a reference, not the value.
    /// </summary>
    private void ValidateStructLayoutIsAcyclic(TypeSymbol structSymbol, StructDef structDef)
    {
        foreach (var stmt in structDef.Body)
        {
            if (stmt is not VariableDeclaration varDecl || !MemberClassification.IsInstanceField(varDecl))
                continue;
            var field = structSymbol.Fields.FirstOrDefault(f => f.Name == varDecl.Name);
            if (field == null)
                continue;

            var chain = new List<string> { $"{structSymbol.Name}.{field.Name}" };
            var visited = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);
            if (!HoldsByValue(FieldType(field), structSymbol, visited, chain))
                continue;

            AddError(
                $"Struct '{structSymbol.Name}' contains itself by value through field '{field.Name}' " +
                $"({string.Join(" -> ", chain)} -> {structSymbol.Name}); store a reference type such as " +
                $"'list[{structSymbol.Name}]' or make '{structSymbol.Name}' a class",
                varDecl.LineStart,
                varDecl.ColumnStart,
                code: DiagnosticCodes.SemanticOverflow.StructLayoutCycle,
                span: varDecl.Span);
        }
    }

    /// <summary>
    /// Whether a value of <paramref name="type"/> holds <paramref name="origin"/> inline. Appends the
    /// fields walked through to <paramref name="chain"/> when it does; <paramref name="visited"/>
    /// stops a cycle that does not pass through <paramref name="origin"/> (reported at its own
    /// structs).
    /// </summary>
    private bool HoldsByValue(SemanticType type, TypeSymbol origin, HashSet<TypeSymbol> visited, List<string> chain)
    {
        switch (type)
        {
            case TupleType tuple:
                return tuple.ElementTypes.Any(e => HoldsByValue(e, origin, visited, chain));
            case OptionalType optional:
                // Optional<T> is a struct holding its T.
                return HoldsByValue(optional.UnderlyingType, origin, visited, chain);
            case NullableType nullable when nullable.UnderlyingType.IsValueType:
                // Nullable<T> over a value type holds the T; over a reference type it is the reference.
                return HoldsByValue(nullable.UnderlyingType, origin, visited, chain);
            case ResultType result:
                // Result<T, E> is a struct holding both its T and its E.
                return HoldsByValue(result.OkType, origin, visited, chain)
                    || HoldsByValue(result.ErrorType, origin, visited, chain);
            case UserDefinedType { Symbol: { TypeKind: TypeKind.Struct } symbol }:
                return StructHolds(symbol, new Dictionary<string, SemanticType>(), origin, visited, chain);
            case GenericType { GenericDefinition: { TypeKind: TypeKind.Struct } definition } generic:
                var substitution = new Dictionary<string, SemanticType>();
                for (int i = 0; i < definition.TypeParameters.Count && i < generic.TypeArguments.Count; i++)
                    substitution[definition.TypeParameters[i].Name] = generic.TypeArguments[i];
                return StructHolds(definition, substitution, origin, visited, chain);
            default:
                return false;
        }
    }

    private bool StructHolds(
        TypeSymbol structSymbol, Dictionary<string, SemanticType> substitution,
        TypeSymbol origin, HashSet<TypeSymbol> visited, List<string> chain)
    {
        if (ReferenceEquals(structSymbol, origin))
            return true;
        if (!visited.Add(structSymbol))
            return false;

        foreach (var field in structSymbol.Fields)
        {
            if (field.IsStatic || field.IsConstant)
                continue;
            chain.Add($"{structSymbol.Name}.{field.Name}");
            if (HoldsByValue(TypeSubstitution.Apply(FieldType(field), substitution), origin, visited, chain))
                return true;
            chain.RemoveAt(chain.Count - 1);
        }

        return false;
    }

    private SemanticType FieldType(VariableSymbol field)
    {
        var type = Context.SemanticBinding.GetVariableType(field);
        return type is UnknownType ? field.Type : type;
    }

    private void ValidateStructRules(TypeSymbol structSymbol, StructDef structDef)
    {
        _logger.LogDebug($"Validating struct-specific rules for '{structSymbol.Name}'");

        if (structSymbol.Constructors.Count > 0)
        {
            foreach (var constructor in structSymbol.Constructors)
            {
                ValidateStructConstructorInitializesAllFields(structSymbol, constructor, structDef);
            }
        }
    }

    private void ValidateStructConstructorInitializesAllFields(
        TypeSymbol structSymbol,
        FunctionSymbol constructor,
        StructDef structDef)
    {
        var constructorDef = structDef.Body
            .OfType<FunctionDef>()
            .FirstOrDefault(f => f.Name == DunderNames.Init && f.LineStart == constructor.DeclarationLine);

        if (constructorDef == null)
            return;

        var cfg = Context.ControlFlowGraphs.GetOrBuild(constructorDef);
        var definitelyAssigned = DefiniteFieldAssignmentAnalysis.FindDefinitelyAssignedFields(cfg);

        // Collect the names of fields that have default values in their declaration, and the
        // class-level storage an explicit __init__ never assigns (`const`, `@static`).
        var fieldsWithDefaults = new HashSet<string>();
        var nonInstanceFields = new HashSet<string>();
        foreach (var stmt in structDef.Body)
        {
            if (stmt is not VariableDeclaration varDecl)
                continue;
            if (!MemberClassification.IsInstanceField(varDecl))
            {
                nonInstanceFields.Add(varDecl.Name);
                continue;
            }
            if (varDecl.InitialValue != null)
                fieldsWithDefaults.Add(varDecl.Name);
        }

        // Fields with defaults are exempt from the "must initialize" check; so is class-level
        // storage, which the constructor has nothing to assign (#1794).
        var uninitializedFields = structSymbol.Fields
            .Where(f => !definitelyAssigned.Contains(f.Name)
                && !fieldsWithDefaults.Contains(f.Name)
                && !nonInstanceFields.Contains(f.Name))
            .ToList();

        if (uninitializedFields.Count > 0)
        {
            var fieldNames = string.Join(", ", uninitializedFields.Select(f => $"'{f.Name}'"));
            AddError(
                $"Struct '{structSymbol.Name}' constructor must initialize all fields. " +
                $"Missing initialization for: {fieldNames}",
                constructorDef.LineStart,
                constructorDef.ColumnStart,
                code: DiagnosticCodes.Semantic.UninitializedStructField,
                span: constructorDef.Span);
        }
    }
}
