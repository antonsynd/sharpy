using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates that no two overloads in ANY overload group — module functions, methods, struct
/// methods, <c>__init__</c>, dunders — map to the same C# parameter types (#1721). The key is
/// <see cref="ClrSignatureKey"/>: resolved types' <see cref="SemanticType.CanonicalKey"/> with
/// the distinctions C# erases removed, so <c>float</c>/<c>double</c> (one <c>float64</c>),
/// <c>tuple[a: int]</c>/<c>tuple[int]</c>, <c>str | None</c>/<c>str</c> and
/// <c>LiteralString</c>/<c>str</c> collide, while <c>int</c>/<c>int?</c>, <c>int</c>/<c>int | None</c>
/// and same-named classes from two modules stay distinct.
/// Absorbs the former <c>ConstructorOverloadValidator</c> — <c>__init__</c> is a dunder.
/// </summary>
internal class DunderSignatureValidator : SemanticValidatorBase
{
    public override string Name => "DunderSignatureValidator";
    public override int Order => 407;

    public override void Validate(Module module, SemanticContext context)
    {
        var visitor = new ClassCollector();
        visitor.Visit(module);

        foreach (var classDef in visitor.Classes)
        {
            var typeSymbol = context.LookupDeclaredType(classDef, classDef.Name);
            if (typeSymbol == null)
                continue;
            CheckAllOverloadGroups(context, typeSymbol);
        }

        foreach (var structDef in visitor.Structs)
        {
            var typeSymbol = context.LookupDeclaredType(structDef, structDef.Name);
            if (typeSymbol == null)
                continue;
            CheckAllOverloadGroups(context, typeSymbol);
        }

        CheckModuleFunctions(context, module);
    }

    private void CheckAllOverloadGroups(SemanticContext context, TypeSymbol typeSymbol)
    {
        var groups = new Dictionary<string, List<FunctionSymbol>>();
        foreach (var method in typeSymbol.Methods)
        {
            if (method.Name == DunderNames.Init)
                continue;
            if (!groups.TryGetValue(method.Name, out var list))
            {
                list = new List<FunctionSymbol>();
                groups[method.Name] = list;
            }
            list.Add(method);
        }

        foreach (var ctor in typeSymbol.Constructors)
        {
            if (!groups.TryGetValue(DunderNames.Init, out var list))
            {
                list = new List<FunctionSymbol>();
                groups[DunderNames.Init] = list;
            }
            list.Add(ctor);
        }

        foreach (var (name, overloads) in groups)
        {
            if (overloads.Count < 2)
                continue;
            CheckGroupForClrCollisions(context, typeSymbol.Name, name, overloads);
        }
    }

    private void CheckModuleFunctions(SemanticContext context, Module module)
    {
        var seen = new HashSet<string>();
        foreach (var stmt in module.Body)
        {
            if (stmt is FunctionDef funcDef && seen.Add(funcDef.Name))
            {
                var overloads = context.SymbolTable.LookupFunctionOverloads(funcDef.Name);
                if (overloads == null || overloads.Count < 2)
                    continue;
                CheckGroupForClrCollisions(context, "<module>", funcDef.Name, overloads);
            }
        }
    }

    private void CheckGroupForClrCollisions(
        SemanticContext context, string hostName, string groupName,
        List<FunctionSymbol> overloads)
    {
        var seen = new Dictionary<string, FunctionSymbol>();
        foreach (var func in overloads)
        {
            var clrKey = Semantic.ClrSignatureKey.Of(func);
            if (seen.TryGetValue(clrKey, out var first))
            {
                AddError(
                    context,
                    $"Duplicate CLR-mapped signature for '{groupName}' in '{hostName}' — " +
                    $"both at line {first.DeclarationLine} and line {func.DeclarationLine} " +
                    $"map to the same C# parameter types",
                    func.DeclarationLine, func.DeclarationColumn,
                    code: DiagnosticCodes.ValidationOverflow.DuplicateDunderSignature);
            }
            else
            {
                seen[clrKey] = func;
            }
        }
    }


    private class ClassCollector : AstVisitor
    {
        public List<ClassDef> Classes { get; } = new();
        public List<StructDef> Structs { get; } = new();

        public override void VisitClassDef(ClassDef node)
        {
            Classes.Add(node);
            base.VisitClassDef(node);
        }

        public override void VisitStructDef(StructDef node)
        {
            Structs.Add(node);
            base.VisitStructDef(node);
        }
    }
}
