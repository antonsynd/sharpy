using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// DecoratorValidator partial: bracket-attribute (<c>@[...]</c>) name resolution (#1427).
/// </summary>
internal partial class DecoratorValidator
{
    /// <summary>
    /// .NET namespaces the current module brings into scope by importing them, in their C#
    /// spelling. Recomputed per module in <see cref="Validate"/>.
    /// </summary>
    private IReadOnlyList<string> _importedClrNamespaces = Array.Empty<string>();

    /// <summary>
    /// Module-name prefixes the emitter treats as .NET framework namespaces — the only imports
    /// that produce a plain <c>using Namespace;</c> rather than a using-alias, and therefore the
    /// only ones that can make an unqualified attribute name resolve. Mirrors
    /// <c>RoslynEmitter.IsNetFrameworkNamespace</c>.
    /// </summary>
    private static readonly string[] NetFrameworkModulePrefixes =
    {
        "system",
        "microsoft",
        "windows",
        "xamarin",
        "mono",
        "netstandard",
    };

    /// <summary>
    /// Collects the C# namespaces the module's imports bring into scope. Deliberately
    /// over-collects (aliased and named imports are included even though the emitter turns some
    /// of them into using-aliases): every extra namespace can only make the bracket-attribute
    /// check more permissive, and a false refusal is the failure mode that matters.
    /// </summary>
    private static IReadOnlyList<string> CollectImportedClrNamespaces(Module module)
    {
        var namespaces = new List<string>();

        void AddModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName) || !IsNetFrameworkModule(moduleName))
                return;
            var converted = ConvertModuleNameToNamespace(moduleName);
            if (converted.Length > 0 && !namespaces.Contains(converted, StringComparer.Ordinal))
                namespaces.Add(converted);
        }

        foreach (var bodyStatement in module.Body)
        {
            // An import can carry decorators, in which case the parser wraps it — scan through
            // the wrapper or a decorated import silently stops counting (#1124/#1125).
            switch (bodyStatement.UnwrapDecorated())
            {
                case ImportStatement import:
                    foreach (var alias in import.Names)
                        AddModule(alias.Name);
                    break;
                case FromImportStatement fromImport:
                    AddModule(fromImport.Module);
                    break;
            }
        }

        return namespaces;
    }

    private static bool IsNetFrameworkModule(string moduleName)
    {
        var firstPart = moduleName.Split('.')[0].ToLowerInvariant();
        return Array.IndexOf(NetFrameworkModulePrefixes, firstPart) >= 0;
    }

    /// <summary>
    /// Mirrors <c>RoslynEmitter.ConvertModuleNameToNamespace</c>: "system.io" -> "System.IO".
    /// Shared with <c>UnusedImportValidator</c> (#1429) so both sides of the bracket-attribute
    /// import-use match agree on the C# spelling of an import.
    /// </summary>
    internal static string ConvertModuleNameToNamespace(string moduleName)
    {
        var parts = moduleName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(".", parts.Select(NameMangler.ToNamespacePart));
    }

    /// <summary>
    /// The C# name the emitter will write for a bracket attribute — the same per-part mangling
    /// <c>RoslynEmitter.MangleBracketPart</c> applies, joined with dots. Returns null when the
    /// decorator has no name parts (a parse error the parser has already reported).
    /// </summary>
    private static string? MangledBracketAttributeName(Decorator decorator)
    {
        if (decorator.QualifiedParts.Length == 0)
            return null;

        var parts = new string[decorator.QualifiedParts.Length];
        for (var i = 0; i < decorator.QualifiedParts.Length; i++)
        {
            var isEscaped = decorator.BacktickEscapedParts.Length > i && decorator.BacktickEscapedParts[i];
            parts[i] = NameCasing.ResolveType(decorator.QualifiedParts[i], isEscaped);
        }

        return string.Join(".", parts);
    }

    /// <summary>
    /// Refuses a bracket attribute whose name denotes no type at all (#1427).
    ///
    /// The emitted attribute name is resolved against the types this compilation declares and the
    /// CLR types the generated file can see (<see cref="ClrAttributeResolver"/> owns the
    /// reflection — validators do none). Only a name that resolves nowhere is refused; a name
    /// that resolves to something which is not an attribute class is left to C#, because the
    /// failure mode this check must not have is rejecting a program that compiles today.
    /// </summary>
    private void ValidateBracketAttributeResolves(Decorator decorator)
    {
        var mangled = MangledBracketAttributeName(decorator);
        if (mangled == null)
            return;

        if (ResolvesToDeclaredType(decorator, mangled))
            return;

        if (ClrAttributeResolver.ResolvesToClrType(mangled, _importedClrNamespaces))
        {
            // Resolves as a CLR attribute. If it resolves ONLY through one of the file's imported
            // namespaces (no bare or always-in-scope spelling does), record that namespace so
            // UnusedImportValidator counts the import that brings it into scope as used (#1429). A
            // name reachable via an always-in-scope namespace records nothing, so a redundant import
            // there is still (correctly) reported unused.
            var requiredImport = ClrAttributeResolver.RequiredImportNamespace(mangled, _importedClrNamespaces);
            if (requiredImport != null)
                Context.SemanticInfo.SetBracketAttributeResolvedNamespace(decorator, requiredImport);
            return;
        }

        var attempted = $"'{mangled}' or '{mangled}Attribute'";
        var message =
            $"Bracket attribute '@[{decorator.Name}]' names no attribute type that is in scope: " +
            $"looked for {attempted}.";

        // '@[abstract]' and its siblings are one keystroke from the Sharpy modifier decorators,
        // and bracket syntax deliberately does NOT mean the decorator (#1373) — say so, because
        // the mangled-name message alone reads as a spelling problem.
        if (AllKnownDecorators.Contains(decorator.Name))
        {
            message += $" Did you mean the '@{decorator.Name}' decorator? " +
                       "Bracket syntax is a .NET attribute pass-through, never a Sharpy modifier.";
        }
        else
        {
            message += " Check the spelling, or import the namespace that declares the attribute.";
        }

        AddError(
            message,
            decorator.LineStart,
            decorator.ColumnStart,
            code: DiagnosticCodes.Validation.UnknownBracketAttribute,
            span: decorator.Span);
    }

    /// <summary>
    /// True when the attribute name denotes a type this compilation declares or imports — a
    /// Sharpy-authored <c>Attribute</c> subclass, for instance. Both the written name and the
    /// mangled spelling are tried, since a bracket attribute may name the class either way
    /// (<c>@[my_tag_attribute]</c> and <c>@[MyTagAttribute]</c> both emit <c>MyTagAttribute</c>).
    /// </summary>
    private bool ResolvesToDeclaredType(Decorator decorator, string mangled)
    {
        foreach (var candidate in new[] { decorator.Name, mangled, mangled + "Attribute" })
        {
            if (Context.SymbolTable.LookupType(candidate) != null)
                return true;
        }

        // A qualified spelling may name a type through a module prefix the symbol table holds
        // under its bare name; the last segment is the type itself.
        var lastDot = mangled.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < mangled.Length - 1)
        {
            var lastPart = mangled.Substring(lastDot + 1);
            if (Context.SymbolTable.LookupType(lastPart) != null
                || Context.SymbolTable.LookupType(lastPart + "Attribute") != null)
            {
                return true;
            }
        }

        return false;
    }
}
