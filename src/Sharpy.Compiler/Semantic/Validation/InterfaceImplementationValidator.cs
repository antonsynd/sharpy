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
        var classSymbol = Context.LookupDeclaredType(node, node.Name);
        if (classSymbol != null && !classSymbol.IsAbstract)
        {
            ValidateInterfaceImplementations(classSymbol, node.LineStart, node.ColumnStart, node.Span);
        }
        base.VisitClassDef(node);
    }

    public override void VisitStructDef(StructDef node)
    {
        var structSymbol = Context.LookupDeclaredType(node, node.Name);
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

                // A mixed-escape implementation emits a different C# name than the interface member
                // it is meant to implement (#2033) — refused here, by name, not as CS0535.
                if (iface.ClrType == null
                    && MemberClassification.MethodSpellingsDiffer(
                        interfaceMethod.Name, interfaceMethod.IsNameBacktickEscaped, classMethod.IsNameBacktickEscaped))
                {
                    AddError(
                        $"Class '{typeSymbol.Name}' does not implement interface method '{iface.Name}.{interfaceMethod.Name}': "
                        + $"it is declared as {MemberClassification.Spelling(interfaceMethod.Name, interfaceMethod.IsNameBacktickEscaped)} "
                        + $"but implemented as {MemberClassification.Spelling(classMethod.Name, classMethod.IsNameBacktickEscaped)}; "
                        + "implement it with the same spelling",
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

            if (iface.ClrType != null)
                continue;

            // Properties and events are named by the method rule too (NameCasing.ResolveMethod), so a
            // mixed-escape implementation misses its interface member exactly as a method does — the
            // method arm above refused it by name while the property and event twins reached C# as
            // CS0535 behind SPY0908 (#2033). Checked whatever the member's abstractness: an auto
            // property or event on an interface is abstract in C# but not flagged IsAbstract here,
            // and against a DEFAULT-bodied property the mismatched member silently failed to
            // implement it (the interface's default answered). The method twin of that cell is
            // already refused (SPY0248, a default method needs @override).
            foreach (var interfaceProperty in iface.Properties)
            {
                var (classProperty, _) = TypeHierarchyService.FindMember(
                    typeSymbol, interfaceProperty.Name, t => t.Properties, searchInterfaces: false, Context.SemanticBinding);
                if (classProperty != null)
                {
                    ReportMixedEscapeImplementation(typeSymbol, iface, "property", interfaceProperty.Name,
                        interfaceProperty.IsNameBacktickEscaped, classProperty.IsNameBacktickEscaped,
                        declarationLine, declarationColumn, declarationSpan);
                }
            }

            foreach (var interfaceEvent in iface.Events)
            {
                var classEvent = new[] { typeSymbol }
                    .Concat(TypeHierarchyService.GetAllBaseTypes(typeSymbol, Context.SemanticBinding))
                    .SelectMany(t => t.Events)
                    .FirstOrDefault(e => e.Name == interfaceEvent.Name);
                if (classEvent != null)
                {
                    ReportMixedEscapeImplementation(typeSymbol, iface, "event", interfaceEvent.Name,
                        interfaceEvent.IsNameBacktickEscaped, classEvent.IsNameBacktickEscaped,
                        declarationLine, declarationColumn, declarationSpan);
                }
            }
        }
    }

    private void ReportMixedEscapeImplementation(
        TypeSymbol typeSymbol, TypeSymbol iface, string memberKind, string name,
        bool declaredEscaped, bool implementedEscaped,
        int? declarationLine, int? declarationColumn, Text.TextSpan? declarationSpan)
    {
        if (!MemberClassification.MethodSpellingsDiffer(name, declaredEscaped, implementedEscaped))
            return;

        AddError(
            $"Class '{typeSymbol.Name}' does not implement interface {memberKind} '{iface.Name}.{name}': "
            + $"it is declared as {MemberClassification.Spelling(name, declaredEscaped)} "
            + $"but implemented as {MemberClassification.Spelling(name, implementedEscaped)}; "
            + "implement it with the same spelling",
            declarationLine,
            declarationColumn,
            code: DiagnosticCodes.Semantic.InterfaceMethodNotImplemented,
            span: declarationSpan);
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
