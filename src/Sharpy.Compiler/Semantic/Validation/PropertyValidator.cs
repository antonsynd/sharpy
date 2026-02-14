using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates property declarations in classes and structs.
///
/// Rules:
/// 1. Property cannot have the same name as a field (SPY0405)
/// 2. Property cannot have the same name as a method (SPY0406)
/// 3. Cannot mix auto-property and function-style for the same name (SPY0407)
/// 4. 'property init' is only valid for auto-properties (SPY0408)
/// 5. @abstract properties must have ellipsis body (SPY0409)
/// 6. @final cannot be combined with @abstract or @virtual (SPY0410)
/// </summary>
internal class PropertyValidator : SemanticValidatorBase
{
    public override string Name => "PropertyValidator";
    public override int Order => 410; // After control flow (400), before unused variables (420)

    private ICompilerLogger _logger = NullLogger.Instance;
    private SemanticContext _context = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _logger = context.Logger;
        _logger.LogDebug("Starting property validation");

        foreach (var stmt in module.Body)
        {
            switch (stmt)
            {
                case ClassDef classDef:
                    ValidateTypeBody(classDef.Name, classDef.Body);
                    break;
                case StructDef structDef:
                    ValidateTypeBody(structDef.Name, structDef.Body);
                    break;
            }
        }
    }

    private void ValidateTypeBody(string typeName, IReadOnlyList<Statement> body)
    {
        // Collect fields, methods, and properties from the body
        var fieldNames = new HashSet<string>();
        var methodNames = new HashSet<string>();
        var propertyDefs = new List<PropertyDef>();

        foreach (var member in body)
        {
            switch (member)
            {
                case VariableDeclaration varDecl:
                    fieldNames.Add(varDecl.Name);
                    break;
                case FunctionDef funcDef:
                    methodNames.Add(funcDef.Name);
                    break;
                case PropertyDef propDef:
                    propertyDefs.Add(propDef);
                    break;
            }
        }

        // Group properties by name to detect mixed styles
        var propertyGroups = new Dictionary<string, List<PropertyDef>>();
        foreach (var propDef in propertyDefs)
        {
            if (!propertyGroups.TryGetValue(propDef.Name, out var group))
            {
                group = new List<PropertyDef>();
                propertyGroups[propDef.Name] = group;
            }
            group.Add(propDef);
        }

        // Check each property definition
        foreach (var propDef in propertyDefs)
        {
            ValidatePropertyAgainstFields(typeName, propDef, fieldNames);
            ValidatePropertyAgainstMethods(typeName, propDef, methodNames);
            ValidateInitOnlyFunctionStyle(typeName, propDef);
            ValidateAbstractPropertyBody(typeName, propDef);
            ValidateFinalNotWithAbstractOrVirtual(typeName, propDef);
        }

        // Check for mixed auto/function-style per property name
        foreach (var (propName, group) in propertyGroups)
        {
            ValidateMixedPropertyStyle(typeName, propName, group);
        }
    }

    /// <summary>
    /// Rule 1: Property cannot share a name with a field.
    /// </summary>
    private void ValidatePropertyAgainstFields(string typeName, PropertyDef propDef, HashSet<string> fieldNames)
    {
        if (fieldNames.Contains(propDef.Name))
        {
            AddError(_context,
                $"Property '{propDef.Name}' in '{typeName}' conflicts with a field of the same name",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.PropertyFieldNameConflict,
                span: propDef.Span);
        }
    }

    /// <summary>
    /// Rule 2: Property cannot share a name with a method.
    /// </summary>
    private void ValidatePropertyAgainstMethods(string typeName, PropertyDef propDef, HashSet<string> methodNames)
    {
        if (methodNames.Contains(propDef.Name))
        {
            AddError(_context,
                $"Property '{propDef.Name}' in '{typeName}' conflicts with a method of the same name",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.PropertyMethodNameConflict,
                span: propDef.Span);
        }
    }

    /// <summary>
    /// Rule 3: Cannot mix auto-property and function-style for the same name.
    /// </summary>
    private void ValidateMixedPropertyStyle(string typeName, string propName, List<PropertyDef> group)
    {
        if (group.Count < 2)
            return;

        bool hasAuto = group.Any(p => !p.IsFunctionStyle);
        bool hasFunction = group.Any(p => p.IsFunctionStyle);

        if (hasAuto && hasFunction)
        {
            // Report on the second definition that conflicts
            var conflicting = group.First(p => p.IsFunctionStyle && group.Any(q => !q.IsFunctionStyle))
                ?? group[1];
            AddError(_context,
                $"Property '{propName}' in '{typeName}' cannot mix auto-property and function-style definitions",
                conflicting.LineStart, conflicting.ColumnStart,
                code: DiagnosticCodes.Validation.MixedAutoAndFunctionStyleProperty,
                span: conflicting.Span);
        }
    }

    /// <summary>
    /// Rule 4: 'property init' is only valid for auto-properties.
    /// </summary>
    private void ValidateInitOnlyFunctionStyle(string typeName, PropertyDef propDef)
    {
        if (propDef.Accessor == PropertyAccessor.Init && propDef.IsFunctionStyle)
        {
            AddError(_context,
                $"'property init' for '{propDef.Name}' in '{typeName}' is only valid for auto-properties, not function-style",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.InitOnlyFunctionStyleProperty,
                span: propDef.Span);
        }
    }

    /// <summary>
    /// Rule 5: @abstract properties must have ellipsis body.
    /// An abstract function-style property has IsFunctionStyle=true and an empty Body.
    /// If there's a non-empty body, it's an error.
    /// </summary>
    private void ValidateAbstractPropertyBody(string typeName, PropertyDef propDef)
    {
        bool isAbstract = propDef.Decorators.Any(d => d.Name == "abstract");
        if (!isAbstract || !propDef.IsFunctionStyle)
            return;

        // For abstract properties, the body must be empty (ellipsis parses to empty body)
        if (propDef.Body.Length > 0)
        {
            AddError(_context,
                $"@abstract property '{propDef.Name}' in '{typeName}' must have '...' (ellipsis) body",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.AbstractPropertyMustHaveEllipsisBody,
                span: propDef.Span);
        }
    }

    /// <summary>
    /// Rule 6: @final cannot be combined with @abstract or @virtual.
    /// </summary>
    private void ValidateFinalNotWithAbstractOrVirtual(string typeName, PropertyDef propDef)
    {
        bool isFinal = propDef.Decorators.Any(d => d.Name == "final");
        if (!isFinal)
            return;

        bool isAbstract = propDef.Decorators.Any(d => d.Name == "abstract");
        bool isVirtual = propDef.Decorators.Any(d => d.Name == "virtual");

        if (isAbstract)
        {
            AddError(_context,
                $"Property '{propDef.Name}' in '{typeName}' cannot be both @final and @abstract",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.FinalWithAbstractOrVirtual,
                span: propDef.Span);
        }

        if (isVirtual)
        {
            AddError(_context,
                $"Property '{propDef.Name}' in '{typeName}' cannot be both @final and @virtual",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.FinalWithAbstractOrVirtual,
                span: propDef.Span);
        }
    }
}
