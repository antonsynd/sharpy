using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates module-level code adheres to entry point rules:
/// 1. Entry point files MUST have a main() function
/// 2. Module-level variable declarations MUST have type annotations
/// 3. No bare executable statements at module level
///
/// This validator runs early in the pipeline (Order 50) to catch structural
/// errors before other validators attempt to process invalid code.
/// </summary>
public class ModuleLevelValidator : SemanticValidatorBase
{
    public override string Name => "ModuleLevelValidator";
    public override int Order => 50; // Very early, before signature validation (150)

    private ICompilerLogger _logger = NullLogger.Instance;
    private SemanticContext _context = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _logger = context.Logger;
        _logger.LogDebug("Starting module-level validation");

        bool hasMainFunction = false;
        var executableStatements = new List<Statement>();
        var untypedVariables = new List<VariableDeclaration>();

        // First pass: categorize all top-level statements
        foreach (var stmt in module.Body)
        {
            switch (stmt)
            {
                case FunctionDef funcDef when funcDef.Name == "main":
                    hasMainFunction = true;
                    break;

                case FunctionDef:
                case ClassDef:
                case StructDef:
                case InterfaceDef:
                case EnumDef:
                case TypeAlias:
                case ImportStatement:
                case FromImportStatement:
                    // These are valid module-level declarations
                    break;

                case VariableDeclaration varDecl:
                    // Module-level variable declarations must have type annotations
                    if (varDecl.Type == null && !varDecl.IsConst)
                    {
                        untypedVariables.Add(varDecl);
                    }
                    // Const declarations without type annotation are OK (inferred from value)
                    break;

                case ExpressionStatement:
                case Assignment:
                case IfStatement:
                case WhileStatement:
                case ForStatement:
                case TryStatement:
                case AssertStatement:
                case ReturnStatement:
                case BreakStatement:
                case ContinueStatement:
                case RaiseStatement:
                case PassStatement:
                    // These are executable statements - not allowed at module level
                    executableStatements.Add(stmt);
                    break;

                default:
                    // Unknown statement type - treat as executable
                    executableStatements.Add(stmt);
                    break;
            }
        }

        // Report errors for untyped module-level variables
        foreach (var varDecl in untypedVariables)
        {
            AddError(_context,
                $"Top-level variable '{varDecl.Name}' requires a type annotation",
                varDecl.LineStart, varDecl.ColumnStart, code: DiagnosticCodes.Semantic.ModuleLevelNoTypeAnnotation,
                span: varDecl.Span);
        }

        // Report errors for executable statements at module level when:
        // 1. There's a main() function - bare statements should be inside main()
        // 2. This is a non-entry-point (library) module - bare statements would be ignored anyway
        //
        // For entry-point modules WITHOUT main(), we allow bare statements for backward compatibility
        // (the code generator will synthesize a Main() and wrap them).
        bool shouldRejectExecutableStatements = hasMainFunction || !_context.IsEntryPoint;

        if (shouldRejectExecutableStatements && executableStatements.Count > 0)
        {
            foreach (var stmt in executableStatements)
            {
                AddError(_context,
                    "Executable statements are not allowed at module level",
                    stmt.LineStart, stmt.ColumnStart, code: DiagnosticCodes.Semantic.ModuleLevelExecutableStatement,
                    span: stmt.Span);
            }
        }

        // Entry point files must have a main() function
        if (_context.IsEntryPoint && !hasMainFunction)
        {
            AddError(_context,
                "Entry point file requires a 'main()' function",
                module.LineStart, module.ColumnStart, code: DiagnosticCodes.Validation.MissingMainFunction,
                span: module.Span);
        }
    }
}
