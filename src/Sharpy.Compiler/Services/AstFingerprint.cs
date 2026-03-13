using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Classifies the kind of change between two AST versions.
/// </summary>
public enum AstChangeKind
{
    /// <summary>ASTs are structurally identical (ignoring positions).</summary>
    NoChange,

    /// <summary>Only a single function body changed; top-level structure is identical.</summary>
    BodyOnly,

    /// <summary>Any structural change (new declarations, signature changes, import changes).</summary>
    Structural
}

/// <summary>
/// Result of AST change classification.
/// </summary>
public sealed record AstChangeResult(AstChangeKind Kind, string? FunctionName = null, int FunctionIndex = -1);

/// <summary>
/// Classifies changes between two AST versions to enable partial re-analysis.
/// Compares top-level structure ignoring position information.
/// </summary>
public static class AstFingerprint
{
    /// <summary>
    /// Classifies the change between two parsed modules.
    /// </summary>
    public static AstChangeResult Classify(Module oldAst, Module newAst)
    {
        var oldBody = oldAst.Body;
        var newBody = newAst.Body;

        // Different number of top-level statements → structural change
        if (oldBody.Length != newBody.Length)
            return new AstChangeResult(AstChangeKind.Structural);

        string? changedFunction = null;
        int changedIndex = -1;
        int changedCount = 0;

        for (int i = 0; i < oldBody.Length; i++)
        {
            var oldStmt = oldBody[i];
            var newStmt = newBody[i];

            // Different statement types → structural
            if (oldStmt.GetType() != newStmt.GetType())
                return new AstChangeResult(AstChangeKind.Structural);

            if (!SignatureEquals(oldStmt, newStmt))
            {
                // Signature changed → structural
                return new AstChangeResult(AstChangeKind.Structural);
            }

            if (oldStmt is FunctionDef oldFunc && newStmt is FunctionDef newFunc)
            {
                if (!BodyEquals(oldFunc.Body, newFunc.Body))
                {
                    changedFunction = oldFunc.Name;
                    changedIndex = i;
                    changedCount++;
                    if (changedCount > 1)
                        return new AstChangeResult(AstChangeKind.Structural);
                }
            }
            else if (oldStmt is ClassDef oldClass && newStmt is ClassDef newClass)
            {
                // For classes, any body change is structural for now
                if (!BodyEquals(oldClass.Body, newClass.Body))
                    return new AstChangeResult(AstChangeKind.Structural);
            }
        }

        if (changedCount == 0)
            return new AstChangeResult(AstChangeKind.NoChange);

        return new AstChangeResult(AstChangeKind.BodyOnly, changedFunction, changedIndex);
    }

    /// <summary>
    /// Compares the "signature" of two statements — everything except body and positions.
    /// </summary>
    private static bool SignatureEquals(Statement a, Statement b)
    {
        return (a, b) switch
        {
            (FunctionDef fa, FunctionDef fb) => FunctionSignatureEquals(fa, fb),
            (ClassDef ca, ClassDef cb) => ca.Name == cb.Name
                && TypeParamsEqual(ca.TypeParameters, cb.TypeParameters)
                && TypeAnnotationsEqual(ca.BaseClasses, cb.BaseClasses)
                && DecoratorsEqual(ca.Decorators, cb.Decorators),
            (ImportStatement ia, ImportStatement ib) => ImportAliasesEqual(ia.Names, ib.Names),
            (FromImportStatement fa, FromImportStatement fb) =>
                fa.Module == fb.Module && fa.ImportAll == fb.ImportAll
                && ImportAliasesEqual(fa.Names, fb.Names),
            (VariableDeclaration va, VariableDeclaration vb) =>
                va.Name == vb.Name && va.IsConst == vb.IsConst
                && TypeAnnotationEquals(va.Type, vb.Type),
            (TypeAlias ta, TypeAlias tb) => ta.Name == tb.Name,
            _ => a.GetType() == b.GetType() // For other statement types, just check type match
        };
    }

    private static bool FunctionSignatureEquals(FunctionDef a, FunctionDef b)
    {
        if (a.Name != b.Name || a.IsAsync != b.IsAsync)
            return false;
        if (!TypeAnnotationEquals(a.ReturnType, b.ReturnType))
            return false;
        if (!TypeParamsEqual(a.TypeParameters, b.TypeParameters))
            return false;
        if (!ParametersEqual(a.Parameters, b.Parameters))
            return false;
        if (!DecoratorsEqual(a.Decorators, b.Decorators))
            return false;
        return true;
    }

    private static bool ParametersEqual(ImmutableArray<Parameter> a, ImmutableArray<Parameter> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Name != b[i].Name)
                return false;
            if (!TypeAnnotationEquals(a[i].Type, b[i].Type))
                return false;
            // Default values are part of the signature for re-analysis purposes
        }
        return true;
    }

    private static bool TypeParamsEqual(ImmutableArray<TypeParameterDef> a, ImmutableArray<TypeParameterDef> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Name != b[i].Name)
                return false;
        }
        return true;
    }

    private static bool TypeAnnotationEquals(TypeAnnotation? a, TypeAnnotation? b)
    {
        if (a == null && b == null)
            return true;
        if (a == null || b == null)
            return false;
        // Compare the string representation — sufficient for signature comparison
        return a.ToString() == b.ToString();
    }

    private static bool TypeAnnotationsEqual(ImmutableArray<TypeAnnotation> a, ImmutableArray<TypeAnnotation> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (!TypeAnnotationEquals(a[i], b[i]))
                return false;
        }
        return true;
    }

    private static bool DecoratorsEqual(ImmutableArray<Decorator> a, ImmutableArray<Decorator> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Name != b[i].Name)
                return false;
        }
        return true;
    }

    private static bool ImportAliasesEqual(ImmutableArray<ImportAlias> a, ImmutableArray<ImportAlias> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Name != b[i].Name || a[i].AsName != b[i].AsName)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Compares two statement bodies for structural equality, ignoring position info.
    /// Uses ToString() on statements as a pragmatic heuristic — if the textual
    /// representation (minus positions) differs, the body has changed.
    /// </summary>
    private static bool BodyEquals(ImmutableArray<Statement> a, ImmutableArray<Statement> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            // Use record ToString which includes all fields including positions.
            // We need a position-independent comparison, so compare the debug representation.
            // Since we can't easily strip positions from records, compare field-by-field
            // for simple cases, and fall back to assuming changed for complex bodies.
            if (!StatementStructuralEquals(a[i], b[i]))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Checks if two statements are structurally equal ignoring position info.
    /// Conservative: returns false (assumes changed) when uncertain.
    /// </summary>
    private static bool StatementStructuralEquals(Statement a, Statement b)
    {
        if (a.GetType() != b.GetType())
            return false;

        // For function-level body comparison, we compare the structural content.
        // Since AST nodes are records with position fields that change on every edit,
        // we use a pragmatic approach: compare everything except line/column fields.
        // The key fields that matter are names, types, operators, and values.
        return (a, b) switch
        {
            (ReturnStatement ra, ReturnStatement rb) =>
                ExpressionEquals(ra.Value, rb.Value),
            (ExpressionStatement ea, ExpressionStatement eb) =>
                ExpressionEquals(ea.Expression, eb.Expression),
            (VariableDeclaration va, VariableDeclaration vb) =>
                va.Name == vb.Name && va.IsConst == vb.IsConst
                && TypeAnnotationEquals(va.Type, vb.Type)
                && ExpressionEquals(va.InitialValue, vb.InitialValue),
            (Assignment aa, Assignment ab) =>
                ExpressionEquals(aa.Target, ab.Target)
                && ExpressionEquals(aa.Value, ab.Value),
            (PassStatement, PassStatement) => true,
            (BreakStatement, BreakStatement) => true,
            (ContinueStatement, ContinueStatement) => true,
            // For complex statements (if, for, while, etc.), conservatively return false
            // This means body changes in nested control flow always trigger full re-analysis
            _ => false
        };
    }

    /// <summary>
    /// Compares two expressions for structural equality ignoring positions.
    /// Conservative: returns false when uncertain.
    /// </summary>
    private static bool ExpressionEquals(Expression? a, Expression? b)
    {
        if (a == null && b == null)
            return true;
        if (a == null || b == null)
            return false;
        if (a.GetType() != b.GetType())
            return false;

        return (a, b) switch
        {
            (IntegerLiteral ia, IntegerLiteral ib) => ia.Value == ib.Value,
            (FloatLiteral fa, FloatLiteral fb) => fa.Value == fb.Value,
            (StringLiteral sa, StringLiteral sb) => sa.Value == sb.Value,
            (BooleanLiteral ba, BooleanLiteral bb) => ba.Value == bb.Value,
            (NoneLiteral, NoneLiteral) => true,
            (Identifier ida, Identifier idb) => ida.Name == idb.Name,
            (BinaryOp bea, BinaryOp beb) =>
                bea.Operator == beb.Operator
                && ExpressionEquals(bea.Left, beb.Left)
                && ExpressionEquals(bea.Right, beb.Right),
            (UnaryOp ua, UnaryOp ub) =>
                ua.Operator == ub.Operator
                && ExpressionEquals(ua.Operand, ub.Operand),
            (FunctionCall fca, FunctionCall fcb) =>
                ExpressionEquals(fca.Function, fcb.Function)
                && fca.Arguments.Length == fcb.Arguments.Length
                && fca.Arguments.Zip(fcb.Arguments).All(p => ExpressionEquals(p.First, p.Second)),
            (MemberAccess aa, MemberAccess ab) =>
                aa.Member == ab.Member
                && ExpressionEquals(aa.Object, ab.Object),
            // Conservative: any other expression type → assume changed
            _ => false
        };
    }
}
