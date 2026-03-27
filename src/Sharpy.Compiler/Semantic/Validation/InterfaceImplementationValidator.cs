using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Collections;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates that classes and structs implement all required interface methods.
/// This includes methods from directly implemented interfaces, base interfaces,
/// and interfaces implemented by base classes.
/// Abstract classes are exempt from this check.
/// </summary>
internal class InterfaceImplementationValidator : ValidatingAstWalker
{
    public override string Name => "InterfaceImplementationValidator";
    public override int Order => 480;

    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _logger = context.Logger;
        base.Validate(module, context);
    }

    public override void VisitClassDef(ClassDef node)
    {
        var classSymbol = Context.SymbolTable.LookupType(node.Name);
        if (classSymbol != null && !classSymbol.IsAbstract)
        {
            ValidateInterfaceImplementations(classSymbol, node.LineStart, node.ColumnStart, node.Span);
        }
        base.VisitClassDef(node);
    }

    public override void VisitStructDef(StructDef node)
    {
        var structSymbol = Context.SymbolTable.LookupType(node.Name);
        if (structSymbol != null)
        {
            ValidateInterfaceImplementations(structSymbol, node.LineStart, node.ColumnStart, node.Span);
        }
        base.VisitStructDef(node);
    }

    private void ValidateInterfaceImplementations(
        TypeSymbol typeSymbol, int? declarationLine, int? declarationColumn,
        Text.TextSpan? declarationSpan = null)
    {
        var allInterfaces = CollectAllInterfaces(typeSymbol);
        if (allInterfaces.Count == 0)
            return;

        _logger.LogDebug($"Validating interface implementations for '{typeSymbol.Name}': {allInterfaces.Count} interfaces");

        var implementedMethodsByName = CollectImplementedMethodsByName(typeSymbol);

        foreach (var iface in allInterfaces)
        {
            foreach (var interfaceMethod in iface.Methods)
            {
                if (!interfaceMethod.IsAbstract)
                    continue;

                if (!implementedMethodsByName.TryGetValue(interfaceMethod.Name, out var classMethod))
                {
                    AddError(
                        $"Class '{typeSymbol.Name}' does not implement interface method '{iface.Name}.{interfaceMethod.Name}'",
                        declarationLine,
                        declarationColumn,
                        code: DiagnosticCodes.Semantic.InterfaceMethodNotImplemented,
                        span: declarationSpan);
                    continue;
                }

                var interfaceParams = interfaceMethod.Parameters.Where(p => p.Name != PythonNames.Self).ToList();
                var classParams = classMethod.Parameters.Where(p => p.Name != PythonNames.Self).ToList();

                if (interfaceParams.Count != classParams.Count)
                {
                    AddError(
                        $"Class '{typeSymbol.Name}' method '{interfaceMethod.Name}' has {classParams.Count} parameters but interface '{iface.Name}' requires {interfaceParams.Count}",
                        declarationLine,
                        declarationColumn,
                        code: DiagnosticCodes.Semantic.IncompatibleOverride,
                        span: declarationSpan);
                }
            }
        }
    }

    private TypeSymbolSet CollectAllInterfaces(TypeSymbol type)
    {
        var all = TypeHierarchyService.GetAllInterfaces(type, Context.SemanticBinding);
        var result = new TypeSymbolSet();
        foreach (var iface in all)
            result.Add(iface);
        return result;
    }

    private Dictionary<string, FunctionSymbol> CollectImplementedMethodsByName(TypeSymbol type)
        => TypeHierarchyService.CollectAllMethods(type, Context.SemanticBinding);
}
