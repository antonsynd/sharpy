using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    /// Maps "relative_path::Type.Method" → justification category. Every category is a claim
    /// that a fact below checks (verify-round T10: before that, walker-default-contract and
    /// documented-by-design rows were never checked — 113 of 184 rows were unfalsifiable):
    ///   guarded-by:&lt;TestClass&gt;      — a totality test asserts scan-vs-roster for the site;
    ///                                  the class must exist and its source must scan the site
    ///   documented-by-design:&lt;path&gt;:&lt;Method|Type&gt;
    ///                                — deliberately partial dispatch; the cited file must exist
    ///                                  (compiler-relative, or "Sharpy.Lsp/…"), declare a method
    ///                                  or type with that identifier, and that declaration must
    ///                                  carry a contract comment (a /// doc comment or a //
    ///                                  comment in the body) containing one of the markers in
    ///                                  <see cref="ContractMarkers"/>
    ///   walker-default-contract      — validator whose default-ignore is contractual; every
    ///                                  switch at the site must HAVE a default/discard arm
    ///   refusal-net:&lt;TestClass&gt;      — a conformance net covers the dispatch; the class must exist
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

        // IrNode rewriters — the IrNode kind set is closed by design; the contract is the
        // TypeAnnotation doc comment ("Deliberately not reachable from Node.GetChildNodes …
        // the lowering-IR rewriter and optimization passes … would silently take their default arms")
        ["Lowering/IrTreeRewriter.cs::IrTreeRewriter.RewriteNode"] = "documented-by-design:Parser/Ast/Types.cs:TypeAnnotation",
        ["Lowering/Passes/ComprehensionFusionPass.cs::ComprehensionFusionPass.RewriteNode"] = "documented-by-design:Parser/Ast/Types.cs:TypeAnnotation",
        ["Lowering/Passes/ConstFoldPass.cs::ConstFoldPass.RewriteNode"] = "documented-by-design:Parser/Ast/Types.cs:TypeAnnotation",
        ["Lowering/Passes/StackCollectionsPass.cs::StackCollectionsPass.RewriteNode"] = "documented-by-design:Parser/Ast/Types.cs:TypeAnnotation",

        // CheckExpression — HandleUnrecognizedExpression default
        ["Semantic/TypeChecker.Expressions.cs::TypeChecker.CheckExpression"] = "documented-by-design:Semantic/TypeChecker.Expressions.cs:HandleUnrecognizedExpression",

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
        // AbstractMemberValidator.ValidateClass: pinned in the declaration-kind family (see the guarded-by rows)
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
        // The cast-steer spelling for a refused numeric pair (#1699, 94e87ff99): a switch over the
        // reported node's shape (binary operation / augmented assignment) that names the two
        // operands; the NarrowWidthArithmeticMatrixTests refused cells assert the steer text for
        // both shapes, and mutation B-M9 (steer returns null) reddens them.
        ["Semantic/TypeChecker.Expressions.Operators.cs::TypeChecker.OperandSpellings"] = "refusal-net:NarrowWidthArithmeticMatrixTests",
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
        // (GenerateDictSpreadComprehension / GenerateImperativeComprehension: comprehension-clause family, see guarded-by rows)
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
        // GeneratorContextBuilder.ExtractLiteralValue: dispatch moved to AstHelper.TryGetLiteralValue (#1716)
        ["Project/ProjectCompiler.Generators.cs::ProjectCompiler.IntegrateGeneratedSource"] = "guarded-by:DeclarationKindDispatchTotalityTests",
        ["Semantic/ModuleLoader.cs::ModuleLoader.ExtractNestedTypes"] = "guarded-by:DeclarationKindDispatchTotalityTests",

        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.DescribeTypeOperand"] = "documented-by-design:Semantic/TypeChecker.Expressions.Access.Calls.cs:DescribeTypeOperand",
        ["Semantic/TypeChecker.Expressions.Access.Lambdas.cs::TypeChecker.InferParamTypesFromSubExpression"] = "refusal-net:LambdaTypeInferenceTests",
        ["Semantic/TypeChecker.Expressions.Access.Lambdas.cs::TypeChecker.TryResolveExpressionType"] = "refusal-net:LambdaTypeInferenceTests",
        ["Semantic/TypeChecker.Statements.cs::TypeChecker.CheckDeferBodyControlFlow"] = "guarded-by:DeferBodyControlFlowTotalityTests",
        ["Semantic/TypeChecker.Statements.Patterns.cs::TypeChecker.CheckPattern"] = "guarded-by:CheckPatternTotalityTests",
        ["Services/CompilerInvariants.cs::CompilerInvariants.WarnIfUnknownTypes"] = "documented-by-design:Services/CompilerInvariants.cs:WarnIfUnknownTypes",
        // ContainsWalrusExpression — drained: switch replaced by structural descendant walk (Phase 3a)
        ["Shared/AstHelper.cs::AstHelper.ExtractNarrowingKey"] = "guarded-by:NarrowingKeyTotalityTests",
        ["Shared/AstHelper.cs::AstHelper.TryGetLiteralValue"] = "guarded-by:LiteralValueClassifierTests",
        // #1170 canonical-form seam (332f88156): the parser strips redundant parentheses from store targets once
        ["Shared/AstHelper.cs::AstHelper.CanonicalizeStoreTarget"] = "guarded-by:StoreTargetCanonicalizationTests",
        ["Shared/ExhaustivenessHelper.cs::ExhaustivenessHelper.DescribeIrrefutable"] = "guarded-by:ExhaustivenessHelperTotalityTests",

        // --- New pending-guard rows found by typed census (residue issue) ---
        ["Analysis/ControlFlow/ControlFlowGraphBuilder.cs::ControlFlowGraphBuilder.BuildTry"] = "documented-by-design:Analysis/ControlFlow/ControlFlowGraphBuilder.cs:BuildTry",
        ["Analysis/ControlFlow/ControlFlowGraphBuilder.cs::ControlFlowGraphBuilder.CollectBindingKeysInto"] = "guarded-by:AssignmentTargetDispatchTotalityTests",
        ["Analysis/ControlFlow/DefiniteAssignmentAnalysis.cs::DefiniteAssignmentAnalysis.CollectAssignedNames"] = "guarded-by:AssignmentTargetDispatchTotalityTests",
        ["Analysis/ControlFlow/DefiniteAssignmentAnalysis.cs::DefiniteAssignmentAnalysis.CollectTargetReads"] = "guarded-by:AssignmentTargetDispatchTotalityTests",
        ["Analysis/ControlFlow/NarrowingFlowAnalysis.cs::NarrowingConditionInterpreter.DescribeTupleTypeExpression"] = "documented-by-design:Analysis/ControlFlow/NarrowingFlowAnalysis.cs:DescribeTupleTypeExpression",
        ["Analysis/ControlFlow/NarrowingFlowAnalysis.cs::NarrowingConditionInterpreter.DescribeTypeExpression"] = "documented-by-design:Analysis/ControlFlow/NarrowingFlowAnalysis.cs:DescribeTypeExpression",
        // GetLruCacheMaxSize deleted: emitter reads FunctionSymbol.CacheMaxSize (#1716)
        ["Semantic/TypeChecker.cs::TypeChecker.CheckStatementCore"] = "guarded-by:CheckStatementCoreTotalityTests",
        ["Semantic/TypeChecker.Definitions.cs::TypeChecker.DetectGeneratorAttributes"] = "guarded-by:DeclarationKindDispatchTotalityTests",
        // TypeChecker.ExtractCacheConfig: dispatch moved to AstHelper.TryGetLiteralValue (#1716)
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.MarkTypeFactoryArguments"] = "refusal-net:FileBasedIntegrationTests",
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.RecordIterableArgumentMarks"] = "refusal-net:FileBasedIntegrationTests",
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.TakesContextualCollectionType"] = "walker-default-contract",
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.TryResolveGenericTypeSymbolFromIndexObject"] = "refusal-net:FileBasedIntegrationTests",
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.ValidateClosedExtensionArguments"] = "refusal-net:FileBasedIntegrationTests",
        ["Semantic/TypeChecker.Expressions.Access.Calls.cs::TypeChecker.ValidateSelectedGenericOverloadArguments"] = "refusal-net:FileBasedIntegrationTests",
        ["Semantic/TypeChecker.Expressions.Access.Calls.Overloads.cs::TypeChecker.DescribeMemberPath"] = "documented-by-design:Semantic/TypeChecker.Expressions.Access.Calls.Overloads.cs:DescribeMemberPath",
        ["Semantic/TypeChecker.Expressions.Access.Calls.Overloads.cs::TypeChecker.DescribeReference"] = "documented-by-design:Semantic/TypeChecker.Expressions.Access.Calls.Overloads.cs:DescribeReference",
        ["Semantic/TypeChecker.Expressions.Access.cs::TypeChecker.ClassifyListBacking"] = "walker-default-contract",
        ["Semantic/TypeChecker.Expressions.Access.cs::TypeChecker.TryFlattenDottedName"] = "documented-by-design:Semantic/TypeChecker.Expressions.Access.cs:TryFlattenDottedName",
        ["Semantic/TypeChecker.Expressions.Access.Lambdas.cs::TypeChecker.TryInferLambdaParamTypesFromBody"] = "refusal-net:LambdaTypeInferenceTests",
        ["Semantic/TypeChecker.Expressions.Literals.cs::TypeChecker.CheckComprehensionClauses"] = "guarded-by:ComprehensionClauseDispatchTotalityTests",
        ["Semantic/TypeChecker.Statements.cs::ReassignmentFinder.TargetBindsName"] = "guarded-by:AssignmentTargetDispatchTotalityTests",
        ["Semantic/TypeChecker.Statements.Patterns.cs::TypeChecker.CollectPatternBindingNames"] = "walker-default-contract",
        ["Semantic/TypeChecker.Utilities.cs::TypeChecker.GetAssignmentTargetDescription"] = "documented-by-design:Semantic/TypeChecker.Utilities.cs:GetAssignmentTargetDescription",
        ["Semantic/TypeChecker.Utilities.cs::TypeChecker.IsValidAssignmentTarget"] = "guarded-by:AssignmentTargetDispatchTotalityTests",
        // DecoratorValidator.ValidateLruCacheMaxSizeValue: dispatch moved to AstHelper.TryGetLiteralValue (#1716)
        ["Semantic/Validation/EventValidator.cs::EventValidator.EnumerateAllEvents"] = "guarded-by:MemberKindValidatorTotalityTests",
        ["Semantic/Validation/EventValidator.cs::EventValidator.ValidateInterfaceEvents"] = "guarded-by:MemberKindValidatorTotalityTests",
        ["Semantic/Validation/EventValidator.cs::EventValidator.ValidateTypeBody"] = "guarded-by:MemberKindValidatorTotalityTests",
        ["Semantic/Validation/FinalFieldValidator.cs::FinalFieldValidator.ValidateTypeBody"] = "guarded-by:MemberKindValidatorTotalityTests",
        ["Semantic/Validation/MustUseValidator.cs::MustUseValidator.ElidedMethodGroupMessage"] = "documented-by-design:Semantic/Validation/MustUseValidator.cs:ElidedMethodGroupMessage",
        ["Semantic/Validation/NestedTypeIndex.cs::NestedTypeIndex.TypeDeclarationOf"] = "guarded-by:DeclarationKindDispatchTotalityTests",
        ["Semantic/Validation/AbstractMemberValidator.cs::AbstractMemberValidator.ValidateClass"] = "guarded-by:DeclarationKindDispatchTotalityTests",
        ["Semantic/Validation/PropertyValidator.cs::PropertyValidator.EnumerateAllProperties"] = "guarded-by:MemberKindValidatorTotalityTests",
        ["Semantic/Validation/PropertyValidator.cs::PropertyValidator.ValidateTypeBody"] = "guarded-by:MemberKindValidatorTotalityTests",
        ["Semantic/Validation/PropertyValidator.cs::PropertyValidator.ValidateTypeStatement"] = "guarded-by:MemberKindValidatorTotalityTests",
        ["Semantic/NameResolver.Declarations.cs::NameResolver.GetAccessLevel"] = "guarded-by:DeclarationKindDispatchTotalityTests",
        ["Semantic/NameResolver.Declarations.cs::NameResolver.RefuseBuiltinTypeNameShadowing"] = "guarded-by:DeclarationKindDispatchTotalityTests",
        ["Project/ProjectCompiler.Generators.cs::ProjectCompiler.GetDeclarationName"] = "guarded-by:DeclarationKindDispatchTotalityTests",
        ["Services/ReplSession.cs::ReplSession.IsModuleLevelStatement"] = "guarded-by:DeclarationKindDispatchTotalityTests",
        ["Shared/AssertRaisesForm.cs::AssertRaisesForm.NamesTheMarker"] = "walker-default-contract",
        ["Shared/AstHelper.cs::AstHelper.ExtractIndexComponentKey"] = "guarded-by:NarrowingKeyTotalityTests",
        ["Pretty/StructuralEqualityComparer.cs::StructuralEqualityComparer.Equals"] = "guarded-by:StructuralEqualityComparerTotalityTests",
        ["Pretty/UnparseVisitor.cs::UnparseVisitor.NeedsTrailingCommaInParens"] = "refusal-net:UnparseIdempotencePropertyTests",
        ["Lowering/Passes/ComprehensionFusionPass.cs::ComprehensionFusionPass.QualifiesForProductPreallocation"] = "guarded-by:ComprehensionClauseDispatchTotalityTests",
        // Emitter members of the comprehension-clause family (verify-round finding P4.3); the second one
        // holds two clauses[i] switches under one key and is pinned per switch as well as by union.
        ["CodeGen/RoslynEmitter.Expressions.Comprehensions.cs::RoslynEmitter.GenerateDictSpreadComprehension"] = "guarded-by:ComprehensionClauseDispatchTotalityTests",
        ["CodeGen/RoslynEmitter.Expressions.Comprehensions.cs::RoslynEmitter.GenerateImperativeComprehension"] = "guarded-by:ComprehensionClauseDispatchTotalityTests",

        // ══════════════════════════════════════════════════════════════════════
        // LSP sites — all keyed with "Sharpy.Lsp/" prefix
        // ══════════════════════════════════════════════════════════════════════

        // Cursor resolvers — walker-default: unknown node under cursor → no result
        ["Sharpy.Lsp/Handlers/CallHierarchyPrepareHandler.cs::SharpyCallHierarchyPrepareHandler.ResolveSymbol"] = "walker-default-contract",
        ["Sharpy.Lsp/Handlers/DefinitionHandler.cs::SharpyDefinitionHandler.ResolveSymbol"] = "walker-default-contract",
        ["Sharpy.Lsp/Handlers/ImplementationHandler.cs::SharpyImplementationHandler.ResolveSymbol"] = "walker-default-contract",
        ["Sharpy.Lsp/Handlers/TypeDefinitionHandler.cs::SharpyTypeDefinitionHandler.ResolveType"] = "walker-default-contract",
        ["Sharpy.Lsp/Handlers/TypeHierarchyPrepareHandler.cs::SharpyTypeHierarchyPrepareHandler.ResolveTypeSymbol"] = "walker-default-contract",
        // DeclarationCursorMatrixTests (src/Sharpy.Lsp.Tests) exercises the resolver behaviorally:
        // rename/references parity across the declaration-cursor matrix.
        ["Sharpy.Lsp/Handlers/DeclarationCursorResolver.cs::DeclarationCursorResolver.Resolve"] = "refusal-net:DeclarationCursorMatrixTests",

        // Refactoring providers — documented-by-design partial dispatch
        ["Sharpy.Lsp/Refactoring/ConvertFormsProvider.cs::ConvertFormsProvider.FormatLiteral"] = "documented-by-design:Sharpy.Lsp/Refactoring/ConvertFormsProvider.cs:FormatLiteral",
        ["Sharpy.Lsp/Refactoring/ImplementInterfaceProvider.cs::ImplementInterfaceProvider.GetCodeActionsAsync"] = "documented-by-design:Sharpy.Lsp/Refactoring/ImplementInterfaceProvider.cs:GetCodeActionsAsync",
        ["Sharpy.Lsp/Refactoring/InlineProvider.cs::InlineProvider.CheckReassignmentInStatement"] = "walker-default-contract",
        ["Sharpy.Lsp/Refactoring/InlineProvider.cs::InlineProvider.IsSideEffectFree"] = "walker-default-contract",
        ["Sharpy.Lsp/Refactoring/OrganizeImportsProvider.cs::OrganizeImportsProvider.GetSortKey"] = "documented-by-design:Sharpy.Lsp/Refactoring/OrganizeImportsProvider.cs:GetSortKey",
        ["Sharpy.Lsp/Refactoring/OrganizeImportsProvider.cs::OrganizeImportsProvider.RenderImportStatement"] = "documented-by-design:Sharpy.Lsp/Refactoring/OrganizeImportsProvider.cs:RenderImportStatement",
        ["Sharpy.Lsp/Refactoring/ScopeAnalyzer.cs::SelectionVisitor.CollectAssignmentTargets"] = "guarded-by:AssignmentTargetDispatchTotalityTests",
        ["Sharpy.Lsp/Refactoring/SelectionAnalyzer.cs::SelectionAnalyzer.GetStatementBody"] = "walker-default-contract",

        // Coverage-critical handlers — Phase 2 totality guards
        ["Sharpy.Lsp/Handlers/SemanticTokensHandler.cs::SharpySemanticTokensHandler.CollectStatementTokens"] = "guarded-by:SemanticTokensDispatchTotalityTests",
        ["Sharpy.Lsp/Handlers/SemanticTokensHandler.cs::SharpySemanticTokensHandler.CollectExpressionTokens"] = "guarded-by:SemanticTokensDispatchTotalityTests",
        ["Sharpy.Lsp/Handlers/SemanticTokensHandler.cs::SharpySemanticTokensHandler.CollectComprehensionClauseTokens"] = "guarded-by:SemanticTokensDispatchTotalityTests",
        ["Sharpy.Lsp/Handlers/CodeLensHandler.cs::SharpyCodeLensHandler.Handle"] = "guarded-by:CodeLensDocumentLinkDispatchTotalityTests",
        ["Sharpy.Lsp/Handlers/DocumentSymbolHandler.cs::SharpyDocumentSymbolHandler.ConvertStatement"] = "guarded-by:DocumentSymbolDispatchTotalityTests",
        ["Sharpy.Lsp/Handlers/DocumentSymbolHandler.cs::SharpyDocumentSymbolHandler.ConvertClassMember"] = "guarded-by:DocumentSymbolDispatchTotalityTests",
        ["Sharpy.Lsp/Handlers/FoldingRangeHandler.cs::SharpyFoldingRangeHandler.CollectStatementRanges"] = "guarded-by:FoldingRangeDispatchTotalityTests",
        ["Sharpy.Lsp/Handlers/InlayHintHandler.cs::SharpyInlayHintHandler.CollectInlayHints"] = "guarded-by:InlayHintDispatchTotalityTests",
        ["Sharpy.Lsp/Handlers/InlayHintHandler.cs::SharpyInlayHintHandler.MarkPatternBound"] = "guarded-by:InlayHintDispatchTotalityTests",
        ["Sharpy.Lsp/Handlers/InlayHintHandler.cs::SharpyInlayHintHandler.MarkTargetBound"] = "guarded-by:AssignmentTargetDispatchTotalityTests",
        ["Sharpy.Lsp/Handlers/DocumentLinkHandler.cs::SharpyDocumentLinkHandler.CollectLinks"] = "guarded-by:CodeLensDocumentLinkDispatchTotalityTests",
        ["Sharpy.Lsp/HoverService.cs::HoverService.GetHoverMarkdownForNode"] = "guarded-by:HoverDispatchTotalityTests",
        ["Sharpy.Lsp/HoverService.cs::HoverService.TryNarrowHighlight"] = "guarded-by:HoverDispatchTotalityTests",
        ["Sharpy.Lsp/HoverService.cs::HoverService.TryNarrowToKeyword"] = "guarded-by:HoverDispatchTotalityTests",
        ["Sharpy.Lsp/HoverService.cs::HoverService.GetBody"] = "guarded-by:HoverDispatchTotalityTests",
        ["Sharpy.Lsp/HoverService.cs::HoverService.GetDecorators"] = "guarded-by:HoverDispatchTotalityTests",
    };

    /// <summary>One typed census per test run: both roots, scanned once and shared by the facts.</summary>
    private static readonly Lazy<(DispatchSiteScan.ScanResult Compiler, DispatchSiteScan.ScanResult Lsp)> Scans = new(() => (
        DispatchSiteScan.Scan("src/Sharpy.Compiler", "src/Sharpy.Compiler/Sharpy.Compiler.csproj"),
        DispatchSiteScan.Scan("src/Sharpy.Lsp", "src/Sharpy.Lsp/Sharpy.Lsp.csproj", keyPrefix: "Sharpy.Lsp")));

    private static IReadOnlyList<DispatchSiteScan.DispatchSite> AllSites
        => Scans.Value.Compiler.Sites.Concat(Scans.Value.Lsp.Sites).ToList();

    [Fact]
    public void AllDispatchSites_MatchRosterExactly()
    {
        var (compilerResult, lspResult) = Scans.Value;

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

        // Census (T10 ruling 3): refusal-net rows are checked for class existence only, so their
        // count is reported here to stay visible; they drain to guarded-by over time.
        _output.WriteLine("CENSUS rows-per-category: " + string.Join(" ",
            byCategory.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"))
            + $" total={Roster.Count}");

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
            if (category is "documented-by-design")
            {
                Assert.True(DesignCitationPattern.IsMatch(justification.Split(':', 2)[1]),
                    $"Roster row '{site}' documented-by-design citation '{justification}' must be '<path>.cs:<Method|Type>'.");
            }
        }
    }

    /// <summary><c>&lt;path&gt;.cs:&lt;Identifier&gt;</c> — the only accepted documented-by-design spelling.</summary>
    private static readonly Regex DesignCitationPattern = new(@"^[A-Za-z0-9_./]+\.cs:[A-Za-z_][A-Za-z0-9_]*$");

    // ══════════════════════════════════════════════════════════════════════
    // T10: documented-by-design / walker-default-contract / refusal-net are claims, not labels
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Words that mark a comment as a dispatch CONTRACT — what the switch deliberately does not
    /// handle, or that it only renders text and no semantic decision keys on it ("no semantic
    /// decision" is the codebase's own renderer idiom; ruled in for T10).
    /// </summary>
    internal static readonly string[] ContractMarkers =
    {
        "render", "contract", "deliberately", "documented-by-design", "no semantic decision",
    };

    /// <summary>
    /// (1) Every <c>documented-by-design:&lt;path&gt;.cs:&lt;Identifier&gt;</c> row resolves to an
    /// existing file under src/ that declares a method or type with that identifier, and that
    /// declaration carries a contract comment (see <see cref="ContractMarkers"/>). Before this
    /// fact, repointing a citation at <c>NoSuchFileZZ.cs:NoSuchMethodZZ</c> stayed green.
    /// </summary>
    [Fact]
    public void DesignCitations_ResolveToContractComments()
    {
        var violations = CollectDesignCitationViolations(Roster);
        foreach (var v in violations)
            _output.WriteLine($"DESIGN VIOLATION: {v}");
        Assert.True(violations.Count == 0,
            $"documented-by-design rows without a resolvable contract:\n  {string.Join("\n  ", violations)}");
    }

    /// <summary>
    /// (2) A <c>walker-default-contract</c> row claims the site ignores unknown kinds BY CONTRACT;
    /// a switch with no default/discard arm makes that claim false (a switch expression throws,
    /// a switch statement falls through by accident or by design — indistinguishable). Every
    /// switch at the site must have one.
    /// </summary>
    [Fact]
    public void WalkerDefaultContractRows_HaveDefaultArms()
    {
        var violations = CollectWalkerDefaultViolations(Roster, AllSites);
        foreach (var v in violations)
            _output.WriteLine($"WALKER VIOLATION: {v}");
        Assert.True(violations.Count == 0,
            $"walker-default-contract rows whose site has a switch without a default arm:\n  {string.Join("\n  ", violations)}");
    }

    // (3) refusal-net: ruled (T10, option a) to stay at class existence — a net is keyed by
    // behavior (fixtures, a property corpus, a matrix), never by production file, so 54 of 55
    // rows failed a "cited source names the site's file" rule when measured. The rows are made
    // visible by the per-category census in EveryJustificationCategory_… and drain to
    // guarded-by over time.

    /// <summary>
    /// Positive control for (1), file level: a missing file, a missing member, a malformed
    /// citation and a legacy spelling are each rejected with their own reason, while a real
    /// renderer (compiler-relative and LSP-prefixed) resolves cleanly.
    /// </summary>
    [Fact]
    public void DesignCitations_SyntheticRoster_RejectsMissingFileMemberAndMalformedSpelling()
    {
        const string noFile = "Synthetic/A.cs::A.NoFile";
        const string noMember = "Synthetic/A.cs::A.NoMember";
        const string legacy = "Synthetic/A.cs::A.Legacy";
        const string ok = "Synthetic/A.cs::A.Ok";
        const string lspOk = "Sharpy.Lsp/Synthetic/B.cs::B.Ok";

        var synthetic = new Dictionary<string, string>
        {
            [noFile] = "documented-by-design:NoSuchFileZZ.cs:NoSuchMethodZZ",
            [noMember] = "documented-by-design:Semantic/TypeChecker.Expressions.Access.Calls.cs:NoSuchMethodZZ",
            [legacy] = "documented-by-design:HandleUnrecognizedExpression-default",
            [ok] = "documented-by-design:Semantic/TypeChecker.Expressions.Access.Calls.cs:DescribeTypeOperand",
            [lspOk] = "documented-by-design:Sharpy.Lsp/Refactoring/OrganizeImportsProvider.cs:RenderImportStatement",
        };

        var violations = CollectDesignCitationViolations(synthetic);
        foreach (var v in violations)
            _output.WriteLine($"SYNTHETIC DESIGN VIOLATION: {v}");

        Assert.Contains(violations, v => v.StartsWith(noFile, StringComparison.Ordinal) && v.Contains("file not found"));
        Assert.Contains(violations, v => v.StartsWith(noMember, StringComparison.Ordinal) && v.Contains("no method or type named 'NoSuchMethodZZ'"));
        Assert.Contains(violations, v => v.StartsWith(legacy, StringComparison.Ordinal) && v.Contains("malformed"));
        Assert.DoesNotContain(violations, v => v.StartsWith(ok, StringComparison.Ordinal));
        Assert.DoesNotContain(violations, v => v.StartsWith(lspOk, StringComparison.Ordinal));
        Assert.Equal(3, violations.Count);
    }

    /// <summary>
    /// Positive control for (1), syntax level: the contract-comment probe on parsed snippets —
    /// a marker in the doc comment or in a body comment passes; a doc comment without a marker,
    /// a body comment on a TYPE citation (only the type's own doc counts), and a missing member
    /// fail. Independent of any production file, so it cannot rot when comments move.
    /// </summary>
    [Theory]
    [InlineData("class C { /// <summary>Renders the name.</summary>\n void M() {} }", "M", null)]
    [InlineData("class C { void M() { // deliberately partial: leaves fall through\n } }", "M", null)]
    [InlineData("class C { void M() { /* documented-by-design */ } }", "M", null)]
    [InlineData("/// <summary>Deliberately closed set.</summary>\nrecord R;", "R", null)]
    [InlineData("class C { /// <summary>Gets the thing.</summary>\n void M() {} }", "M", "no contract comment")]
    [InlineData("class C { void M() {} }", "M", "no contract comment")]
    [InlineData("class C { void M() { // contract\n } }", "C", "no contract comment")]
    [InlineData("class C { void Other() {} }", "M", "no method or type named 'M'")]
    public void ContractCommentProbe_OnSnippets(string source, string identifier, string? expectedReasonFragment)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var verdict = ContractCommentVerdict(root, identifier);
        _output.WriteLine($"  {identifier}: {verdict ?? "ok"}");
        if (expectedReasonFragment == null)
            Assert.Null(verdict);
        else
            Assert.Contains(expectedReasonFragment, verdict);
    }

    /// <summary>
    /// Positive control for (2): a real site with a switch lacking a default arm, re-cited as
    /// walker-default-contract, is rejected; a real site whose every switch has one is accepted.
    /// Both are chosen from the live census so the control cannot rot.
    /// </summary>
    [Fact]
    public void WalkerDefault_SyntheticRoster_RejectsSiteWithoutDefaultArm()
    {
        var byKey = AllSites.GroupBy(s => s.Key).ToDictionary(g => g.Key, g => g.ToList());
        var bareKey = byKey.First(kv => kv.Value.Any(s => !s.HasDefaultArm)).Key;
        var coveredKey = byKey.First(kv => kv.Value.All(s => s.HasDefaultArm)).Key;
        _output.WriteLine($"  no-default site: {bareKey}");
        _output.WriteLine($"  all-default site: {coveredKey}");

        var synthetic = new Dictionary<string, string>
        {
            [bareKey] = "walker-default-contract",
            [coveredKey] = "walker-default-contract",
        };

        var violations = CollectWalkerDefaultViolations(synthetic, AllSites);
        foreach (var v in violations)
            _output.WriteLine($"SYNTHETIC WALKER VIOLATION: {v}");

        Assert.Single(violations);
        Assert.StartsWith(bareKey, violations[0], StringComparison.Ordinal);
        Assert.Contains("no default/discard arm", violations[0]);
    }

    private static string ResolveRosterPath(string rosterRelativePath)
    {
        var repoRoot = DispatchSiteScan.FindRepoRoot();
        const string lspPrefix = "Sharpy.Lsp/";
        return rosterRelativePath.StartsWith(lspPrefix, StringComparison.Ordinal)
            ? Path.Combine(repoRoot, "src", "Sharpy.Lsp", rosterRelativePath[lspPrefix.Length..])
            : Path.Combine(repoRoot, "src", "Sharpy.Compiler", rosterRelativePath);
    }

    /// <summary>
    /// The contract-comment probe: null when a method or type named <paramref name="identifier"/>
    /// carries a comment with a marker, else the reason. Method citations read the declaration's
    /// doc comment and every comment in its body; type citations read only the type's own doc
    /// comment (a comment somewhere inside a type body is not that type's contract).
    /// </summary>
    internal static string? ContractCommentVerdict(CompilationUnitSyntax root, string identifier)
    {
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text == identifier).ToList();
        var types = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
            .Where(t => t.Identifier.Text == identifier).ToList();
        if (methods.Count == 0 && types.Count == 0)
            return $"no method or type named '{identifier}'";

        var comments = new List<string>();
        foreach (var method in methods)
            comments.AddRange(CommentTexts(method.DescendantTrivia()));
        foreach (var type in types)
            comments.AddRange(CommentTexts(type.GetLeadingTrivia()));

        var text = string.Join("\n", comments);
        var hit = ContractMarkers.FirstOrDefault(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));
        return hit == null
            ? $"'{identifier}' has no contract comment (no /// or // comment containing any of: {string.Join(", ", ContractMarkers)})"
            : null;
    }

    private static IEnumerable<string> CommentTexts(IEnumerable<SyntaxTrivia> trivia)
        => trivia
            .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)
                     || t.IsKind(SyntaxKind.MultiLineCommentTrivia)
                     || t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                     || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            .Select(t => t.ToFullString());

    private static List<string> CollectDesignCitationViolations(IReadOnlyDictionary<string, string> roster)
    {
        var violations = new List<string>();
        var parsed = new Dictionary<string, CompilationUnitSyntax>(StringComparer.Ordinal);

        foreach (var (site, justification) in roster)
        {
            var parts = justification.Split(':', 2);
            if (parts[0] != "documented-by-design")
                continue;
            var citation = parts[1];

            if (!DesignCitationPattern.IsMatch(citation))
            {
                violations.Add($"{site} cites '{citation}' — malformed; expected <path>.cs:<Method|Type>");
                continue;
            }

            var sep = citation.LastIndexOf(':');
            var path = citation[..sep];
            var identifier = citation[(sep + 1)..];

            var fullPath = ResolveRosterPath(path);
            if (!File.Exists(fullPath))
            {
                violations.Add($"{site} cites '{citation}' — file not found: {path}");
                continue;
            }

            if (!parsed.TryGetValue(fullPath, out var root))
            {
                root = CSharpSyntaxTree.ParseText(File.ReadAllText(fullPath)).GetCompilationUnitRoot();
                parsed[fullPath] = root;
            }

            var verdict = ContractCommentVerdict(root, identifier);
            if (verdict != null)
                violations.Add($"{site} cites '{citation}' — {verdict}");
        }

        return violations;
    }

    private static List<string> CollectWalkerDefaultViolations(
        IReadOnlyDictionary<string, string> roster,
        IEnumerable<DispatchSiteScan.DispatchSite> sites)
    {
        var byKey = sites.GroupBy(s => s.Key).ToDictionary(g => g.Key, g => g.ToList());
        var violations = new List<string>();

        foreach (var (site, justification) in roster)
        {
            if (justification != "walker-default-contract")
                continue;
            if (!byKey.TryGetValue(site, out var switches))
                continue; // a phantom row is AllDispatchSites_MatchRosterExactly's finding

            var bare = switches.Where(s => !s.HasDefaultArm).ToList();
            if (bare.Count > 0)
            {
                violations.Add($"{site} claims walker-default-contract but {bare.Count} of {switches.Count} switch(es) has no default/discard arm: "
                    + string.Join("; ", bare.Select(s => $"{s.Form} on '{s.ScrutineeText}' at line {s.Line}")));
            }
        }

        return violations;
    }

    /// <summary>
    /// The source file declaring <paramref name="className"/>: LSP test sources first for
    /// LSP-keyed sites, then compiler test sources. Null when neither declares it.
    /// </summary>
    private static string? FindTestSourceFile(string className, string siteKey)
    {
        var repoRoot = DispatchSiteScan.FindRepoRoot();
        var compilerTestsDir = Path.Combine(repoRoot, "src", "Sharpy.Compiler.Tests");
        var lspTestsDir = Path.Combine(repoRoot, "src", "Sharpy.Lsp.Tests");

        static IEnumerable<string> Sources(string dir) => Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        if (siteKey.StartsWith("Sharpy.Lsp/", StringComparison.Ordinal) && Directory.Exists(lspTestsDir))
        {
            var lspHit = Sources(lspTestsDir).FirstOrDefault(f => File.ReadAllText(f).Contains($"class {className}"));
            if (lspHit != null)
                return lspHit;
        }

        return Sources(compilerTestsDir).FirstOrDefault(f => File.ReadAllText(f).Contains($"class {className}"));
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
        var violations = CollectGuardCitationViolations(Roster);

        foreach (var v in violations)
            _output.WriteLine($"VIOLATION: {v}");

        Assert.True(violations.Count == 0,
            $"Unbacked guard citations:\n  {string.Join("\n  ", violations)}");
    }

    /// <summary>
    /// Positive control for the citation check (verify-round finding P1.1): the real roster is
    /// all-green, so nothing above proves a bad citation is REJECTED. A synthetic roster proves
    /// each arm of the existence check by direction:
    /// (a) an LSP-keyed row citing a class that exists in NEITHER test project → exactly one
    ///     violation, from the existence check ("no such test class");
    /// (b) a compiler-keyed row citing a class that exists ONLY in <c>src/Sharpy.Lsp.Tests</c>
    ///     (<c>HoverTests</c>) → violation — LSP sources are consulted only for LSP-keyed rows;
    /// (c) an LSP-keyed row citing that same LSP-only class → no violation.
    /// The messages are asserted verbatim so that skipping the existence check (which would
    /// route (a) to the weaker "test source not found" arm) reads as red, not as a relabeling.
    /// </summary>
    [Fact]
    public void GuardCitations_SyntheticRoster_RejectsUnknownAndCrossProjectClasses()
    {
        const string lspOnlyClass = "HoverTests";
        const string unknownClass = "NoSuchTestClassAnywhere";
        const string lspRowUnknown = "Sharpy.Lsp/Handlers/Synthetic.cs::Synthetic.CitesUnknown";
        const string compilerRowLspClass = "Semantic/Synthetic.cs::Synthetic.CitesLspOnlyClass";
        const string lspRowLspClass = "Sharpy.Lsp/Handlers/Synthetic.cs::Synthetic.CitesLspOnlyClass";

        // Preconditions on the fixtures themselves: the LSP-only class really is LSP-only, and
        // the unknown class really is unknown to both projects.
        var repoRoot = DispatchSiteScan.FindRepoRoot();
        var lspHoverSource = Path.Combine(repoRoot, "src", "Sharpy.Lsp.Tests", "HoverTests.cs");
        Assert.True(File.Exists(lspHoverSource), $"fixture precondition: {lspHoverSource} must exist");
        Assert.Contains($"class {lspOnlyClass}", File.ReadAllText(lspHoverSource));
        Assert.DoesNotContain(typeof(DispatchSiteInventoryTests).Assembly.GetTypes(),
            t => t.Name == lspOnlyClass);
        Assert.DoesNotContain(typeof(DispatchSiteInventoryTests).Assembly.GetTypes(),
            t => t.Name == unknownClass);

        var synthetic = new Dictionary<string, string>
        {
            [lspRowUnknown] = $"guarded-by:{unknownClass}",
            [compilerRowLspClass] = $"refusal-net:{lspOnlyClass}",
            [lspRowLspClass] = $"refusal-net:{lspOnlyClass}",
        };

        var violations = CollectGuardCitationViolations(synthetic);
        foreach (var v in violations)
            _output.WriteLine($"SYNTHETIC VIOLATION: {v}");

        var unknownViolations = violations.Where(v => v.StartsWith(lspRowUnknown, StringComparison.Ordinal)).ToList();
        Assert.True(unknownViolations.Count == 1,
            $"(a) expected exactly one violation for {lspRowUnknown}, got {unknownViolations.Count}");
        Assert.Contains($"'{unknownClass}'", unknownViolations[0]);
        Assert.Contains("no such test class in any test assembly", unknownViolations[0]);

        var crossProjectViolations = violations.Where(v => v.StartsWith(compilerRowLspClass, StringComparison.Ordinal)).ToList();
        Assert.True(crossProjectViolations.Count == 1,
            $"(b) expected exactly one violation for {compilerRowLspClass} (LSP sources are consulted only for LSP-keyed rows), got {crossProjectViolations.Count}");
        Assert.Contains($"'{lspOnlyClass}'", crossProjectViolations[0]);
        Assert.Contains("no such test class in any test assembly", crossProjectViolations[0]);

        Assert.DoesNotContain(violations, v => v.StartsWith(lspRowLspClass, StringComparison.Ordinal));
        Assert.Equal(2, violations.Count);
    }

    /// <summary>
    /// The citation validator's body, parameterized on the roster so the real roster and a
    /// synthetic one go through the same predicate. Returns the violation list (empty = clean).
    /// </summary>
    private static List<string> CollectGuardCitationViolations(IReadOnlyDictionary<string, string> roster)
    {
        var repoRoot = DispatchSiteScan.FindRepoRoot();
        var lspTestsDir = Path.Combine(repoRoot, "src", "Sharpy.Lsp.Tests");
        var testAssemblyTypes = typeof(DispatchSiteInventoryTests).Assembly.GetTypes()
            .Select(t => t.Name)
            .ToHashSet();

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
        foreach (var (site, justification) in roster)
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
                var sourcePath = FindTestSourceFile(cited, site);
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

        return violations;
    }
}
