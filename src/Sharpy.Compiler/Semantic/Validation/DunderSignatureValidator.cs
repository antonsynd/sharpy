using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

internal class DunderSignatureValidator : SemanticValidatorBase
{
    public override string Name => "DunderSignatureValidator";
    public override int Order => 407;

    public override void Validate(Module module, SemanticContext context)
    {
        var visitor = new ClassCollector();
        visitor.Visit(module);

        foreach (var classDef in visitor.Classes)
            CheckDunders(context, classDef);
    }

    private void CheckDunders(SemanticContext context, ClassDef classDef)
    {
        var dunders = new Dictionary<string, List<FunctionDef>>();
        foreach (var stmt in classDef.Body)
        {
            if (stmt is FunctionDef fd && DunderDetector.IsDunderMethod(fd.Name))
            {
                if (!dunders.TryGetValue(fd.Name, out var list))
                {
                    list = new List<FunctionDef>();
                    dunders[fd.Name] = list;
                }
                list.Add(fd);
            }
        }

        foreach (var (name, overloads) in dunders)
        {
            if (overloads.Count < 2)
                continue;

            var seenSignatures = new Dictionary<string, FunctionDef>();
            foreach (var func in overloads)
            {
                var sig = KeySignature(func);
                if (seenSignatures.TryGetValue(sig, out var first))
                {
                    AddError(
                        context,
                        $"Duplicate CLR-mapped signature for '{name}' — both at line {first.LineStart} and line {func.LineStart} map to the same C# parameter types",
                        func.LineStart, func.ColumnStart,
                        code: DiagnosticCodes.ValidationOverflow.DuplicateDunderSignature);
                }
                else
                {
                    seenSignatures[sig] = func;
                }
            }
        }
    }

    private static string KeySignature(FunctionDef func)
    {
        var paramTypes = func.Parameters
            .Where(p =>
                !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.Name, PythonNames.Cls, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Type?.Name ?? "object");
        return string.Join(",", paramTypes);
    }

    private class ClassCollector : AstVisitor
    {
        public List<ClassDef> Classes { get; } = new();

        public override void VisitClassDef(ClassDef node)
        {
            Classes.Add(node);
            base.VisitClassDef(node);
        }
    }
}
