using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates source generator declarations and usage.
///
/// Source generators are classes that extend Sharpy's <c>SourceGenerator</c>
/// runtime base type and are invoked via a bracket-attribute decorator
/// (e.g., <c>@[GenerateEquals]</c>) at compile time. This validator enforces:
///
/// <list type="bullet">
///   <item>A generator class must declare exactly one <c>generate</c> method
///         with the signature <c>(self, context: GeneratorContext) -> GeneratorOutput</c>
///         (SPY0445).</item>
///   <item>A generator class must not be abstract — it is instantiated at
///         compile time and cannot be left without an implementation (SPY0446).</item>
///   <item>A generator bracket attribute cannot decorate another source
///         generator class (SPY0553 — cycle).</item>
///   <item>A generator bracket attribute may only target classes, functions,
///         or structs (SPY0447).</item>
/// </list>
///
/// This validator runs at Order 65, after <see cref="DecoratorValidator"/>
/// (Order 60) and <see cref="BodylessSyntaxValidator"/> (Order 62), but well
/// before type checking-dependent validators. It needs access to
/// <see cref="SemanticInfo.GetAllGeneratorBindings"/>, which is populated by
/// <c>TypeChecker.DetectGeneratorAttributes</c>; the bindings exist as soon
/// as inheritance has been resolved (so <see cref="TypeSymbol.IsSourceGenerator"/>
/// is set on the relevant types).
/// </summary>
internal sealed class SourceGeneratorValidator : SemanticValidatorBase
{
    public override string Name => "SourceGeneratorValidator";

    // Runs after DecoratorValidator (60) and BodylessSyntaxValidator (62).
    // Other generator-related validation (signature on the SourceGenerator
    // base class) does not need to wait for type checking.
    public override int Order => 65;

    private const string GenerateMethodName = "generate";
    private const string GeneratorContextTypeName = "GeneratorContext";
    private const string GeneratorOutputTypeName = "GeneratorOutput";
    private const string SelfParameterName = "self";

    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _logger = context.Logger;
        _logger.LogDebug("Starting source generator validation");

        ValidateGeneratorClasses(module, context);
        ValidateGeneratorBindings(context);
    }

    /// <summary>
    /// Walks top-level class declarations and validates the shape of each
    /// source generator class (signature + non-abstract).
    /// </summary>
    private void ValidateGeneratorClasses(Module module, SemanticContext context)
    {
        foreach (var stmt in module.Body)
        {
            if (stmt is not ClassDef classDef)
                continue;

            var typeSymbol = context.SymbolTable.LookupType(classDef.Name);
            if (typeSymbol is null || !typeSymbol.IsSourceGenerator)
                continue;

            ValidateGeneratorIsNotAbstract(classDef, typeSymbol, context);
            ValidateGeneratorMethod(classDef, typeSymbol, context);
        }
    }

    /// <summary>
    /// Validates that the generator class is not @abstract.
    /// </summary>
    private void ValidateGeneratorIsNotAbstract(
        ClassDef classDef,
        TypeSymbol typeSymbol,
        SemanticContext context)
    {
        if (!typeSymbol.IsAbstract)
            return;

        AddError(
            context,
            $"Source generator class '{typeSymbol.Name}' cannot be abstract. " +
            "Generators are instantiated at compile time and must be concrete.",
            classDef.NameLineStart != 0 ? classDef.NameLineStart : classDef.LineStart,
            classDef.NameColumnStart != 0 ? classDef.NameColumnStart : classDef.ColumnStart,
            code: DiagnosticCodes.Validation.AbstractGenerator,
            span: classDef.Span);
    }

    /// <summary>
    /// Validates that the generator class declares exactly one method named
    /// <c>generate</c> with the expected signature.
    /// </summary>
    private void ValidateGeneratorMethod(
        ClassDef classDef,
        TypeSymbol typeSymbol,
        SemanticContext context)
    {
        var generateMethods = classDef.Body
            .OfType<FunctionDef>()
            .Where(f => f.Name == GenerateMethodName)
            .ToList();

        if (generateMethods.Count == 0)
        {
            AddError(
                context,
                $"Source generator class '{typeSymbol.Name}' must declare a 'generate' method " +
                $"with the signature '(self, context: {GeneratorContextTypeName}) -> {GeneratorOutputTypeName}'.",
                classDef.NameLineStart != 0 ? classDef.NameLineStart : classDef.LineStart,
                classDef.NameColumnStart != 0 ? classDef.NameColumnStart : classDef.ColumnStart,
                code: DiagnosticCodes.Validation.InvalidGeneratorSignature,
                span: classDef.Span);
            return;
        }

        if (generateMethods.Count > 1)
        {
            // Overloads of 'generate' are not supported — pick the second
            // occurrence as the offending location.
            var duplicate = generateMethods[1];
            AddError(
                context,
                $"Source generator class '{typeSymbol.Name}' must declare exactly one 'generate' method; " +
                $"found {generateMethods.Count} overloads.",
                duplicate.NameLineStart != 0 ? duplicate.NameLineStart : duplicate.LineStart,
                duplicate.NameColumnStart != 0 ? duplicate.NameColumnStart : duplicate.ColumnStart,
                code: DiagnosticCodes.Validation.InvalidGeneratorSignature,
                span: duplicate.Span);
            return;
        }

        ValidateGenerateSignature(generateMethods[0], typeSymbol, context);
    }

    /// <summary>
    /// Validates the signature of a single <c>generate</c> method.
    /// Expected: <c>(self, context: GeneratorContext) -> GeneratorOutput</c>.
    /// Untyped parameters are allowed (they default to the expected types);
    /// explicit annotations that disagree are reported.
    /// </summary>
    private void ValidateGenerateSignature(
        FunctionDef funcDef,
        TypeSymbol typeSymbol,
        SemanticContext context)
    {
        var paramCount = funcDef.Parameters.Length;
        if (paramCount != 2)
        {
            AddError(
                context,
                $"'generate' method on source generator '{typeSymbol.Name}' must have exactly " +
                $"2 parameters '(self, context: {GeneratorContextTypeName})', got {paramCount}.",
                funcDef.NameLineStart != 0 ? funcDef.NameLineStart : funcDef.LineStart,
                funcDef.NameColumnStart != 0 ? funcDef.NameColumnStart : funcDef.ColumnStart,
                code: DiagnosticCodes.Validation.InvalidGeneratorSignature,
                span: funcDef.Span);
            return;
        }

        var selfParam = funcDef.Parameters[0];
        if (selfParam.Name != SelfParameterName)
        {
            AddError(
                context,
                $"First parameter of 'generate' on source generator '{typeSymbol.Name}' must be " +
                $"'self', got '{selfParam.Name}'.",
                selfParam.LineStart, selfParam.ColumnStart,
                code: DiagnosticCodes.Validation.InvalidGeneratorSignature,
                span: selfParam.Span ?? funcDef.Span);
        }

        var contextParam = funcDef.Parameters[1];
        if (contextParam.Type != null && !IsTypeAnnotationName(contextParam.Type, GeneratorContextTypeName))
        {
            AddError(
                context,
                $"Parameter '{contextParam.Name}' of 'generate' on source generator '{typeSymbol.Name}' must be " +
                $"of type '{GeneratorContextTypeName}', got '{TypeAnnotationHelper.GetName(contextParam.Type)}'.",
                contextParam.LineStart, contextParam.ColumnStart,
                code: DiagnosticCodes.Validation.InvalidGeneratorSignature,
                span: contextParam.Span ?? funcDef.Span);
        }

        if (funcDef.ReturnType != null && !IsTypeAnnotationName(funcDef.ReturnType, GeneratorOutputTypeName))
        {
            AddError(
                context,
                $"'generate' method on source generator '{typeSymbol.Name}' must return " +
                $"'{GeneratorOutputTypeName}', got '{TypeAnnotationHelper.GetName(funcDef.ReturnType)}'.",
                funcDef.NameLineStart != 0 ? funcDef.NameLineStart : funcDef.LineStart,
                funcDef.NameColumnStart != 0 ? funcDef.NameColumnStart : funcDef.ColumnStart,
                code: DiagnosticCodes.Validation.InvalidGeneratorSignature,
                span: funcDef.Span);
        }
    }

    /// <summary>
    /// Walks each recorded generator binding and validates its target —
    /// detecting cycles (SPY0553) and invalid targets (SPY0447).
    /// </summary>
    private void ValidateGeneratorBindings(SemanticContext context)
    {
        foreach (var (declaration, bindings) in context.SemanticInfo.GetAllGeneratorBindings())
        {
            foreach (var binding in bindings)
            {
                ValidateBindingTarget(declaration, binding, context);
                ValidateBindingNotOnAnotherGenerator(declaration, binding, context);
            }
        }
    }

    /// <summary>
    /// A generator bracket attribute is only valid on a class, function, or
    /// struct declaration. Anything else is rejected with SPY0447.
    /// </summary>
    private void ValidateBindingTarget(
        Statement declaration,
        GeneratorBinding binding,
        SemanticContext context)
    {
        if (declaration is ClassDef or FunctionDef or StructDef)
            return;

        var trigger = binding.Trigger;
        AddError(
            context,
            $"Source generator '@[{binding.GeneratorType.Name}]' can only be applied to a class, function, " +
            "or struct declaration.",
            trigger.LineStart, trigger.ColumnStart,
            code: DiagnosticCodes.Validation.GeneratorOnInvalidTarget,
            span: trigger.Span);
    }

    /// <summary>
    /// A generator bracket attribute applied to another source generator class
    /// would create a cycle (the inner generator would need to be invoked to
    /// produce the outer generator). Reported with SPY0553.
    /// </summary>
    private void ValidateBindingNotOnAnotherGenerator(
        Statement declaration,
        GeneratorBinding binding,
        SemanticContext context)
    {
        if (declaration is not ClassDef classDef)
            return;

        var targetSymbol = context.SymbolTable.LookupType(classDef.Name);
        if (targetSymbol is null || !targetSymbol.IsSourceGenerator)
            return;

        var trigger = binding.Trigger;
        AddError(
            context,
            $"Source generator '@[{binding.GeneratorType.Name}]' cannot be applied to another " +
            $"source generator class '{targetSymbol.Name}'. Generators cannot decorate other generators.",
            trigger.LineStart, trigger.ColumnStart,
            code: DiagnosticCodes.CodeGen.GeneratorCycleDetected,
            span: trigger.Span);
    }

    /// <summary>
    /// Returns true if the type annotation is a simple, non-optional, non-generic
    /// reference to a type with the given name.
    /// </summary>
    private static bool IsTypeAnnotationName(TypeAnnotation annotation, string expectedName)
    {
        if (annotation.IsOptional)
            return false;
        if (annotation.TypeArguments.Length != 0)
            return false;
        return annotation.Name == expectedName;
    }
}
