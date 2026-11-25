using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;

namespace Sharpy.BlastRadius.Probes;

/// <summary>
/// Every store to a member in one file, so a probe can tell a field that is assigned once in
/// <c>__init__</c> (it could be <c>let</c>) from one that is written again (it has to be <c>var</c>).
/// </summary>
internal sealed class StoreCensus
{
    private readonly HashSet<(TypeSymbol Owner, string Member)> _writtenAgain = new();

    public bool WrittenAgain(TypeSymbol owner, string member) => _writtenAgain.Contains((owner, member));

    public static StoreCensus Collect(ProbeInput input)
    {
        var census = new StoreCensus();
        Walk(input.Ast, enclosing: null, assigned: null, inLoop: false);
        return census;

        // `assigned` is non-null while walking the straight-line body of an __init__: the fields a
        // plain `self.f = …` has already assigned on the path to this node. The first such store on
        // a path is the assignment a `let` field allows; any other store means the field is `var`.
        void Walk(Node node, TypeSymbol? enclosing, HashSet<(TypeSymbol, string)>? assigned, bool inLoop)
        {
            switch (node)
            {
                case ClassDef classDef:
                    enclosing = input.Types.SymbolFor(classDef, classDef.Name, input.Symbols);
                    assigned = null;
                    break;
                case StructDef:
                    enclosing = null;
                    assigned = null;
                    break;
                case FunctionDef function:
                    // A def nested in __init__ runs later, or more than once: not the first assignment.
                    assigned = assigned == null && function.Name == DunderNames.Init ? new() : null;
                    inLoop = false;
                    break;
                case LambdaExpression:
                    assigned = null;
                    break;
                case ForStatement loop:
                    foreach (var member in MemberTargets(loop.Target))
                        Record(member, plain: false, enclosing, assigned, inLoop);
                    inLoop = true;
                    break;
                case WhileStatement:
                    inLoop = true;
                    break;
                case Assignment assignment:
                    foreach (var member in MemberTargets(assignment.Target))
                        Record(member, assignment.Operator == AssignmentOperator.Assign, enclosing, assigned, inLoop);
                    break;

                // Branches are alternatives: a field assigned once on each is assigned once.
                case IfStatement branch when assigned != null:
                    Walk(branch.Test, enclosing, assigned, inLoop);
                    var arms = new List<IEnumerable<Node>> { branch.ThenBody };
                    arms.AddRange(branch.ElifClauses.Select(e => e.Body.Cast<Node>().Prepend(e.Test)));
                    arms.Add(branch.ElseBody);
                    WalkAlternatives(arms, enclosing, assigned, inLoop);
                    return;
                case MatchStatement match when assigned != null:
                    Walk(match.Scrutinee, enclosing, assigned, inLoop);
                    WalkAlternatives(match.Cases.Select(c => c.Body.Cast<Node>()), enclosing, assigned, inLoop);
                    return;
            }

            foreach (var child in node.GetChildNodes())
                Walk(child, enclosing, assigned, inLoop);
        }

        void WalkAlternatives(IEnumerable<IEnumerable<Node>> arms, TypeSymbol? enclosing,
            HashSet<(TypeSymbol, string)> assigned, bool inLoop)
        {
            var after = new HashSet<(TypeSymbol, string)>(assigned);
            foreach (var arm in arms)
            {
                var onThisArm = new HashSet<(TypeSymbol, string)>(assigned);
                foreach (var node in arm)
                    Walk(node, enclosing, onThisArm, inLoop);
                after.UnionWith(onThisArm);
            }

            assigned.UnionWith(after);
        }

        void Record(MemberAccess member, bool plain, TypeSymbol? enclosing,
            HashSet<(TypeSymbol, string)>? assigned, bool inLoop)
        {
            var isSelf = member.Object is Identifier { Name: PythonNames.Self };
            // The type the store goes through: the class it is written in for `self.f`, the
            // receiver's type for `other.f`. The checker records a resolution only for a store
            // through a type name (`Counter.total = 1`).
            var through = input.Semantic.GetMemberAccessResolution(member)?.Owner
                ?? (isSelf ? enclosing : SymbolOf(input.Semantic.GetExpressionType(member.Object)));
            if (through == null)
                return;
            // The member belongs to the class that declares it, which may be a base class.
            var owner = TypeHierarchyService.FirstDeclaringType(through, member.Member) ?? through;
            // Only a class's own __init__ makes the first assignment of its field: a store from
            // a subclass's __init__ comes after the base __init__ has run.
            if (plain && isSelf && ReferenceEquals(owner, enclosing) && assigned != null && !inLoop
                && assigned.Add((owner, member.Member)))
                return;
            census._writtenAgain.Add((owner, member.Member));
        }
    }

    private static TypeSymbol? SymbolOf(SemanticType? type) => type switch
    {
        NullableType nullable => SymbolOf(nullable.UnderlyingType),
        GenericType generic => generic.GenericDefinition,
        _ => type?.DeclaringSymbol,
    };

    private static IEnumerable<MemberAccess> MemberTargets(Expression target)
        => SyntaxFacts.TargetLeaves(target).OfType<MemberAccess>();
}
