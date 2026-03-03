using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;

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
        [DecoratorNames.StaticMethod] = "The '@staticmethod' decorator is not supported in Sharpy. " +
                           "Methods without a 'self' parameter are automatically static.",
        [DecoratorNames.ClassMethod] = "The '@classmethod' decorator is not supported in Sharpy.",
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

            case EventDef eventDef:
                ValidateDecorators(eventDef.Decorators, eventDef.Name);
                break;

            case VariableDeclaration varDecl when varDecl.Decorators.Length > 0:
                // Decorated variables at module level are not allowed —
                // @static only makes sense on class/struct fields
                ValidateModuleLevelFieldDecorators(varDecl);
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
                ValidateVirtualOnObjectOverride(method, classDef.Name);
            }
            else if (member is VariableDeclaration varDecl)
            {
                ValidateFieldDecorators(varDecl, classDef.Name);
            }
            else if (member is EventDef eventDef)
            {
                ValidateDecorators(eventDef.Decorators, $"{classDef.Name}.{eventDef.Name}");
                ValidateEventFinalRequiresOverride(eventDef, classDef.Name);
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
                ValidateVirtualOnStruct(method, structDef.Name);
            }
            else if (member is VariableDeclaration varDecl)
            {
                ValidateFieldDecorators(varDecl, structDef.Name);
            }
            else if (member is EventDef eventDef)
            {
                ValidateDecorators(eventDef.Decorators, $"{structDef.Name}.{eventDef.Name}");
                ValidateEventFinalRequiresOverride(eventDef, structDef.Name);
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
            else if (member is EventDef eventDef)
            {
                ValidateDecorators(eventDef.Decorators, $"{interfaceDef.Name}.{eventDef.Name}");
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

            // Known modifier decorators must not have arguments
            if (DecoratorNames.KnownModifierDecorators.Contains(decorator.Name)
                && (decorator.Arguments.Length > 0 || decorator.KeywordArguments.Length > 0))
            {
                AddError(_context,
                    $"Built-in decorator '@{decorator.Name}' does not accept arguments",
                    decorator.LineStart,
                    decorator.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                    span: decorator.Span);
            }

            // For unknown decorators (custom attributes), validate arguments are compile-time constants
            if (!DecoratorNames.KnownModifierDecorators.Contains(decorator.Name)
                && !UnsupportedDecorators.ContainsKey(decorator.Name))
            {
                ValidateDecoratorArgumentsAreConstants(decorator);
            }
        }
    }

    /// <summary>
    /// Validates that all arguments to a custom decorator are compile-time constant expressions.
    /// Allowed: string/int/float/bool literals, None, enum member access (dotted names), type(X).
    /// </summary>
    private void ValidateDecoratorArgumentsAreConstants(Decorator decorator)
    {
        foreach (var arg in decorator.Arguments)
        {
            if (!IsCompileTimeConstant(arg))
            {
                AddError(_context,
                    "Decorator argument must be a compile-time constant",
                    arg.LineStart,
                    arg.ColumnStart,
                    code: DiagnosticCodes.Validation.NonConstantDecoratorArgument,
                    span: arg.Span);
            }
        }

        foreach (var kwArg in decorator.KeywordArguments)
        {
            if (!IsCompileTimeConstant(kwArg.Value))
            {
                AddError(_context,
                    "Decorator argument must be a compile-time constant",
                    kwArg.Value.LineStart,
                    kwArg.Value.ColumnStart,
                    code: DiagnosticCodes.Validation.NonConstantDecoratorArgument,
                    span: kwArg.Value.Span);
            }
        }
    }

    /// <summary>
    /// Returns true if the expression is a compile-time constant suitable for a decorator argument.
    /// Allowed forms:
    /// - Literals: string, int, float, bool, None
    /// - Enum member access: dotted name like EnumType.member (MemberAccess with Identifier object)
    /// - type(X): FunctionCall with name "type" and exactly one argument
    /// </summary>
    private static bool IsCompileTimeConstant(Expression expr)
    {
        return expr switch
        {
            StringLiteral => true,
            IntegerLiteral => true,
            FloatLiteral => true,
            BooleanLiteral => true,
            NoneLiteral => true,
            // Enum member access: SomeType.Member
            MemberAccess { Object: Identifier } => true,
            // type(X) is allowed as typeof equivalent
            FunctionCall { Function: Identifier { Name: "type" }, Arguments.Length: 1, KeywordArguments.Length: 0 } => true,
            // Negative numeric literals: -42, -3.14
            UnaryOp { Operator: UnaryOperator.Minus, Operand: IntegerLiteral or FloatLiteral } => true,
            _ => false,
        };
    }

    /// <summary>
    /// Validates that module-level variable declarations cannot have decorators.
    /// Decorators like @static only make sense on class/struct fields.
    /// </summary>
    private void ValidateModuleLevelFieldDecorators(VariableDeclaration varDecl)
    {
        foreach (var decorator in varDecl.Decorators)
        {
            AddError(_context,
                $"Decorators cannot be applied to module-level variable declarations. " +
                $"'@{decorator.Name}' on '{varDecl.Name}' is only valid inside a class or struct body.",
                decorator.LineStart,
                decorator.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                span: decorator.Span);
        }
    }

    /// <summary>
    /// Validates decorators on field declarations. Only @static is allowed.
    /// </summary>
    private void ValidateFieldDecorators(VariableDeclaration varDecl, string typeName)
    {
        foreach (var decorator in varDecl.Decorators)
        {
            if (decorator.Name != DecoratorNames.Static)
            {
                AddError(_context,
                    $"Decorator '@{decorator.Name}' is not valid on field '{varDecl.Name}' in '{typeName}'. " +
                    "Only @static is allowed on field declarations.",
                    decorator.LineStart,
                    decorator.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                    span: decorator.Span);
            }
        }
    }

    /// <summary>
    /// Validates that @virtual is not used on struct methods (structs are sealed in C#).
    /// </summary>
    private void ValidateVirtualOnStruct(FunctionDef method, string typeName)
    {
        var virtualDecorator = method.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.Virtual);
        if (virtualDecorator != null)
        {
            AddError(_context,
                $"Struct method '{method.Name}' in '{typeName}' cannot be @virtual. " +
                "Struct methods cannot be virtual because structs are implicitly sealed.",
                virtualDecorator.LineStart,
                virtualDecorator.ColumnStart,
                code: DiagnosticCodes.Validation.VirtualOnStructMethod,
                span: virtualDecorator.Span);
        }
    }

    /// <summary>
    /// Warns when @virtual is used on dunder methods that always generate 'override'
    /// (e.g., __str__ → ToString(), __hash__ → GetHashCode()).
    /// </summary>
    private void ValidateVirtualOnObjectOverride(FunctionDef method, string typeName)
    {
        if (!method.Decorators.Any(d => d.Name == DecoratorNames.Virtual))
            return;

        // These dunders always generate 'override' on Object methods
        string? csharpName = method.Name switch
        {
            DunderNames.Str => "Object.ToString()",
            DunderNames.Hash => "Object.GetHashCode()",
            _ => null
        };

        // __eq__ with object parameter also overrides Object.Equals
        if (csharpName == null && method.Name == DunderNames.Eq
            && method.Parameters.Any(p =>
                p.Name != PythonNames.Self
                && p.Type?.Name == "object"))
        {
            csharpName = "Object.Equals()";
        }

        if (csharpName != null)
        {
            var virtualDecorator = method.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.Virtual);
            if (virtualDecorator == null)
                return;
            AddWarning(_context,
                $"@virtual is redundant on '{method.Name}' in '{typeName}' — " +
                $"it always overrides {csharpName}. The @virtual decorator will be ignored.",
                virtualDecorator.LineStart,
                virtualDecorator.ColumnStart,
                code: DiagnosticCodes.Validation.VirtualOnObjectOverride,
                span: virtualDecorator.Span);
        }
    }

    /// <summary>
    /// Validates that @final on an event is always accompanied by @override.
    /// </summary>
    private void ValidateEventFinalRequiresOverride(EventDef eventDef, string typeName)
    {
        bool hasFinal = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Final);
        bool hasOverride = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Override);

        if (hasFinal && !hasOverride)
        {
            var finalDecorator = eventDef.Decorators.First(d => d.Name == DecoratorNames.Final);
            AddError(_context,
                $"Event '{eventDef.Name}' in '{typeName}' is marked @final but not @override. " +
                "The @final decorator prevents further overriding and requires @override.",
                finalDecorator.LineStart,
                finalDecorator.ColumnStart,
                code: DiagnosticCodes.Validation.FinalWithoutOverride,
                span: finalDecorator.Span);
        }
    }

    /// <summary>
    /// Validates that @final on a method is always accompanied by @override.
    /// @final prevents further overriding, so it only makes sense on an override method.
    /// </summary>
    private void ValidateFinalRequiresOverride(FunctionDef method, string typeName)
    {
        bool hasFinal = method.Decorators.Any(d => d.Name == DecoratorNames.Final);
        bool hasOverride = method.Decorators.Any(d => d.Name == DecoratorNames.Override);

        if (hasFinal && !hasOverride)
        {
            var finalDecorator = method.Decorators.First(d => d.Name == DecoratorNames.Final);
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
