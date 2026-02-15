using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates decorator usage across all definitions.
///
/// Currently validates:
/// - @staticmethod is not supported (methods without 'self' are automatically static)
/// - @final without @override on methods (SPY0412)
///
/// This validator runs early (Order 60) to catch decorator errors before
/// other validators attempt to process the decorated definitions.
/// </summary>
internal class DecoratorValidator : SemanticValidatorBase
{
    public override string Name => "DecoratorValidator";
    public override int Order => 60; // After ModuleLevelValidator (50), before SignatureValidator (150)

    private ICompilerLogger _logger = NullLogger.Instance;
    private SemanticContext _context = null!;

    /// <summary>
    /// Set of decorators that are explicitly unsupported with their error messages.
    /// </summary>
    private static readonly Dictionary<string, string> UnsupportedDecorators = new()
    {
        ["staticmethod"] = "The '@staticmethod' decorator is not supported in Sharpy. " +
                           "Methods without a 'self' parameter are automatically static.",
        ["classmethod"] = "The '@classmethod' decorator is not supported in Sharpy.",
    };

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _logger = context.Logger;
        _logger.LogDebug("Starting decorator validation");

        // Validate top-level function definitions
        foreach (var stmt in module.Body)
        {
            ValidateStatement(stmt);
        }
    }

    private void ValidateStatement(Statement stmt)
    {
        switch (stmt)
        {
            case FunctionDef funcDef:
                ValidateDecorators(funcDef.Decorators, funcDef.Name);
                break;

            case ClassDef classDef:
                ValidateDecorators(classDef.Decorators, classDef.Name);
                ValidateClassBody(classDef);
                break;

            case StructDef structDef:
                ValidateDecorators(structDef.Decorators, structDef.Name);
                ValidateStructBody(structDef);
                break;

            case InterfaceDef interfaceDef:
                // InterfaceDef doesn't have decorators, but validate methods inside
                ValidateInterfaceBody(interfaceDef);
                break;

            case PropertyDef propDef:
                ValidateDecorators(propDef.Decorators, propDef.Name);
                break;
        }
    }

    private void ValidateClassBody(ClassDef classDef)
    {
        foreach (var member in classDef.Body)
        {
            if (member is FunctionDef method)
            {
                ValidateDecorators(method.Decorators, $"{classDef.Name}.{method.Name}");
                ValidateFinalRequiresOverride(method, classDef.Name);
            }
        }
    }

    private void ValidateStructBody(StructDef structDef)
    {
        foreach (var member in structDef.Body)
        {
            if (member is FunctionDef method)
            {
                ValidateDecorators(method.Decorators, $"{structDef.Name}.{method.Name}");
                ValidateFinalRequiresOverride(method, structDef.Name);
            }
        }
    }

    private void ValidateInterfaceBody(InterfaceDef interfaceDef)
    {
        foreach (var member in interfaceDef.Body)
        {
            if (member is FunctionDef method)
            {
                ValidateDecorators(method.Decorators, $"{interfaceDef.Name}.{method.Name}");
            }
        }
    }

    private void ValidateDecorators(IEnumerable<Decorator> decorators, string definitionName)
    {
        foreach (var decorator in decorators)
        {
            if (UnsupportedDecorators.TryGetValue(decorator.Name, out var errorMessage))
            {
                _logger.LogDebug($"Found unsupported decorator '@{decorator.Name}' on '{definitionName}'");
                AddError(_context, errorMessage, decorator.LineStart, decorator.ColumnStart, code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                    span: decorator.Span);
            }
        }
    }

    /// <summary>
    /// Validates that @final on a method is always accompanied by @override.
    /// @final prevents further overriding, so it only makes sense on an override method.
    /// </summary>
    private void ValidateFinalRequiresOverride(FunctionDef method, string typeName)
    {
        bool hasFinal = method.Decorators.Any(d => d.Name == "final");
        bool hasOverride = method.Decorators.Any(d => d.Name == "override");

        if (hasFinal && !hasOverride)
        {
            var finalDecorator = method.Decorators.First(d => d.Name == "final");
            AddError(_context,
                $"Method '{method.Name}' in '{typeName}' is marked @final but not @override. " +
                "The @final decorator prevents further overriding and requires @override.",
                finalDecorator.LineStart,
                finalDecorator.ColumnStart,
                code: DiagnosticCodes.Validation.FinalWithoutOverride,
                span: finalDecorator.Span);
        }
    }
}
