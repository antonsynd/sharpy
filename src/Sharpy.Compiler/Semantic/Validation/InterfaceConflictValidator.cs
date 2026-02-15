using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Detects conflicting synthesized generic interfaces between a class and its ancestors.
/// For example, if Base defines __eq__(self, other: str) synthesizing IEquatable&lt;str&gt;,
/// and Derived defines __eq__(self, other: int) synthesizing IEquatable&lt;int&gt;, this
/// creates a conflicting generic interface (C# forbids the same generic interface with
/// different type arguments in a single type hierarchy).
///
/// Only generic interfaces can conflict — non-generic interfaces (ISized, IBoolConvertible)
/// are harmless duplicates.
/// </summary>
internal class InterfaceConflictValidator : SemanticValidatorBase
{
    public override string Name => "InterfaceConflictValidator";
    public override int Order => 170; // After EqualityContractValidator (160)

    public override void Validate(Module module, SemanticContext context)
    {
        foreach (var stmt in module.Body)
        {
            switch (stmt)
            {
                case ClassDef classDef:
                    ValidateType(classDef.Name, classDef.Body, context);
                    break;
                case StructDef structDef:
                    ValidateType(structDef.Name, structDef.Body, context);
                    break;
            }
        }
    }

    private void ValidateType(string typeName, IReadOnlyList<Statement> body, SemanticContext context)
    {
        var typeSymbol = context.SymbolTable.Lookup(typeName) as TypeSymbol;
        if (typeSymbol == null)
            return;

        // Compute synthesized interfaces for the current type
        var currentSynthesized = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Only check generic interfaces — non-generic ones can't conflict
        var currentGeneric = currentSynthesized
            .Where(i => i.TypeArgs.Length > 0)
            .ToList();

        if (currentGeneric.Count == 0)
            return;

        // Also include explicit interfaces from TypeSymbol.Interfaces
        var currentExplicitGeneric = CollectExplicitGenericInterfaces(typeSymbol);

        // Walk ancestor chain
        var visited = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);
        var ancestor = typeSymbol.BaseType;
        while (ancestor != null && visited.Add(ancestor))
        {
            // Get ancestor's synthesized + explicit interfaces
            var ancestorSynthesized = SynthesisAnalyzer.ComputeSynthesizedInterfaces(ancestor);
            var ancestorGeneric = ancestorSynthesized
                .Where(i => i.TypeArgs.Length > 0)
                .ToList();
            var ancestorExplicitGeneric = CollectExplicitGenericInterfaces(ancestor);

            // Check for conflicts: same interface name, different type args
            foreach (var current in currentGeneric)
            {
                CheckConflict(current, ancestorGeneric, typeName, ancestor.Name, body, context);
                CheckConflictWithExplicit(current, ancestorExplicitGeneric, typeName, ancestor.Name, body, context);
            }

            ancestor = ancestor.BaseType;
        }
    }

    private void CheckConflict(
        SynthesizedInterfaceInfo current,
        List<SynthesizedInterfaceInfo> ancestorInterfaces,
        string typeName, string ancestorName,
        IReadOnlyList<Statement> body,
        SemanticContext context)
    {
        foreach (var ancestorIface in ancestorInterfaces)
        {
            if (current.InterfaceName == ancestorIface.InterfaceName
                && !TypeArgsMatch(current.TypeArgs, ancestorIface.TypeArgs))
            {
                var triggeringFunc = body.OfType<FunctionDef>()
                    .FirstOrDefault(f => f.Name == current.TriggeringDunder);

                var currentArgs = FormatTypeArgs(current.TypeArgs);
                var ancestorArgs = FormatTypeArgs(ancestorIface.TypeArgs);

                AddError(context,
                    $"Type '{typeName}' would synthesize '{current.InterfaceName}<{currentArgs}>' via '{current.TriggeringDunder}', " +
                    $"but ancestor '{ancestorName}' already implements '{current.InterfaceName}<{ancestorArgs}>'. " +
                    "This creates a conflicting generic interface.",
                    triggeringFunc?.LineStart,
                    triggeringFunc?.ColumnStart,
                    code: DiagnosticCodes.Semantic.ConflictingSynthesizedInterface,
                    span: triggeringFunc?.Span);
            }
        }
    }

    private void CheckConflictWithExplicit(
        SynthesizedInterfaceInfo current,
        List<(string Name, SemanticType[] TypeArgs)> ancestorExplicit,
        string typeName, string ancestorName,
        IReadOnlyList<Statement> body,
        SemanticContext context)
    {
        foreach (var (name, typeArgs) in ancestorExplicit)
        {
            if (current.InterfaceName == name && !TypeArgsMatch(current.TypeArgs, typeArgs))
            {
                var triggeringFunc = body.OfType<FunctionDef>()
                    .FirstOrDefault(f => f.Name == current.TriggeringDunder);

                var currentArgs = FormatTypeArgs(current.TypeArgs);
                var ancestorArgs = FormatTypeArgs(typeArgs);

                AddError(context,
                    $"Type '{typeName}' would synthesize '{current.InterfaceName}<{currentArgs}>' via '{current.TriggeringDunder}', " +
                    $"but ancestor '{ancestorName}' already implements '{current.InterfaceName}<{ancestorArgs}>'. " +
                    "This creates a conflicting generic interface.",
                    triggeringFunc?.LineStart,
                    triggeringFunc?.ColumnStart,
                    code: DiagnosticCodes.Semantic.ConflictingSynthesizedInterface,
                    span: triggeringFunc?.Span);
            }
        }
    }

    /// <summary>
    /// Collects generic interfaces from a TypeSymbol's explicit Interfaces list.
    /// Extracts interface name and type arguments from GenericType interfaces.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>v0.2.x deferral (see #122):</b> This method currently returns an empty list because
    /// <see cref="TypeSymbol.Interfaces"/> stores only the interface <em>definition</em>
    /// (<see cref="TypeSymbol"/>), not the concrete instantiation (e.g., <c>IEquatable&lt;str&gt;</c>).
    /// Generic type arguments are discarded during name resolution when the interface is
    /// resolved to its <see cref="TypeSymbol"/>.
    /// </para>
    /// <para>
    /// To fix this, the data model would need to preserve type arguments on interface
    /// references — for example, by storing <c>(TypeSymbol Definition, SemanticType[] TypeArgs)</c>
    /// tuples instead of bare <c>TypeSymbol</c> entries. This is planned for v0.2.x when the
    /// generic type system is extended.
    /// </para>
    /// </remarks>
    private static List<(string Name, SemanticType[] TypeArgs)> CollectExplicitGenericInterfaces(TypeSymbol typeSymbol)
    {
        var result = new List<(string, SemanticType[])>();
        // TODO(#122): TypeSymbol.Interfaces stores only the interface definition (TypeSymbol),
        // not the instantiation (e.g., IEquatable<str>). Generic type arguments are discarded
        // during name resolution. Until the data model preserves type args, explicit generic
        // interface conflicts cannot be detected here.
        return result;
    }

    private static bool TypeArgsMatch(SemanticType[] a, SemanticType[] b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].GetDisplayName() != b[i].GetDisplayName())
                return false;
        }
        return true;
    }

    private static string FormatTypeArgs(SemanticType[] typeArgs)
    {
        return string.Join(", ", typeArgs.Select(t => t.GetDisplayName()));
    }
}
