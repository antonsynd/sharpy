using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// Standing inventory of hand-rolled AST/IrNode dispatch switches in the compiler and LSP.
/// Every site must carry a rostered justification; a new unguarded dispatch
/// fails this test, preventing the #1694/#1709 vacuity class from recurring.
///
/// The scan is a Roslyn COMPILATION (plan-950124 Phase 0): it resolves every switch
/// scrutinee's type and reports those deriving from <c>Node</c> or <c>IrNode</c>,
/// regardless of scrutinee spelling. LSP sites are keyed "Sharpy.Lsp/path::Type.Method".
/// The old name-scoped identifier scan is retired.
///
/// Scope: AST-typed and IrNode-typed scrutinees in both src/Sharpy.Compiler and
/// src/Sharpy.Lsp (Design Decision 2). Roster matching is exact-key, both directions:
/// an unrostered site fails, and a phantom roster row (site gone) fails —
/// entries drain on fix.
/// </summary>
public class DispatchSiteInventoryTests
{
    private readonly ITestOutputHelper _output;

    public DispatchSiteInventoryTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Maps "relative_path::Type.Method" → justification category. Categories:
    ///   guarded-by:&lt;TestClass&gt;      — a totality test asserts scan-vs-roster for the site
    ///   documented-by-design:&lt;where&gt; — deliberately partial dispatch, contract recorded there
    ///   walker-default-contract      — validator whose default-ignore is contractual
    ///   refusal-net:&lt;TestClass&gt;      — a conformance net covers the dispatch
    ///   pending-guard:#&lt;issue&gt;       — no guard/contract/net yet; the row cites its tracking
    ///                                  issue and drains when one lands (allowlist discipline)
    /// </summary>
    private static readonly Dictionary<string, string> Roster = new()
    {
        // ══════════════════════════════════════════════════════════════════════
        // guarded-by: a totality test asserts scan-vs-roster for the method
        // ══════════════════════════════════════════════════════════════════════

        // AstVisitor — visitor pattern over every Node kind
        ["Parser/Ast/AstVisitor.cs::AstVisitor.Visit"] = "guarded-by:AstVisitorTotalityTests",
        ["Parser/Ast/AstVisitor.cs::AstVisitor`1.Visit"] = "guarded-by:AstVisitorTotalityTests",

        // ControlFlowGraphBuilder — CFG construction
        ["Analysis/ControlFlow/ControlFlowGraphBuilder.cs::ControlFlowGraphBuilder.BuildStatement"] = "guarded-by:CfgStatementTotalityTests",
        ["Analysis/ControlFlow/ControlFlowGraphBuilder.cs::ControlFlowGraphBuilder.CollectPatternBindingKeysInto"] = "guarded-by:CfgPatternBindingTotalityTests",

        // ExecutionOrderAnalyzer — identifier/declaration collection
        ["Semantic/ExecutionOrderAnalyzer.cs::ExecutionOrderAnalyzer.CollectReferencedIdentifiers"] = "guarded-by:ExecutionOrderAnalyzerTotalityTests",
        ["Semantic/ExecutionOrderAnalyzer.cs::ExecutionOrderAnalyzer.CollectDeclarationNames"] = "guarded-by:ExecutionOrderAnalyzerTotalityTests",

        // CodeGenInfoComputer — code generation info over module/type members
        ["Semantic/CodeGenInfoComputer.cs::CodeGenInfoComputer.ComputeForModule"] = "guarded-by:CodeGenInfoComputerTotalityTests",
        ["Semantic/CodeGenInfoComputer.cs::CodeGenInfoComputer.DetectModuleLevelCollisions"] = "guarded-by:CodeGenInfoComputerTotalityTests",
        ["Semantic/CodeGenInfoComputer.cs::CodeGenInfoComputer.ProcessTypeMembers"] = "guarded-by:CodeGenInfoComputerTotalityTests",
        ["Semantic/CodeGenInfoComputer.cs::CodeGenInfoComputer.ProcessModuleLevelDeclarations"] = "guarded-by:CodeGenInfoComputerTotalityTests",
        ["Semantic/CodeGenInfoComputer.cs::CodeGenInfoComputer.EnumerateMemberNames"] = "guarded-by:CodeGenInfoComputerTotalityTests",
        ["Semantic/CodeGenInfoComputer.cs::CodeGenInfoComputer.FindMemberPosition"] = "guarded-by:CodeGenInfoComputerTotalityTests",

        // ExhaustivenessHelper — pattern exhaustiveness/irrefutability
        ["Shared/ExhaustivenessHelper.cs::ExhaustivenessHelper.CollectCoveredCases"] = "guarded-by:ExhaustivenessHelperTotalityTests",
        ["Shared/ExhaustivenessHelper.cs::ExhaustivenessHelper.IsIrrefutable"] = "guarded-by:ExhaustivenessHelperTotalityTests",

        // ModuleLoader — symbol extraction
        ["Semantic/ModuleLoader.cs::ModuleLoader.ExtractExportedSymbol"] = "guarded-by:ModuleLoaderTotalityTests",
        ["Semantic/ModuleLoader.cs::ModuleLoader.CreateStubModuleInfo"] = "guarded-by:ModuleLoaderTotalityTests",

        // NameResolver — declaration resolution
        ["Semantic/NameResolver.Declarations.cs::NameResolver.ResolveDeclaration"] = "guarded-by:NameResolverDeclarationsTotalityTests",
        ["Semantic/NameResolver.Declarations.cs::NameResolver.ResolveNestedTypeDeclaration"] = "guarded-by:NameResolverDeclarationsTotalityTests",

        // NarrowingFlowAnalysis — narrowing recognizers
        ["Analysis/ControlFlow/NarrowingFlowAnalysis.cs::NarrowingConditionInterpreter.Recognize"] = "guarded-by:NarrowingFlowAnalysisTotalityTests",
        ["Analysis/ControlFlow/NarrowingFlowAnalysis.cs::NarrowingConditionInterpreter.RecognizeLeaf"] = "guarded-by:NarrowingFlowAnalysisTotalityTests",
        ["Analysis/ControlFlow/NarrowingFlowAnalysis.cs::NarrowingFlowAnalysis.CollectAssignedKeys"] = "guarded-by:NarrowingFlowAnalysisTotalityTests",

        // ══════════════════════════════════════════════════════════════════════
        // documented-by-design: deliberately partial dispatch, contract at cited location
        // ══════════════════════════════════════════════════════════════════════

        // IrNode rewriters — the IrNode kind set is closed by design
        ["Lowering/IrTreeRewriter.cs::IrTreeRewriter.RewriteNode"] = "documented-by-design:Parser/Ast/Types.cs:~15-28",
        ["Lowering/Passes/ComprehensionFusionPass.cs::ComprehensionFusionPass.RewriteNode"] = "documented-by-design:Parser/Ast/Types.cs:~15-28",
        ["Lowering/Passes/ConstFoldPass.cs::ConstFoldPass.RewriteNode"] = "documented-by-design:Parser/Ast/Types.cs:~15-28",
        ["Lowering/Passes/StackCollectionsPass.cs::StackCollectionsPass.RewriteNode"] = "documented-by-design:Parser/Ast/Types.cs:~15-28",

        // CheckExpression — documented HandleUnrecognizedExpression default
        ["Semantic/TypeChecker.Expressions.cs::TypeChecker.CheckExpression"] = "documented-by-design:HandleUnrecognizedExpression-default",

        // ══════════════════════════════════════════════════════════════════════
        // walker-default-contract: validator whose default-ignore is contractual
        // ══════════════════════════════════════════════════════════════════════

        ["Semantic/Validation/DecoratorValidator.cs::DecoratorValidator.IsCompileTimeConstant"] = "walker-default-contract",
        ["Semantic/Validation/DefaultParameterValidator.cs::DefaultParameterValidator.CollectIdentifierNamesInto"] = "walker-default-contract",
        ["Semantic/Validation/DefaultParameterValidator.cs::DefaultParameterValidator.IsCompileTimeConstant"] = "walker-default-contract",
        ["Semantic/Validation/DefaultParameterValidator.cs::DefaultParameterValidator.IsMutableDefault"] = "walker-default-contract",
        ["Semantic/Validation/EqualityContractValidator.cs::EqualityContractValidator.Validate"] = "walker-default-contract",
        ["Semantic/Validation/EventValidator.cs::EventValidator.TypeStatementName"] = "walker-default-contract",
        ["Semantic/Validation/EventValidator.cs::EventValidator.ValidateTypeStatement"] = "walker-default-contract",
        ["Semantic/Validation/FinalFieldValidator.cs::FinalFieldValidator.GetChildStatements"] = "walker-default-contract",
        ["Semantic/Validation/FinalFieldValidator.cs::FinalFieldValidator.ValidateModuleStatement"] = "walker-default-contract",
        ["Semantic/Validation/GeneratorValidator.cs::GeneratorValidator.Validate"] = "walker-default-contract",
        ["Semantic/Validation/InterfaceConflictValidator.cs::InterfaceConflictValidator.Validate"] = "walker-default-contract",
        ["Semantic/Validation/MatchArmOrderValidator.cs::MatchArmOrderValidator.CoversItsRecordedType"] = "walker-default-contract",
        ["Semantic/Validation/MatchArmOrderValidator.cs::MatchArmOrderValidator.GetPatternRecordedType"] = "walker-default-contract",
        ["Semantic/Validation/MatchArmOrderValidator.cs::MatchArmOrderValidator.IsTypeTotalPattern"] = "walker-default-contract",
        ["Semantic/Validation/ModuleLevelValidator.cs::ModuleLevelValidator.Validate"] = "walker-default-contract",
        ["Semantic/Validation/PropertyValidator.cs::PropertyValidator.GetChildStatements"] = "walker-default-contract",
        ["Semantic/Validation/SignatureValidator.cs::SignatureValidator.ValidateTopLevelStatement"] = "walker-default-contract",
        ["Semantic/Validation/UnusedVariableValidator.cs::UnusedVariableValidator.CollectDefinitionsFromPattern"] = "walker-default-contract",
        ["Semantic/Validation/UnusedVariableValidator.cs::UnusedVariableValidator.CollectFromStatement"] = "walker-default-contract",
        ["Semantic/Validation/VarianceValidator.cs::VarianceValidator.Validate"] = "walker-default-contract",
        ["Semantic/Validation/VarianceValidator.cs::VarianceValidator.WalkFunctionsForVariance"] = "walker-default-contract",
        // New walker-default-contract sites found by typed census
        ["Semantic/Validation/DecoratorValidator.BracketAttributes.cs::DecoratorValidator.CollectImportedClrNamespaces"] = "walker-default-contract",
        ["Semantic/Validation/UnusedImportValidator.cs::UnusedImportValidator.Validate"] = "walker-default-contract",
        ["Semantic/Validation/AbstractMemberValidator.cs::AbstractMemberValidator.ValidateClass"] = "walker-default-contract",
        ["Semantic/Validation/LocalNameCollisionValidator.cs::LocalNameCollisionValidator.DeclareTarget"] = "walker-default-contract",
        ["Semantic/Validation/NamingConventionValidator.cs::NamingConventionValidator.CheckForTarget"] = "walker-default-contract",

        // ══════════════════════════════════════════════════════════════════════
        // refusal-net: a named conformance/behavioral net covers the dispatch
        // ══════════════════════════════════════════════════════════════════════

        // Emitter dispatch — FileBasedIntegrationTests covers via running fixtures
        ["CodeGen/RoslynEmitter.ClassMembers.cs::RoslynEmitter.GenerateClassMembers"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.ClassMembers.cs::RoslynEmitter.GenerateInterfaceMembers"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Expressions.Access.Calls.cs::RoslynEmitter.IsMethodGroup"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Expressions.Access.Calls.cs::RoslynEmitter.IsMethodGroupOrLambda"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Expressions.cs::RoslynEmitter.GenerateExpressionCore"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Expressions.Literals.cs::RoslynEmitter.DeriveExpressionText"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.ModuleClass.cs::RoslynEmitter.GenerateModuleMembers"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.ModuleClass.cs::RoslynEmitter.GenerateParametrizeMemberDataProperties"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.ModuleClass.cs::RoslynEmitter.GenerateStatement"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Operators.cs::RoslynEmitter.CollectReferencedIdentifiers"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Operators.cs::RoslynEmitter.ContainsSuperExpressionInExpression"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Operators.cs::RoslynEmitter.ContainsSuperExpressionInStatement"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Operators.cs::RoslynEmitter.TransformStatementForLoopElse"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Patterns.cs::RoslynEmitter.GenerateMatchPattern"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Statements.Assignments.cs::RoslynEmitter.IsRepeatableOperand"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Statements.cs::RoslynEmitter.GenerateBodyStatements"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Statements.cs::RoslynEmitter.IsCompileTimeLiteral"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.TypeDeclarations.cs::RoslynEmitter.GenerateAttributeArgumentExpression"] = "refusal-net:FileBasedIntegrationTests",
        ["Parser/Parser.cs::Parser.ParseDecoratedStatement"] = "refusal-net:FileBasedIntegrationTests",
        ["Parser/Parser.Primaries.cs::Parser.ContainsPlaceholderIdentifier"] = "refusal-net:FileBasedIntegrationTests",
        ["Parser/Parser.Primaries.cs::Parser.ReplacePlaceholders"] = "refusal-net:FileBasedIntegrationTests",
        ["Pretty/UnparseVisitor.cs::UnparseVisitor.GetExpressionPrecedence"] = "refusal-net:UnparseIdempotencePropertyTests",
        ["Semantic/IntegerConstantEvaluator.cs::IntegerConstantEvaluator.TryGetConstantInteger"] = "refusal-net:IntegerConstantEvaluatorTests",
        // New emitter sites found by typed census
        ["CodeGen/RoslynEmitter.Expressions.Comprehensions.cs::RoslynEmitter.GenerateDictSpreadComprehension"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Expressions.Comprehensions.cs::RoslynEmitter.GenerateImperativeComprehension"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Expressions.Comprehensions.cs::RoslynEmitter.BindComprehensionLoopTarget"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Expressions.Comprehensions.cs::RoslynEmitter.TargetBoundNames"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Statements.Assignments.cs::RoslynEmitter.GenerateStore"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Statements.ControlFlow.cs::RoslynEmitter.GenerateAssertThrowsStatements"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Statements.ControlFlow.cs::RoslynEmitter.GenerateTestAssert"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Statements.cs::RoslynEmitter.TryGetUnittestAssertionName"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Statements.cs::RoslynEmitter.IsAssertAlmostEqualCall"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Statements.cs::RoslynEmitter.IsAssertCountEqualCall"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.Statements.cs::RoslynEmitter.IsAssertRegexCall"] = "refusal-net:FileBasedIntegrationTests",
        ["CodeGen/RoslynEmitter.TypeDeclarations.cs::RoslynEmitter.GenerateParametrizeAttributes"] = "refusal-net:FileBasedIntegrationTests",
        // Callee-shape resolvers — refusal via metamorphic ParensWrapCalleeTransform
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.CalleeDenotesOverloadSet"] = "refusal-net:MetamorphicCorpusSweepTests",
        ["Semantic/TypeChecker.Expressions.Access.Calls.Overloads.cs::TypeChecker.ResolveReferencedCallableOverloads"] = "refusal-net:MetamorphicCorpusSweepTests",
        // Semantic callee/reference helpers
        ["Semantic/GenericReferenceResolver.cs::TypeChecker.LookupNestedTypeSymbol"] = "refusal-net:FileBasedIntegrationTests",
        ["Semantic/GenericReferenceResolver.cs::TypeChecker.LookupQualifierTypeSymbol"] = "refusal-net:FileBasedIntegrationTests",
        // Lowering pass — dispatch over AST/IrNode types in lowering
        ["Lowering/LoweringPass.cs::LoweringPass.LowerChildren"] = "refusal-net:FileBasedIntegrationTests",
        ["Lowering/LoweringPass.cs::LoweringPass.LowerExpression"] = "refusal-net:FileBasedIntegrationTests",
        ["Lowering/Passes/ConstFoldPass.cs::ConstFoldPass.TryFoldExpression"] = "refusal-net:FileBasedIntegrationTests",
        // Parser internals
        ["Parser/Parser.cs::Parser.DelSteerFor"] = "refusal-net:FileBasedIntegrationTests",
        ["Parser/AstDumper.cs::AstDumper.DumpComprehensionClause"] = "refusal-net:FileBasedIntegrationTests",
        // Import resolution
        ["Semantic/ImportResolver.ModuleLoading.cs::ImportResolver.ResolveModuleImports"] = "refusal-net:FileBasedIntegrationTests",
        // Package resolution
        ["Semantic/PackageResolver.cs::PackageResolver.ResolvePackage"] = "refusal-net:FileBasedIntegrationTests",

        // ══════════════════════════════════════════════════════════════════════
        // pending-guard: no guard, no documented contract, no named net
        // ══════════════════════════════════════════════════════════════════════

        // --- Original #1716 pending-guard rows ---
        ["Project/GeneratorContextBuilder.cs::GeneratorContextBuilder.ExtractLiteralValue"] = "pending-guard:#1716",
        ["Project/ProjectCompiler.Generators.cs::ProjectCompiler.IntegrateGeneratedSource"] = "pending-guard:#1716",
        ["Semantic/ModuleLoader.cs::ModuleLoader.ExtractNestedTypes"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.cs::TypeChecker.ReferencesUnfoldedConst"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.DescribeTypeOperand"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.IsLiteralStringExpression"] = "guarded-by:IsLiteralStringExpressionTotalityTests",
        ["Semantic/TypeChecker.Expressions.Access.Lambdas.cs::TypeChecker.InferParamTypesFromSubExpression"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Lambdas.cs::TypeChecker.TryResolveExpressionType"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Statements.cs::TypeChecker.CheckDeferBodyControlFlow"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Statements.Patterns.cs::TypeChecker.CheckPattern"] = "pending-guard:#1716",
        ["Services/CompilerInvariants.cs::CompilerInvariants.WarnIfUnknownTypes"] = "pending-guard:#1716",
        // ContainsWalrusExpression — drained: switch replaced by structural descendant walk (Phase 3a)
        ["Shared/AstHelper.cs::AstHelper.ExtractNarrowingKey"] = "pending-guard:#1716",
        ["Shared/ExhaustivenessHelper.cs::ExhaustivenessHelper.DescribeIrrefutable"] = "pending-guard:#1716",

        // --- New pending-guard rows found by typed census (residue issue) ---
        ["Analysis/ControlFlow/ControlFlowGraphBuilder.cs::ControlFlowGraphBuilder.BuildTry"] = "pending-guard:#1716",
        ["Analysis/ControlFlow/ControlFlowGraphBuilder.cs::ControlFlowGraphBuilder.CollectBindingKeysInto"] = "pending-guard:#1716",
        ["Analysis/ControlFlow/DefiniteAssignmentAnalysis.cs::DefiniteAssignmentAnalysis.CollectAssignedNames"] = "pending-guard:#1716",
        ["Analysis/ControlFlow/DefiniteAssignmentAnalysis.cs::DefiniteAssignmentAnalysis.CollectTargetReads"] = "pending-guard:#1716",
        ["Analysis/ControlFlow/NarrowingFlowAnalysis.cs::NarrowingConditionInterpreter.DescribeTupleTypeExpression"] = "pending-guard:#1716",
        ["Analysis/ControlFlow/NarrowingFlowAnalysis.cs::NarrowingConditionInterpreter.DescribeTypeExpression"] = "pending-guard:#1716",
        ["CodeGen/RoslynEmitter.ClassMembers.LruCache.cs::RoslynEmitter.GetLruCacheMaxSize"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.cs::TypeChecker.CheckStatementCore"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Definitions.cs::TypeChecker.DetectGeneratorAttributes"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Definitions.cs::TypeChecker.ExtractCacheConfig"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.MarkTypeFactoryArguments"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.RecordIterableArgumentMarks"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.TakesContextualCollectionType"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.TryResolveGenericTypeSymbolFromIndexObject"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.ValidateClosedExtensionArguments"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.ValidateSelectedGenericOverloadArguments"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Calls.Overloads.cs::TypeChecker.DescribeMemberPath"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Calls.Overloads.cs::TypeChecker.DescribeReference"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.cs::TypeChecker.ClassifyListBacking"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.cs::TypeChecker.TryFlattenDottedName"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Access.Lambdas.cs::TypeChecker.TryInferLambdaParamTypesFromBody"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Expressions.Literals.cs::TypeChecker.CheckComprehensionClauses"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Statements.cs::ReassignmentFinder.TargetBindsName"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Statements.Patterns.cs::TypeChecker.CollectPatternBindingNames"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Utilities.cs::TypeChecker.GetAssignmentTargetDescription"] = "pending-guard:#1716",
        ["Semantic/TypeChecker.Utilities.cs::TypeChecker.IsValidAssignmentTarget"] = "pending-guard:#1716",
        ["Semantic/Validation/DecoratorValidator.Caching.cs::DecoratorValidator.ValidateLruCacheMaxSizeValue"] = "pending-guard:#1716",
        ["Semantic/Validation/EventValidator.cs::EventValidator.EnumerateAllEvents"] = "pending-guard:#1716",
        ["Semantic/Validation/EventValidator.cs::EventValidator.ValidateInterfaceEvents"] = "pending-guard:#1716",
        ["Semantic/Validation/EventValidator.cs::EventValidator.ValidateTypeBody"] = "pending-guard:#1716",
        ["Semantic/Validation/FinalFieldValidator.cs::FinalFieldValidator.ValidateTypeBody"] = "pending-guard:#1716",
        ["Semantic/Validation/MustUseValidator.cs::MustUseValidator.ElidedMethodGroupMessage"] = "pending-guard:#1716",
        ["Semantic/Validation/NestedTypeIndex.cs::NestedTypeIndex.TypeDeclarationOf"] = "pending-guard:#1716",
        ["Semantic/Validation/PropertyValidator.cs::PropertyValidator.EnumerateAllProperties"] = "pending-guard:#1716",
        ["Semantic/Validation/PropertyValidator.cs::PropertyValidator.ValidateTypeBody"] = "pending-guard:#1716",
        ["Semantic/Validation/PropertyValidator.cs::PropertyValidator.ValidateTypeStatement"] = "pending-guard:#1716",
        ["Semantic/NameResolver.Declarations.cs::NameResolver.GetAccessLevel"] = "pending-guard:#1716",
        ["Semantic/NameResolver.Declarations.cs::NameResolver.RefuseBuiltinTypeNameShadowing"] = "pending-guard:#1716",
        ["Project/ProjectCompiler.Generators.cs::ProjectCompiler.GetDeclarationName"] = "pending-guard:#1716",
        ["Services/ReplSession.cs::ReplSession.IsModuleLevelStatement"] = "pending-guard:#1716",
        ["Shared/AssertRaisesForm.cs::AssertRaisesForm.NamesTheMarker"] = "pending-guard:#1716",
        ["Shared/AstHelper.cs::AstHelper.ExtractIndexComponentKey"] = "pending-guard:#1716",
        ["Pretty/StructuralEqualityComparer.cs::StructuralEqualityComparer.Equals"] = "pending-guard:#1716",
        ["Pretty/UnparseVisitor.cs::UnparseVisitor.NeedsTrailingCommaInParens"] = "pending-guard:#1716",
        ["Lowering/Passes/ComprehensionFusionPass.cs::ComprehensionFusionPass.QualifiesForProductPreallocation"] = "pending-guard:#1716",

        // ══════════════════════════════════════════════════════════════════════
        // LSP sites — all keyed with "Sharpy.Lsp/" prefix
        // ══════════════════════════════════════════════════════════════════════

        // Cursor resolvers — walker-default: unknown node under cursor → no result
        ["Sharpy.Lsp/Handlers/CallHierarchyPrepareHandler.cs::SharpyCallHierarchyPrepareHandler.ResolveSymbol"] = "walker-default-contract",
        ["Sharpy.Lsp/Handlers/DefinitionHandler.cs::SharpyDefinitionHandler.ResolveSymbol"] = "walker-default-contract",
        ["Sharpy.Lsp/Handlers/ImplementationHandler.cs::SharpyImplementationHandler.ResolveSymbol"] = "walker-default-contract",
        ["Sharpy.Lsp/Handlers/TypeDefinitionHandler.cs::SharpyTypeDefinitionHandler.ResolveType"] = "walker-default-contract",
        ["Sharpy.Lsp/Handlers/TypeHierarchyPrepareHandler.cs::SharpyTypeHierarchyPrepareHandler.ResolveTypeSymbol"] = "walker-default-contract",
        ["Sharpy.Lsp/Handlers/DeclarationCursorResolver.cs::DeclarationCursorResolver.Resolve"] = "walker-default-contract",

        // Refactoring providers — documented-by-design partial dispatch
        ["Sharpy.Lsp/Refactoring/ConvertFormsProvider.cs::ConvertFormsProvider.FormatLiteral"] = "documented-by-design:Sharpy.Lsp/Refactoring/ConvertFormsProvider.cs:FormatLiteral",
        ["Sharpy.Lsp/Refactoring/ImplementInterfaceProvider.cs::ImplementInterfaceProvider.GetCodeActionsAsync"] = "documented-by-design:Sharpy.Lsp/Refactoring/ImplementInterfaceProvider.cs:GetCodeActionsAsync",
        ["Sharpy.Lsp/Refactoring/InlineProvider.cs::InlineProvider.CheckReassignmentInStatement"] = "walker-default-contract",
        ["Sharpy.Lsp/Refactoring/InlineProvider.cs::InlineProvider.IsSideEffectFree"] = "walker-default-contract",
        ["Sharpy.Lsp/Refactoring/OrganizeImportsProvider.cs::OrganizeImportsProvider.GetSortKey"] = "documented-by-design:Sharpy.Lsp/Refactoring/OrganizeImportsProvider.cs:GetSortKey",
        ["Sharpy.Lsp/Refactoring/OrganizeImportsProvider.cs::OrganizeImportsProvider.RenderImportStatement"] = "documented-by-design:Sharpy.Lsp/Refactoring/OrganizeImportsProvider.cs:RenderImportStatement",
        ["Sharpy.Lsp/Refactoring/ScopeAnalyzer.cs::SelectionVisitor.CollectAssignmentTargets"] = "pending-guard:#1716",
        ["Sharpy.Lsp/Refactoring/SelectionAnalyzer.cs::SelectionAnalyzer.GetStatementBody"] = "walker-default-contract",

        // Coverage-critical handlers — pending-guard until Phase 2 adds totality guards
        ["Sharpy.Lsp/Handlers/SemanticTokensHandler.cs::SharpySemanticTokensHandler.CollectStatementTokens"] = "pending-guard:#1716",
        ["Sharpy.Lsp/Handlers/SemanticTokensHandler.cs::SharpySemanticTokensHandler.CollectExpressionTokens"] = "pending-guard:#1716",
        ["Sharpy.Lsp/Handlers/SemanticTokensHandler.cs::SharpySemanticTokensHandler.CollectComprehensionClauseTokens"] = "pending-guard:#1716",
        ["Sharpy.Lsp/Handlers/CodeLensHandler.cs::SharpyCodeLensHandler.Handle"] = "pending-guard:#1716",
        ["Sharpy.Lsp/Handlers/DocumentSymbolHandler.cs::SharpyDocumentSymbolHandler.ConvertStatement"] = "pending-guard:#1716",
        ["Sharpy.Lsp/Handlers/DocumentSymbolHandler.cs::SharpyDocumentSymbolHandler.ConvertClassMember"] = "pending-guard:#1716",
        ["Sharpy.Lsp/Handlers/FoldingRangeHandler.cs::SharpyFoldingRangeHandler.CollectStatementRanges"] = "pending-guard:#1716",
        ["Sharpy.Lsp/Handlers/InlayHintHandler.cs::SharpyInlayHintHandler.CollectInlayHints"] = "pending-guard:#1716",
        ["Sharpy.Lsp/Handlers/InlayHintHandler.cs::SharpyInlayHintHandler.MarkPatternBound"] = "pending-guard:#1716",
        ["Sharpy.Lsp/Handlers/InlayHintHandler.cs::SharpyInlayHintHandler.MarkTargetBound"] = "pending-guard:#1716",
        ["Sharpy.Lsp/Handlers/DocumentLinkHandler.cs::SharpyDocumentLinkHandler.CollectLinks"] = "pending-guard:#1716",
        ["Sharpy.Lsp/HoverService.cs::HoverService.GetHoverMarkdownForNode"] = "pending-guard:#1716",
        ["Sharpy.Lsp/HoverService.cs::HoverService.TryNarrowHighlight"] = "pending-guard:#1716",
        ["Sharpy.Lsp/HoverService.cs::HoverService.TryNarrowToKeyword"] = "pending-guard:#1716",
        ["Sharpy.Lsp/HoverService.cs::HoverService.GetBody"] = "pending-guard:#1716",
        ["Sharpy.Lsp/HoverService.cs::HoverService.GetDecorators"] = "pending-guard:#1716",
    };

    [Fact]
    public void AllDispatchSites_MatchRosterExactly()
    {
        var compilerResult = DispatchSiteScan.Scan(
            "src/Sharpy.Compiler",
            "src/Sharpy.Compiler/Sharpy.Compiler.csproj");
        var lspResult = DispatchSiteScan.Scan(
            "src/Sharpy.Lsp",
            "src/Sharpy.Lsp/Sharpy.Lsp.csproj",
            keyPrefix: "Sharpy.Lsp");

        var sites = compilerResult.SiteCountByKey.Keys
            .Concat(lspResult.SiteCountByKey.Keys)
            .ToHashSet();

        var missing = sites.Except(Roster.Keys).OrderBy(s => s).ToList();
        var phantom = Roster.Keys.Except(sites).OrderBy(s => s).ToList();

        foreach (var site in missing)
            _output.WriteLine($"UNROSTERED: {site}");
        foreach (var row in phantom)
            _output.WriteLine($"PHANTOM ROSTER ROW: {row}");

        _output.WriteLine($"Census: {compilerResult.SiteCountByKey.Count} compiler keys + " +
            $"{lspResult.SiteCountByKey.Count} LSP keys = {sites.Count} total");
        _output.WriteLine($"Roster: {Roster.Count} rows");

        Assert.True(missing.Count == 0 && phantom.Count == 0,
            $"Dispatch-site scan and roster differ.\n" +
            $"Unrostered sites (add a row with a justification):\n  {string.Join("\n  ", missing)}\n" +
            $"Phantom roster rows (site no longer exists — drain the row):\n  {string.Join("\n  ", phantom)}");
    }

    [Fact]
    public void EveryJustificationCategory_HasAtLeastOneSite_AndAllJustificationsWellFormed()
    {
        // The four categories that always have at least one site. pending-guard is allowed
        // but not required — once #1716 drains, zero pending-guard rows is valid.
        string[] required = { "guarded-by", "documented-by-design", "walker-default-contract", "refusal-net" };
        string[] allowed = { "guarded-by", "documented-by-design", "walker-default-contract", "refusal-net", "pending-guard" };

        var byCategory = Roster.Values
            .GroupBy(v => v.Split(':')[0])
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var category in required)
            Assert.True(byCategory.ContainsKey(category), $"Category '{category}' has no rostered site.");

        foreach (var (site, justification) in Roster)
        {
            var category = justification.Split(':')[0];
            Assert.True(allowed.Contains(category),
                $"Roster row '{site}' has malformed justification '{justification}'.");
            if (category is "guarded-by" or "refusal-net" or "documented-by-design")
            {
                Assert.True(justification.Contains(':') && justification.Split(':', 2)[1].Length > 0,
                    $"Roster row '{site}' category '{category}' must name its guard/contract.");
            }
            if (category is "pending-guard")
            {
                Assert.True(justification.Contains(":#"),
                    $"Roster row '{site}' is pending-guard and must cite its tracking issue (e.g. pending-guard:#1716).");
            }
        }
    }

    /// <summary>
    /// A justification is a claim, not a label: <c>guarded-by:X</c>/<c>refusal-net:X</c> must name
    /// a test class that EXISTS, and a <c>guarded-by</c> test must actually scan the site it is
    /// credited with. For LSP-keyed rows, cited classes are resolved from <c>src/Sharpy.Lsp.Tests</c>
    /// sources in addition to the compiler test assembly.
    /// </summary>
    [Fact]
    public void GuardCitations_ResolveToRealTests_AndGuardedByTestsScanTheirSite()
    {
        var repoRoot = DispatchSiteScan.FindRepoRoot();
        var compilerTestsDir = Path.Combine(repoRoot, "src", "Sharpy.Compiler.Tests");
        var lspTestsDir = Path.Combine(repoRoot, "src", "Sharpy.Lsp.Tests");
        var testAssemblyTypes = typeof(DispatchSiteInventoryTests).Assembly.GetTypes()
            .Select(t => t.Name)
            .ToHashSet();

        var testSourceCache = new Dictionary<string, string?>();
        string? FindTestSource(string className, string siteKey)
        {
            var cacheKey = $"{className}:{siteKey}";
            if (testSourceCache.TryGetValue(cacheKey, out var cached))
                return cached;

            // For LSP-keyed rows, check LSP test sources first
            if (siteKey.StartsWith("Sharpy.Lsp/") && Directory.Exists(lspTestsDir))
            {
                var lspHit = Directory.GetFiles(lspTestsDir, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                    .FirstOrDefault(f => File.ReadAllText(f).Contains($"class {className}"));
                if (lspHit != null)
                {
                    testSourceCache[cacheKey] = lspHit;
                    return lspHit;
                }
            }

            // Check compiler test sources
            var hit = Directory.GetFiles(compilerTestsDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                    && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .FirstOrDefault(f => File.ReadAllText(f).Contains($"class {className}"));
            testSourceCache[cacheKey] = hit;
            return hit;
        }

        bool ClassExistsInAnyTestProject(string className, string siteKey)
        {
            if (testAssemblyTypes.Contains(className))
                return true;

            // For LSP rows, also search LSP test assembly via source scan
            if (siteKey.StartsWith("Sharpy.Lsp/") && Directory.Exists(lspTestsDir))
            {
                return Directory.GetFiles(lspTestsDir, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                    .Any(f => File.ReadAllText(f).Contains($"class {className}"));
            }

            return false;
        }

        var violations = new List<string>();
        foreach (var (site, justification) in Roster)
        {
            var parts = justification.Split(':', 2);
            if (parts[0] is not ("guarded-by" or "refusal-net"))
                continue;
            var cited = parts[1];

            if (!ClassExistsInAnyTestProject(cited, site))
            {
                violations.Add($"{site} cites '{cited}' — no such test class in any test assembly");
                continue;
            }

            if (parts[0] == "guarded-by")
            {
                var sourcePath = FindTestSource(cited, site);
                if (sourcePath == null)
                {
                    violations.Add($"{site} cites '{cited}' — test source not found");
                    continue;
                }
                var testSource = File.ReadAllText(sourcePath);
                var pathPart = site.Split("::")[0];
                var fileName = Path.GetFileName(pathPart);
                var methodName = site.Split("::")[1].Split('.')[^1];
                if (!testSource.Contains(fileName) || !testSource.Contains($"\"{methodName}\""))
                {
                    violations.Add($"{site} cites '{cited}' — that test's source does not scan "
                        + $"{fileName} :: \"{methodName}\" (unbacked guarded-by claim)");
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Unbacked guard citations:\n  {string.Join("\n  ", violations)}");
    }
}
