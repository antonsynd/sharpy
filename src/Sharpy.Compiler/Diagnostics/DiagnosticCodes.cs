namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Catalog of all diagnostic error codes for the Sharpy compiler.
/// Codes are organized by phase:
///   SHP0001-SHP0099: Lexer errors
///   SHP0100-SHP0199: Parser errors
///   SHP0200-SHP0399: Semantic errors
///   SHP0400-SHP0499: Validation errors
///   SHP0500-SHP0599: Code generation errors
/// </summary>
public static class DiagnosticCodes
{
    /// <summary>
    /// Lexer diagnostic codes (SHP0001-SHP0099).
    /// </summary>
    public static class Lexer
    {
        public const string UnterminatedString = "SHP0001";
        public const string UnterminatedFString = "SHP0002";
        public const string UnterminatedRawString = "SHP0003";
        public const string InvalidEscapeSequence = "SHP0004";
        public const string InvalidHexEscape = "SHP0005";
        public const string InvalidUnicodeEscape = "SHP0006";
        public const string InvalidNumber = "SHP0007";
        public const string InvalidHexLiteral = "SHP0008";
        public const string InvalidBinaryLiteral = "SHP0009";
        public const string InvalidOctalLiteral = "SHP0010";
        public const string MixedTabsAndSpaces = "SHP0011";
        public const string TabsNotAllowed = "SHP0012";
        public const string InvalidIndentation = "SHP0013";
        public const string IndentationMismatch = "SHP0014";
        public const string UnexpectedCharacter = "SHP0015";
        public const string BackslashAtEof = "SHP0016";
        public const string BackslashTrailingWhitespace = "SHP0017";
        public const string UnterminatedBacktickIdentifier = "SHP0018";
        public const string InvalidFloatLiteral = "SHP0019";
        public const string UnterminatedFStringExpression = "SHP0020";
        public const string UnmatchedBraceInFString = "SHP0021";
        public const string UnterminatedFormatSpec = "SHP0022";
        public const string InvalidNumericSuffix = "SHP0023";
        public const string OctalEscapeOverflow = "SHP0024";
    }

    /// <summary>
    /// Parser diagnostic codes (SHP0100-SHP0199).
    /// </summary>
    public static class Parser
    {
        public const string UnexpectedToken = "SHP0100";
        public const string ExpectedIdentifier = "SHP0101";
        public const string ExpectedNewline = "SHP0102";
        public const string ExpectedEndOfStatement = "SHP0103";
        public const string ExpectedToken = "SHP0104";
        public const string InvalidDecoratorTarget = "SHP0105";
        public const string TupleAsStatement = "SHP0106";
        public const string InvalidTypeAnnotationTarget = "SHP0107";
        public const string EmptyEnum = "SHP0108";
        public const string PositionalAfterKeyword = "SHP0109";
        public const string MultipleVariadic = "SHP0110";
        public const string VariadicWithDefault = "SHP0111";
        public const string VariadicNotLast = "SHP0112";
        public const string FreeUnionNotSupported = "SHP0113";
        public const string EmptyListShorthand = "SHP0114";
        public const string EmptySetDictShorthand = "SHP0115";
        public const string ExpectedModuleName = "SHP0116";
        public const string ExpectedDecoratorName = "SHP0117";
    }

    /// <summary>
    /// Semantic diagnostic codes (SHP0200-SHP0399).
    /// </summary>
    public static class Semantic
    {
        // Name resolution (SHP0200-SHP0219)
        public const string UndefinedVariable = "SHP0200";
        public const string UndefinedFunction = "SHP0201";
        public const string UndefinedType = "SHP0202";
        public const string UndefinedMember = "SHP0203";
        public const string DuplicateDefinition = "SHP0204";
        public const string DuplicateClassField = "SHP0205";
        public const string DuplicateParameter = "SHP0206";
        public const string DuplicateConstant = "SHP0207";
        public const string DuplicateTypeAlias = "SHP0208";
        public const string InvalidTypeAlias = "SHP0209";

        // Type checking (SHP0220-SHP0259)
        public const string TypeMismatch = "SHP0220";
        public const string IncompatibleTypes = "SHP0221";
        public const string InvalidBinaryOperation = "SHP0222";
        public const string InvalidUnaryOperation = "SHP0223";
        public const string WrongArgumentCount = "SHP0224";
        public const string InvalidAssignmentTarget = "SHP0225";
        public const string MissingTypeAnnotation = "SHP0226";
        public const string CannotInferType = "SHP0227";
        public const string InvalidCast = "SHP0228";
        public const string NullabilityViolation = "SHP0229";
        public const string NotCallable = "SHP0230";
        public const string InvalidPipeTarget = "SHP0231";
        public const string InvalidSelfUsage = "SHP0232";
        public const string InvalidNothingUsage = "SHP0233";
        public const string UnknownKeywordArgument = "SHP0234";
        public const string DuplicateArgument = "SHP0235";
        public const string InvalidNullConditional = "SHP0236";
        public const string CannotInferGenericType = "SHP0237";
        public const string InvalidComprehension = "SHP0238";
        public const string InvalidTupleUnpacking = "SHP0239";
        public const string InvalidAutoVariable = "SHP0240";
        public const string ConditionNotBoolean = "SHP0241";
        public const string InvalidRaise = "SHP0242";
        public const string InvalidMaybeExpression = "SHP0243";
        public const string InvalidNoneConstructor = "SHP0244";
        public const string InvalidSomeConstructor = "SHP0245";
        public const string InvalidOkErrConstructor = "SHP0246";
        public const string MissingMethodBody = "SHP0247";
        public const string InvalidOverride = "SHP0248";
        public const string MissingParameterAnnotation = "SHP0249";
        public const string InvalidDefaultValue = "SHP0250";
        public const string InterfaceMethodBody = "SHP0251";
        public const string UninitializedStructField = "SHP0252";
        public const string InvalidEnumValue = "SHP0253";
        public const string InvalidFunctionType = "SHP0254";

        // Return and control flow (SHP0260-SHP0279)
        public const string MissingReturnValue = "SHP0260";
        public const string MissingReturnType = "SHP0261";
        public const string ReturnOutsideFunction = "SHP0262";
        public const string BreakOutsideLoop = "SHP0263";
        public const string ContinueOutsideLoop = "SHP0264";
        public const string NotAllPathsReturn = "SHP0266";

        // Class and inheritance (SHP0280-SHP0299)
        public const string AbstractInstantiation = "SHP0280";
        public const string InvalidInheritance = "SHP0281";
        public const string IncompatibleOverride = "SHP0282";
        public const string AccessViolation = "SHP0283";
        public const string SuperOutsideClass = "SHP0284";
        public const string SuperNoParent = "SHP0285";
        public const string DuplicateClass = "SHP0286";
        public const string InvalidSuperUsage = "SHP0287";

        // Import errors (SHP0300-SHP0319)
        public const string ModuleNotFound = "SHP0300";
        public const string ImportError = "SHP0301";
        public const string CircularImport = "SHP0302";
        public const string ImportPrivateSymbol = "SHP0303";
        public const string ModuleLoadError = "SHP0304";
        public const string AssemblyNotFound = "SHP0305";
        public const string AssemblyLoadError = "SHP0306";

        // Protocol and operator (SHP0320-SHP0339)
        public const string ProtocolMissingMethod = "SHP0320";
        public const string InvalidOperatorSignature = "SHP0321";
        public const string InvalidDecoratorUsage = "SHP0322";

        // Module level (SHP0340-SHP0349)
        public const string ModuleLevelExecutableStatement = "SHP0340";
        public const string ModuleLevelNoTypeAnnotation = "SHP0341";
    }

    /// <summary>
    /// Validation diagnostic codes (SHP0400-SHP0499).
    /// </summary>
    public static class Validation
    {
        public const string MutableDefault = "SHP0400";
        public const string NonConstDefault = "SHP0401";
        public const string UnsupportedOperator = "SHP0402";
        public const string MissingMainFunction = "SHP0403";
        public const string InvalidNullCoalesce = "SHP0404";

        // Warnings (SHP0450-SHP0499)
        public const string UnreachableCodeWarning = "SHP0450";
        public const string UnusedVariable = "SHP0451";
        public const string UnusedImport = "SHP0452";
    }

    /// <summary>
    /// Code generation diagnostic codes (SHP0500-SHP0599).
    /// </summary>
    public static class CodeGen
    {
        public const string EmitError = "SHP0500";
        public const string UnsupportedFeature = "SHP0501";
        public const string EmptyClassName = "SHP0502";
        public const string DuplicateMember = "SHP0503";
        public const string EmptyMethodName = "SHP0504";
        public const string AbstractMethodWithBody = "SHP0505";
        public const string NonAbstractMethodWithoutBody = "SHP0506";
        public const string VarWithoutInitializer = "SHP0507";
        public const string UnrecognizedStatementType = "SHP0510";
        public const string InternalGeneratedCSharpParseError = "SHP0599";
    }

    /// <summary>
    /// Infrastructure diagnostic codes (SHP0900-SHP0999).
    /// These cover compiler-level errors not tied to a specific language phase.
    /// </summary>
    public static class Infrastructure
    {
        public const string CompilationFailed = "SHP0900";
        public const string CompilationCancelled = "SHP0901";
        public const string AssemblyCompilationFailed = "SHP0902";
        public const string FileReadError = "SHP0903";
    }
}
