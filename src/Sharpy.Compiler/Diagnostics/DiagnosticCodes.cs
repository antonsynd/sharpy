namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Catalog of all diagnostic error codes for the Sharpy compiler.
/// Codes are organized by phase:
///   SPY0001-SPY0099: Lexer errors
///   SPY0100-SPY0199: Parser errors
///   SPY0200-SPY0399: Semantic errors
///   SPY0400-SPY0499: Validation errors
///   SPY0500-SPY0599: Code generation errors
/// </summary>
public static class DiagnosticCodes
{
    /// <summary>
    /// Lexer diagnostic codes (SPY0001-SPY0099).
    /// </summary>
    public static class Lexer
    {
        public const string UnterminatedString = "SPY0001";
        public const string UnterminatedFString = "SPY0002";
        public const string UnterminatedRawString = "SPY0003";
        public const string InvalidEscapeSequence = "SPY0004";
        public const string InvalidHexEscape = "SPY0005";
        public const string InvalidUnicodeEscape = "SPY0006";
        public const string InvalidNumber = "SPY0007";
        public const string InvalidHexLiteral = "SPY0008";
        public const string InvalidBinaryLiteral = "SPY0009";
        public const string InvalidOctalLiteral = "SPY0010";
        public const string MixedTabsAndSpaces = "SPY0011";
        public const string TabsNotAllowed = "SPY0012";
        public const string InvalidIndentation = "SPY0013";
        public const string IndentationMismatch = "SPY0014";
        public const string UnexpectedCharacter = "SPY0015";
        public const string BackslashAtEof = "SPY0016";
        public const string BackslashTrailingWhitespace = "SPY0017";
        public const string UnterminatedBacktickIdentifier = "SPY0018";
        public const string InvalidFloatLiteral = "SPY0019";
        public const string UnterminatedFStringExpression = "SPY0020";
        public const string UnmatchedBraceInFString = "SPY0021";
        public const string UnterminatedFormatSpec = "SPY0022";
        public const string InvalidNumericSuffix = "SPY0023";
        public const string OctalEscapeOverflow = "SPY0024";
    }

    /// <summary>
    /// Parser diagnostic codes (SPY0100-SPY0199).
    /// </summary>
    public static class Parser
    {
        public const string UnexpectedToken = "SPY0100";
        public const string ExpectedIdentifier = "SPY0101";
        public const string ExpectedNewline = "SPY0102";
        public const string ExpectedEndOfStatement = "SPY0103";
        public const string ExpectedToken = "SPY0104";
        public const string InvalidDecoratorTarget = "SPY0105";
        public const string TupleAsStatement = "SPY0106";
        public const string InvalidTypeAnnotationTarget = "SPY0107";
        public const string EmptyEnum = "SPY0108";
        public const string PositionalAfterKeyword = "SPY0109";
        public const string MultipleVariadic = "SPY0110";
        public const string VariadicWithDefault = "SPY0111";
        public const string VariadicNotLast = "SPY0112";
        public const string FreeUnionNotSupported = "SPY0113";
        public const string EmptyListShorthand = "SPY0114";
        public const string EmptySetDictShorthand = "SPY0115";
        public const string ExpectedModuleName = "SPY0116";
        public const string ExpectedDecoratorName = "SPY0117";
        public const string MixedNamedUnnamedTupleElements = "SPY0118";
    }

    /// <summary>
    /// Semantic diagnostic codes (SPY0200-SPY0399).
    /// </summary>
    public static class Semantic
    {
        // Name resolution (SPY0200-SPY0219)
        public const string UndefinedVariable = "SPY0200";
        public const string UndefinedFunction = "SPY0201";
        public const string UndefinedType = "SPY0202";
        public const string UndefinedMember = "SPY0203";
        public const string DuplicateDefinition = "SPY0204";
        public const string DuplicateClassField = "SPY0205";
        public const string DuplicateParameter = "SPY0206";
        public const string DuplicateConstant = "SPY0207";
        public const string DuplicateTypeAlias = "SPY0208";
        public const string InvalidTypeAlias = "SPY0209";

        // Type checking (SPY0220-SPY0259)
        public const string TypeMismatch = "SPY0220";
        public const string IncompatibleTypes = "SPY0221";
        public const string InvalidBinaryOperation = "SPY0222";
        public const string InvalidUnaryOperation = "SPY0223";
        public const string WrongArgumentCount = "SPY0224";
        public const string InvalidAssignmentTarget = "SPY0225";
        public const string MissingTypeAnnotation = "SPY0226";
        public const string CannotInferType = "SPY0227";
        public const string InvalidCast = "SPY0228";
        public const string NullabilityViolation = "SPY0229";
        public const string NotCallable = "SPY0230";
        public const string InvalidPipeTarget = "SPY0231";
        public const string InvalidSelfUsage = "SPY0232";
        public const string InvalidNothingUsage = "SPY0233";
        public const string UnknownKeywordArgument = "SPY0234";
        public const string DuplicateArgument = "SPY0235";
        public const string InvalidNullConditional = "SPY0236";
        public const string CannotInferGenericType = "SPY0237";
        public const string InvalidComprehension = "SPY0238";
        public const string InvalidTupleUnpacking = "SPY0239";
        public const string InvalidAutoVariable = "SPY0240";
        public const string ConditionNotBoolean = "SPY0241";
        public const string InvalidRaise = "SPY0242";
        public const string InvalidMaybeExpression = "SPY0243";
        public const string InvalidNoneConstructor = "SPY0244";
        public const string InvalidSomeConstructor = "SPY0245";
        public const string InvalidOkErrConstructor = "SPY0246";
        public const string MissingMethodBody = "SPY0247";
        public const string InvalidOverride = "SPY0248";
        public const string MissingParameterAnnotation = "SPY0249";
        public const string InvalidDefaultValue = "SPY0250";
        public const string InterfaceMethodBody = "SPY0251";
        public const string UninitializedStructField = "SPY0252";
        public const string InvalidEnumValue = "SPY0253";
        public const string InvalidFunctionType = "SPY0254";
        public const string UnrecognizedStatementType = "SPY0255";
        public const string UnrecognizedExpressionType = "SPY0256";

        // Return and control flow (SPY0260-SPY0279)
        public const string MissingReturnValue = "SPY0260";
        public const string MissingReturnType = "SPY0261";
        public const string ReturnOutsideFunction = "SPY0262";
        public const string BreakOutsideLoop = "SPY0263";
        public const string ContinueOutsideLoop = "SPY0264";
        public const string NotAllPathsReturn = "SPY0266";

        // Class and inheritance (SPY0280-SPY0299)
        public const string AbstractInstantiation = "SPY0280";
        public const string InvalidInheritance = "SPY0281";
        public const string IncompatibleOverride = "SPY0282";
        public const string AccessViolation = "SPY0283";
        public const string SuperOutsideClass = "SPY0284";
        public const string SuperNoParent = "SPY0285";
        public const string DuplicateClass = "SPY0286";
        public const string InvalidSuperUsage = "SPY0287";
        public const string CircularInheritance = "SPY0288";

        // Import errors (SPY0300-SPY0319)
        public const string ModuleNotFound = "SPY0300";
        public const string ImportError = "SPY0301";
        public const string CircularImport = "SPY0302";
        public const string ImportPrivateSymbol = "SPY0303";
        public const string ModuleLoadError = "SPY0304";
        public const string AssemblyNotFound = "SPY0305";
        public const string AssemblyLoadError = "SPY0306";

        // Protocol and operator (SPY0320-SPY0339)
        public const string ProtocolMissingMethod = "SPY0320";
        public const string InvalidOperatorSignature = "SPY0321";
        public const string InvalidDecoratorUsage = "SPY0322";
        public const string ConflictingSynthesizedInterface = "SPY0323";

        // Module level (SPY0340-SPY0349)
        public const string ModuleLevelExecutableStatement = "SPY0340";
        public const string ModuleLevelNoTypeAnnotation = "SPY0341";
    }

    /// <summary>
    /// Validation diagnostic codes (SPY0400-SPY0499).
    /// </summary>
    public static class Validation
    {
        public const string MutableDefault = "SPY0400";
        public const string NonConstDefault = "SPY0401";
        public const string UnsupportedOperator = "SPY0402";
        public const string MissingMainFunction = "SPY0403";
        public const string InvalidNullCoalesce = "SPY0404";

        // Warnings (SPY0450-SPY0499)
        public const string UnreachableCodeWarning = "SPY0450";
        public const string UnusedVariable = "SPY0451";
        public const string UnusedImport = "SPY0452";
        public const string NamingConventionWarning = "SPY0453";
        public const string EqWithoutObjectOverload = "SPY0454";
        public const string EqObjectWithoutHash = "SPY0455";
        public const string HashWithoutEqObject = "SPY0456";

        // Dunder invocation rules (SPY0460-SPY0469)
        public const string DunderDirectInvocation = "SPY0460";
        public const string DunderWrongReceiver = "SPY0461";
        public const string DunderCapture = "SPY0462";
    }

    /// <summary>
    /// Code generation diagnostic codes (SPY0500-SPY0599).
    /// </summary>
    public static class CodeGen
    {
        public const string EmitError = "SPY0500";
        public const string UnsupportedFeature = "SPY0501";
        public const string EmptyClassName = "SPY0502";
        public const string DuplicateMember = "SPY0503";
        public const string EmptyMethodName = "SPY0504";
        public const string AbstractMethodWithBody = "SPY0505";
        public const string NonAbstractMethodWithoutBody = "SPY0506";
        public const string VarWithoutInitializer = "SPY0507";
        public const string UnrecognizedStatementType = "SPY0510";
        public const string NestedComprehension = "SPY0515";
        public const string TupleUnpackingComprehension = "SPY0516";
        public const string ComplexTupleUnpacking = "SPY0517";
        public const string UnsupportedExpressionType = "SPY0518";
        public const string UnsupportedOperator = "SPY0519";
        public const string NameCollision = "SPY0520";
        public const string TypeReExportNotSupported = "SPY0521";
        public const string MemberNameCollision = "SPY0522";
        public const string InternalGeneratedCSharpParseError = "SPY0599";
    }

    /// <summary>
    /// Informational diagnostic codes (SPY1000-SPY1099).
    /// Non-error, non-warning notes emitted during compilation.
    /// </summary>
    public static class Info
    {
        public const string ImplicitInterfaceSynthesis = "SPY1001";
    }

    /// <summary>
    /// Infrastructure diagnostic codes (SPY0900-SPY0999).
    /// These cover compiler-level errors not tied to a specific language phase.
    /// </summary>
    public static class Infrastructure
    {
        public const string CompilationFailed = "SPY0900";
        public const string CompilationCancelled = "SPY0901";
        public const string AssemblyCompilationFailed = "SPY0902";
        public const string FileReadError = "SPY0903";
        public const string InvariantViolation = "SPY0904";
        public const string TooManyErrors = "SPY0905";
        public const string ParserLoopStall = "SPY0906";
        public const string UnexpectedUnknownType = "SPY0907";
    }
}
