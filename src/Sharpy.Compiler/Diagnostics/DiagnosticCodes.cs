namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Catalog of all diagnostic error codes for the Sharpy compiler.
/// Codes are organized by phase:
///   SPY0001-SPY0099: Lexer errors
///   SPY0100-SPY0199: Parser errors
///   SPY0200-SPY0399: Semantic errors
///   SPY0400-SPY0449: Validation errors
///   SPY0450-SPY0499: Validation warnings
///   SPY0500-SPY0599: Code generation errors
///   SPY0900-SPY0999: Infrastructure errors
///   SPY1000-SPY1099: Informational notes
///
/// Code status legend:
///   Active    — Emitted by the compiler today
///   Reserved  — Held for future use (no constant defined, or stub)
///   Allocated — Constant defined for a planned feature, not yet emitted
///
/// Gaps in the numbering within each range are intentionally reserved for
/// future diagnostics in that sub-category.
/// </summary>
public static class DiagnosticCodes
{
    /// <summary>
    /// Lexer diagnostic codes (SPY0001-SPY0099).
    /// Active: SPY0001-SPY0029 (29 codes)
    /// Reserved: SPY0030-SPY0099 (70 codes for future lexer diagnostics)
    /// </summary>
    public static class Lexer
    {
        #region String/F-string errors (SPY0001-SPY0006)

        public const string UnterminatedString = "SPY0001";         // Active
        public const string UnterminatedFString = "SPY0002";        // Active
        public const string UnterminatedRawString = "SPY0003";      // Active
        public const string InvalidEscapeSequence = "SPY0004";      // Active
        public const string InvalidHexEscape = "SPY0005";           // Active
        public const string InvalidUnicodeEscape = "SPY0006";       // Active

        #endregion

        #region Numeric literal errors (SPY0007-SPY0010, SPY0019, SPY0023-SPY0024)

        public const string InvalidNumber = "SPY0007";              // Active
        public const string InvalidHexLiteral = "SPY0008";          // Active
        public const string InvalidBinaryLiteral = "SPY0009";       // Active
        public const string InvalidOctalLiteral = "SPY0010";        // Active
        public const string InvalidFloatLiteral = "SPY0019";        // Allocated — not yet emitted
        public const string InvalidNumericSuffix = "SPY0023";       // Active
        public const string OctalEscapeOverflow = "SPY0024";        // Active

        #endregion

        #region Indentation errors (SPY0011-SPY0014)

        public const string MixedTabsAndSpaces = "SPY0011";         // Active
        public const string TabsNotAllowed = "SPY0012";             // Active
        public const string InvalidIndentation = "SPY0013";         // Active
        public const string IndentationMismatch = "SPY0014";        // Active

        #endregion

        #region Miscellaneous lexer errors (SPY0015-SPY0018)

        public const string UnexpectedCharacter = "SPY0015";        // Active
        public const string BackslashAtEof = "SPY0016";             // Active
        public const string BackslashTrailingWhitespace = "SPY0017"; // Active
        public const string UnterminatedBacktickIdentifier = "SPY0018"; // Active

        #endregion

        #region F-string expression errors (SPY0020-SPY0022)

        public const string UnterminatedFStringExpression = "SPY0020"; // Active
        public const string UnmatchedBraceInFString = "SPY0021";    // Active
        public const string UnterminatedFormatSpec = "SPY0022";      // Active

        #endregion

        #region Backtick identifier errors (SPY0025)

        public const string DotInBacktickIdentifier = "SPY0025";    // Active

        #endregion

        #region Byte string errors (SPY0026-SPY0028)

        public const string UnterminatedByteString = "SPY0026";    // Active
        public const string UnicodeEscapeInByteString = "SPY0027"; // Active
        public const string NonAsciiInByteString = "SPY0028";      // Active

        #endregion

        #region D-string (dedented) errors (SPY0029)

        public const string DedentedStringIndentationError = "SPY0029"; // Active

        #endregion

        // SPY0030-SPY0099: Reserved for future lexer diagnostics
    }

    /// <summary>
    /// Parser diagnostic codes (SPY0100-SPY0199).
    /// Active: SPY0100-SPY0137 (38 codes)
    /// Reserved: SPY0138-SPY0199 (62 codes)
    /// </summary>
    public static class Parser
    {
        #region Core parsing errors (SPY0100-SPY0107)

        public const string UnexpectedToken = "SPY0100";            // Active
        public const string ExpectedIdentifier = "SPY0101";         // Active
        public const string ExpectedNewline = "SPY0102";            // Active
        public const string ExpectedEndOfStatement = "SPY0103";     // Active
        public const string ExpectedToken = "SPY0104";              // Active
        public const string InvalidDecoratorTarget = "SPY0105";     // Active
        public const string TupleAsStatement = "SPY0106";           // Active
        public const string InvalidTypeAnnotationTarget = "SPY0107"; // Active

        #endregion

        #region Definition and argument errors (SPY0108-SPY0112)

        public const string EmptyEnum = "SPY0108";                  // Active
        public const string PositionalAfterKeyword = "SPY0109";     // Active
        public const string MultipleVariadic = "SPY0110";           // Active
        public const string VariadicWithDefault = "SPY0111";        // Active
        public const string VariadicNotLast = "SPY0112";            // Allocated — not yet emitted

        #endregion

        #region Type and collection syntax (SPY0113-SPY0118)

        public const string FreeUnionNotSupported = "SPY0113";      // Active
        public const string EmptyListShorthand = "SPY0114";         // Active
        public const string EmptySetDictShorthand = "SPY0115";      // Active
        public const string ExpectedModuleName = "SPY0116";         // Active
        public const string ExpectedDecoratorName = "SPY0117";      // Active
        public const string MixedNamedUnnamedTupleElements = "SPY0118"; // Active

        #endregion

        #region Control flow and pattern parsing (SPY0119-SPY0125)

        public const string MaxRecursionDepthExceeded = "SPY0119";  // Active
        public const string ExpectedPattern = "SPY0120";            // Active
        public const string ExpectedCase = "SPY0121";               // Active
        public const string RaiseFromNotSupported = "SPY0122";      // Active
        public const string DictSpreadCallNotSupported = "SPY0123"; // Active
        public const string EmptyUnion = "SPY0124";                 // Active
        public const string GenericTypeInPattern = "SPY0125";       // Active

        #endregion

        #region Parameter marker and placeholder errors (SPY0126-SPY0133)

        public const string SlashAfterStar = "SPY0126";             // Active
        public const string DuplicateSlashMarker = "SPY0127";       // Active
        public const string DuplicateStarMarker = "SPY0128";        // Active
        public const string SlashAtStart = "SPY0129";               // Active
        public const string PlaceholderInKeywordArg = "SPY0130";    // Reserved — keyword '_' placeholders are now supported
        public const string PlaceholderWithSpread = "SPY0131";      // Active
        public const string PlaceholderOutsideCallOrOperator = "SPY0132"; // Allocated — not yet emitted
        public const string NestedPlaceholder = "SPY0133";          // Allocated — not yet emitted

        #endregion

        #region Rejected Python keywords (SPY0134)

        public const string RejectedPythonKeyword = "SPY0134";   // Active

        #endregion

        #region Event syntax errors (SPY0135-SPY0136)

        public const string AutoEventWithBody = "SPY0135";          // Active
        public const string FunctionStyleEventWithoutAccessor = "SPY0136"; // Active

        #endregion

        #region Exception handler syntax (SPY0137-SPY0139)

        public const string ExceptWithAsRequiresParens = "SPY0137"; // Active
        public const string ExceptStarRequiresType = "SPY0138";     // Active
        public const string MixedExceptAndExceptStar = "SPY0139";   // Active

        #endregion

        // SPY0140-SPY0199: Reserved for future parser diagnostics
    }

    /// <summary>
    /// Semantic diagnostic codes (SPY0200-SPY0399).
    /// Active: 120 codes across name resolution, type checking, control flow,
    ///         class/inheritance, imports, protocols, module-level, and additional errors.
    /// Reserved: SPY0289 and gaps within sub-ranges.
    /// </summary>
    public static class Semantic
    {
        #region Name resolution (SPY0200-SPY0209)

        public const string UndefinedVariable = "SPY0200";          // Active
        public const string UndefinedFunction = "SPY0201";          // Active
        public const string UndefinedType = "SPY0202";              // Active
        public const string UndefinedMember = "SPY0203";            // Active
        public const string DuplicateDefinition = "SPY0204";        // Active
        // SPY0205: Reserved (removed — was DuplicateClassField, superseded by DuplicateDefinition)
        public const string DuplicateParameter = "SPY0206";         // Active
        // SPY0207: Reserved (removed — was DuplicateConstant, superseded by DuplicateDefinition)
        // SPY0208: Reserved (removed — was DuplicateTypeAlias, superseded by DuplicateDefinition)
        public const string InvalidTypeAlias = "SPY0209";           // Active
        // SPY0210-SPY0219: Reserved for future name resolution diagnostics

        #endregion

        #region Type checking (SPY0220-SPY0259)

        public const string TypeMismatch = "SPY0220";               // Active
        public const string IncompatibleTypes = "SPY0221";          // Allocated — not yet emitted
        public const string InvalidBinaryOperation = "SPY0222";     // Active
        public const string InvalidUnaryOperation = "SPY0223";      // Active
        public const string WrongArgumentCount = "SPY0224";         // Active
        public const string InvalidAssignmentTarget = "SPY0225";    // Active
        public const string MissingTypeAnnotation = "SPY0226";      // Active
        public const string CannotInferType = "SPY0227";            // Active
        public const string InvalidCast = "SPY0228";                // Active
        public const string NullabilityViolation = "SPY0229";       // Active
        public const string NotCallable = "SPY0230";                // Active
        public const string InvalidPipeTarget = "SPY0231";          // Active
        public const string InvalidSelfUsage = "SPY0232";           // Active
        public const string InvalidNothingUsage = "SPY0233";        // Active
        public const string UnknownKeywordArgument = "SPY0234";     // Active
        public const string DuplicateArgument = "SPY0235";          // Active
        public const string InvalidNullConditional = "SPY0236";     // Active
        public const string CannotInferGenericType = "SPY0237";     // Active
        public const string InvalidComprehension = "SPY0238";       // Allocated — not yet emitted
        public const string InvalidTupleUnpacking = "SPY0239";      // Active
        public const string InvalidAutoVariable = "SPY0240";        // Active
        public const string ConditionNotBoolean = "SPY0241";        // Active
        public const string InvalidRaise = "SPY0242";               // Active
        public const string InvalidMaybeExpression = "SPY0243";     // Active
        public const string InvalidNoneConstructor = "SPY0244";     // Active
        public const string InvalidSomeConstructor = "SPY0245";     // Allocated — not yet emitted
        public const string InvalidOkErrConstructor = "SPY0246";    // Allocated — not yet emitted
        public const string MissingMethodBody = "SPY0247";          // Active
        public const string InvalidOverride = "SPY0248";            // Active
        public const string MissingParameterAnnotation = "SPY0249"; // Reserved — covered by MissingTypeAnnotation (SPY0226)
        public const string InvalidDefaultValue = "SPY0250";        // Active
        public const string InterfaceMethodBody = "SPY0251";        // Active
        public const string UninitializedStructField = "SPY0252";   // Active
        public const string InvalidEnumValue = "SPY0253";           // Active
        public const string InvalidFunctionType = "SPY0254";        // Active
        public const string UnrecognizedStatementType = "SPY0255";  // Active
        public const string UnrecognizedExpressionType = "SPY0256"; // Active
        public const string TuplePatternLengthMismatch = "SPY0257"; // Active
        public const string TupleIndexOutOfRange = "SPY0258";       // Active
        public const string TupleNegativeIndex = "SPY0259";         // Active

        #endregion

        #region Return and control flow (SPY0260-SPY0274)

        public const string MissingReturnValue = "SPY0260";         // Active
        public const string MissingReturnType = "SPY0261";          // Reserved — covered by MissingTypeAnnotation (SPY0226)
        public const string ReturnOutsideFunction = "SPY0262";      // Active
        public const string BreakOutsideLoop = "SPY0263";           // Active
        public const string ContinueOutsideLoop = "SPY0264";        // Active
        public const string YieldOutsideFunction = "SPY0265";       // Active
        public const string NotAllPathsReturn = "SPY0266";          // Active
        public const string YieldWithReturn = "SPY0267";            // Active
        public const string YieldInNext = "SPY0268";                // Active
        public const string GeneratorIterConflict = "SPY0269";      // Active
        public const string YieldInTryExcept = "SPY0270";           // Active
        public const string YieldInCatchHandler = "SPY0271";        // Active
        public const string YieldInFinallyBlock = "SPY0272";        // Active
        public const string AwaitOutsideAsync = "SPY0273";          // Active
        public const string InvalidAwaitOperand = "SPY0274";        // Active
        // SPY0275-SPY0279: Reserved for future control flow diagnostics

        #endregion

        #region Class and inheritance (SPY0280-SPY0291)

        public const string AbstractInstantiation = "SPY0280";      // Active
        public const string InvalidInheritance = "SPY0281";         // Active
        public const string IncompatibleOverride = "SPY0282";       // Active
        public const string AccessViolation = "SPY0283";            // Active
        public const string SuperOutsideClass = "SPY0284";          // Active
        public const string SuperNoParent = "SPY0285";              // Active
        public const string DuplicateClass = "SPY0286";             // Reserved — covered by DuplicateDefinition (SPY0204)
        public const string InvalidSuperUsage = "SPY0287";          // Active
        public const string CircularInheritance = "SPY0288";        // Active
        // TODO(#237): SPY0289 reserved for future use
        public const string InstanceFieldViaTypeName = "SPY0290";   // Active
        public const string MaybeOnUnconstrainedTypeParameter = "SPY0291"; // Active
        // SPY0292-SPY0299: Reserved for future class/inheritance diagnostics

        #endregion

        #region Import errors (SPY0300-SPY0306)

        public const string ModuleNotFound = "SPY0300";             // Active
        public const string ImportError = "SPY0301";                // Active
        public const string CircularImport = "SPY0302";             // Active
        // SPY0303: Reserved (removed — was ImportPrivateSymbol, never implemented)
        public const string ModuleLoadError = "SPY0304";            // Active
        public const string AssemblyNotFound = "SPY0305";           // Active
        public const string AssemblyLoadError = "SPY0306";          // Active
        public const string CircularImportStubError = "SPY0307";    // Active
        public const string CircularImportRuntimeUsage = "SPY0308"; // Active
        public const string CircularImportBaseClass = "SPY0309";    // Active
        // Import redirect diagnostics (SPY0310-SPY0319)
        public const string TypingModuleRedirect = "SPY0310";        // Active
        public const string DataclassesModuleRedirect = "SPY0311";   // Active
        // SPY0312-SPY0319: Reserved for future import redirect diagnostics

        #endregion

        #region Protocol and operator (SPY0320-SPY0324)

        public const string ProtocolMissingMethod = "SPY0320";      // Active
        public const string InvalidOperatorSignature = "SPY0321";   // Active
        public const string InvalidDecoratorUsage = "SPY0322";      // Active
        public const string ConflictingSynthesizedInterface = "SPY0323"; // Active
        public const string WithNotDisposable = "SPY0324";          // Active
        public const string InterfaceMethodNotImplemented = "SPY0325"; // Active
        // SPY0326-SPY0339: Reserved for future protocol/operator diagnostics

        #endregion

        #region Module level (SPY0340-SPY0341)

        public const string ModuleLevelExecutableStatement = "SPY0340"; // Active
        public const string ModuleLevelNoTypeAnnotation = "SPY0341"; // Active
        // SPY0342-SPY0349: Reserved for future module-level diagnostics

        #endregion

        #region Additional semantic errors (SPY0350-SPY0378)

        public const string SelfInitOutsideConstructor = "SPY0350"; // Active
        public const string ConflictingConstructorInitializers = "SPY0351"; // Active
        public const string TypeAliasArityMismatch = "SPY0352";    // Active
        public const string AmbiguousOverload = "SPY0353";          // Active
        public const string NoMatchingOverload = "SPY0354";         // Active
        public const string DuplicateMethodSignature = "SPY0355";   // Active
        public const string MultipleStarExpressions = "SPY0356";   // Active
        public const string SpreadIntoNonVariadic = "SPY0357";     // Active
        public const string UnsupportedFeature = "SPY0358";         // Active
        public const string BindingInOrPattern = "SPY0359";         // Active
        public const string RelationalPatternTypeMismatch = "SPY0360"; // Active
        public const string TypePatternIncompatible = "SPY0361";    // Active
        public const string PropertyPatternUnknownField = "SPY0362"; // Active
        public const string PositionalPatternCountMismatch = "SPY0363"; // Active
        public const string UnsupportedPatternInMemberAccessOr = "SPY0364"; // Active
        public const string DuplicateUnionCase = "SPY0365";         // Active
        public const string UnionCaseNotFound = "SPY0366";          // Active
        public const string UnionCaseFieldMismatch = "SPY0367";     // Active
        public const string UnionCaseNameConflict = "SPY0368";      // Active
        public const string PositionalPatternNoDeconstruct = "SPY0369"; // Active
        public const string PositionalOnlyPassedByKeyword = "SPY0370"; // Active
        public const string KeywordOnlyPassedPositionally = "SPY0371"; // Active
        // SPY0372: Reserved (removed — was DelegateWithBody, never implemented)

        // Event errors (SPY0373-SPY0378)
        public const string EventTypeNotDelegate = "SPY0373";       // Active
        public const string EventAccessorParamMismatch = "SPY0374"; // Active
        public const string DirectEventAssignment = "SPY0375";      // Active
        public const string EventHandlerTypeMismatch = "SPY0376";   // Active
        public const string RaiseEventOutsideClass = "SPY0377";     // Active
        public const string EventUnsupportedOperator = "SPY0378";   // Active
        // SPY0379: Reserved for future event semantic errors

        // Dataclass errors (SPY0380-SPY0384)
        public const string DataclassOnNonClass = "SPY0380";              // Active
        public const string DataclassFieldOrdering = "SPY0381";           // Active
        public const string DataclassFieldNoType = "SPY0382";             // Active
        public const string DataclassInvalidOption = "SPY0383";           // Active
        // Self type errors (SPY0384-SPY0385)
        public const string SelfOutsideClass = "SPY0384";
        public const string SelfInStaticMethod = "SPY0385";

        // Builtin call errors (SPY0386)
        public const string UnsupportedTypeNone = "SPY0386";           // Active

        // Parameter modifier errors (SPY0387-SPY0391)
        public const string ModifierWithDefault = "SPY0387";           // Active
        public const string ModifierWithVariadic = "SPY0388";          // Active
        public const string ModifierRequiresVariable = "SPY0389";      // Active
        public const string InParameterReassignment = "SPY0390";       // Active

        // except* errors (SPY0391-SPY0394)
        public const string ExceptStarCatchesExceptionGroup = "SPY0391"; // Active
        public const string BreakInExceptStar = "SPY0392";              // Active
        public const string ContinueInExceptStar = "SPY0393";           // Active
        public const string ReturnInExceptStar = "SPY0394";             // Active
        // Generic type parameter default errors (SPY0395-SPY0396)
        public const string TypeParameterDefaultOrdering = "SPY0395"; // Active
        public const string TypeParameterDefaultViolatesConstraint = "SPY0396"; // Active
        // Exception filter errors (SPY0397-SPY0398)
        public const string ExceptionFilterNotBoolean = "SPY0397";       // Active
        public const string ExceptStarWhenNotSupported = "SPY0398";      // Active

        // Try expression errors (SPY0399)
        public const string TryExceptionTypeNotException = "SPY0399";    // Active

        #endregion
    }

    /// <summary>
    /// Validation diagnostic codes (SPY0400-SPY0499).
    /// Errors: SPY0400-SPY0449, Warnings: SPY0450-SPY0469,
    /// Transition hints: SPY0470-SPY0489 (advisory; emitted at Hint severity).
    /// </summary>
    public static class Validation
    {
        #region Validation errors (SPY0400-SPY0426)

        public const string MutableDefault = "SPY0400";             // Active
        public const string NonConstDefault = "SPY0401";            // Active
        public const string UnsupportedOperator = "SPY0402";        // Active
        public const string MissingMainFunction = "SPY0403";        // Active
        public const string InvalidNullCoalesce = "SPY0404";        // Active

        // Property validation (SPY0405-SPY0412)
        public const string PropertyFieldNameConflict = "SPY0405";  // Active
        public const string PropertyMethodNameConflict = "SPY0406"; // Active
        public const string MixedAutoAndFunctionStyleProperty = "SPY0407"; // Active
        public const string InitOnlyFunctionStyleProperty = "SPY0408"; // Active
        public const string AbstractPropertyMustHaveEllipsisBody = "SPY0409"; // Active
        public const string FinalWithAbstractOrVirtual = "SPY0410"; // Active
        public const string InvalidPropertyOverride = "SPY0411";    // Active
        public const string FinalWithoutOverride = "SPY0412";       // Active

        // Interface validation (SPY0413)
        public const string DunderInUserInterface = "SPY0413";      // Active

        // Dunder validation (SPY0414-SPY0415)
        public const string UnknownDunderMethod = "SPY0414";        // Active
        public const string VirtualOnStructMethod = "SPY0415";      // Active

        // Exhaustiveness validation (SPY0416)
        public const string NonExhaustiveMatchExpression = "SPY0416"; // Active

        // Variance validation (SPY0417-SPY0419)
        public const string VarianceOnClassOrStruct = "SPY0417";    // Active
        public const string CovariantInContravariantPosition = "SPY0418"; // Active
        public const string ContravariantInCovariantPosition = "SPY0419"; // Active

        // Event validation (SPY0420-SPY0423)
        public const string UnpairedEventAccessor = "SPY0420";      // Active
        public const string EventFieldNameConflict = "SPY0421";     // Active
        public const string EventMethodNameConflict = "SPY0422";    // Active
        public const string AbstractEventWithBody = "SPY0423";      // Active
        // SPY0424: Reserved for future event validation errors

        // Decorator argument validation (SPY0425-SPY0426)
        public const string NonConstantDecoratorArgument = "SPY0425"; // Active
        public const string InitPropertyNotAssigned = "SPY0426";    // Active

        // Dunder invocation rules (SPY0427-SPY0429)
        public const string DunderDirectInvocation = "SPY0427";     // Active
        public const string DunderWrongReceiver = "SPY0428";        // Active
        public const string DunderCapture = "SPY0429";              // Active
        // Access modifier decorator validation (SPY0430-SPY0431)
        public const string ConflictingAccessModifiers = "SPY0430"; // Active
        public const string AccessModifierOnDunder = "SPY0431";     // Active
        public const string NamedtupleNotSupported = "SPY0432";     // Active
        // Late-bound default validation (SPY0433-SPY0434)
        public const string LateBoundSelfReference = "SPY0433";     // Active
        public const string LateBoundForwardReference = "SPY0434";  // Active

        // Struct field ordering (SPY0435)
        public const string StructFieldOrdering = "SPY0435";        // Active
        // Conversion operator validation (SPY0436-SPY0439)
        public const string ConversionOperatorNotStatic = "SPY0436";       // Active
        public const string ConversionOperatorParamCount = "SPY0437";      // Active
        public const string ConversionOperatorNoEnclosingType = "SPY0438"; // Active
        public const string ConversionOperatorDuplicate = "SPY0439";       // Active

        // @final field validation (SPY0440-SPY0441)
        public const string FinalFieldAssignmentOutsideConstructor = "SPY0440"; // Active
        public const string FinalOnLocalVariable = "SPY0441";              // Active
        // lru_cache / cache decorator validation (SPY0442-SPY0443)
        public const string LruCacheInvalidMaxSize = "SPY0442";            // Active
        public const string LruCacheOnNonFunction = "SPY0443";             // Active
        // Unknown decorator rejection (SPY0444)
        public const string UnknownDecorator = "SPY0444";                    // Active
        // Source generator validation (SPY0445-SPY0447)
        public const string InvalidGeneratorSignature = "SPY0445";           // Active
        public const string AbstractGenerator = "SPY0446";                   // Active
        public const string GeneratorOnInvalidTarget = "SPY0447";            // Active
        // SPY0448-SPY0449: Reserved for future validation errors

        #endregion

        #region Validation warnings (SPY0450-SPY0464)

        public const string UnreachableCodeWarning = "SPY0450";     // Active
        public const string UnusedVariable = "SPY0451";             // Active
        public const string UnusedImport = "SPY0452";               // Active
        public const string NamingConventionWarning = "SPY0453";    // Active
        public const string EqWithoutObjectOverload = "SPY0454";    // Active
        public const string EqObjectWithoutHash = "SPY0455";        // Active
        public const string HashWithoutEqObject = "SPY0456";        // Active
        [System.Obsolete("SPY0457 is reserved — __reversed__ is now supported")]
        public const string UnsupportedDunderReversed = "SPY0457";  // Deprecated — __reversed__ now supported (see audit 2026-05-11)
        public const string VirtualOnObjectOverride = "SPY0458";    // Active
        public const string StaticFieldViaInstance = "SPY0459";     // Active

        // SPY0460-SPY0462: Reserved (formerly dunder invocation rules, moved to SPY0427-SPY0429)

        // Exhaustiveness warnings (SPY0463)
        public const string NonExhaustiveMatch = "SPY0463";         // Active

        // Deprecation warnings (SPY0464)
        public const string DeprecatedBodylessSyntax = "SPY0464";   // Active

        // Identity operator warnings (SPY0465)
        public const string IsWithValueTypes = "SPY0465";           // Active

        // Deprecated usage warnings (SPY0466)
        public const string DeprecatedUsage = "SPY0466";            // Active

        // Readonly property violation (SPY0467)
        public const string ReadonlyPropertyAssignment = "SPY0467"; // Active
        // Constant pattern shadow warning (SPY0468)
        public const string ConstantPatternShadow = "SPY0468";    // Active
        // SPY0469: Reserved for future validation warnings

        #endregion

        #region Transition diagnostics (SPY0470-SPY0489)

        // Hint-severity diagnostics that warn Python/C# developers about behavioral
        // differences in Sharpy. These share the validation-warning code range but
        // are emitted at Hint severity (advisory; not promoted to errors under
        // -Werror) and share suppression with warnings.

        public const string Utf16StringLengthHint = "SPY0470";        // Active (emitted by TransitionWarningValidator; pending #611 for builtin len resolution)
        public const string StructValueSemanticsHint = "SPY0471";     // Active
        public const string HomogeneousVariadicHint = "SPY0472";      // Allocated — not yet emitted
        public const string NoClassmethodHint = "SPY0473";            // Allocated — not yet emitted
        public const string NoAsyncComprehensionHint = "SPY0474";     // Allocated — not yet emitted
        public const string SingleIsinstanceTypeHint = "SPY0475";     // Active
        public const string NegativeTupleIndexHint = "SPY0476";       // Allocated — not yet emitted
        public const string UnnecessaryStaticDecoratorHint = "SPY0477"; // Active
        // SPY0478-SPY0489: Reserved for future transition diagnostics

        #endregion
    }

    /// <summary>
    /// Code generation diagnostic codes (SPY0500-SPY0599).
    /// Active: SPY0500-SPY0508, SPY0510, SPY0518-SPY0520, SPY0522-SPY0523, SPY0550-SPY0554, SPY0599 (21 codes)
    /// Reserved: SPY0521 (TypeReExportNotSupported — for future type re-export support)
    /// Reserved: SPY0509, SPY0511-SPY0517, SPY0524-SPY0549, SPY0555-SPY0569 (source generators), SPY0570-SPY0598 (67 codes)
    /// </summary>
    public static class CodeGen
    {
        #region Core codegen errors (SPY0500-SPY0510)

        public const string EmitError = "SPY0500";                  // Active
        public const string UnsupportedFeature = "SPY0501";         // Active
        public const string EmptyClassName = "SPY0502";             // Active
        public const string DuplicateMember = "SPY0503";            // Active
        public const string EmptyMethodName = "SPY0504";            // Active
        public const string AbstractMethodWithBody = "SPY0505";     // Active
        public const string NonAbstractMethodWithoutBody = "SPY0506"; // Active
        public const string VarWithoutInitializer = "SPY0507";      // Active
        public const string PositionalPatternFallback = "SPY0508";  // Active
        // SPY0509: Reserved
        public const string UnrecognizedStatementType = "SPY0510";  // Active

        #endregion

        #region Expression and operator errors (SPY0518-SPY0523)

        // SPY0511-SPY0517: Reserved for future statement-level codegen diagnostics
        public const string UnsupportedExpressionType = "SPY0518";  // Active
        public const string UnsupportedOperator = "SPY0519";        // Active
        public const string NameCollision = "SPY0520";              // Active
        public const string TypeReExportNotSupported = "SPY0521";   // Reserved — for future type re-export support
        public const string MemberNameCollision = "SPY0522";        // Active
        public const string FunctionModuleClassCollision = "SPY0523"; // Active
        // SPY0524-SPY0549: Reserved for future codegen diagnostics

        #endregion

        #region Source generator errors (SPY0550-SPY0569)

        public const string GeneratorExecutionError = "SPY0550";      // Active
        public const string GeneratorTimeout = "SPY0551";             // Active
        public const string GeneratorInvalidSource = "SPY0552";       // Active
        public const string GeneratorCycleDetected = "SPY0553";       // Active
        public const string GeneratorEmptyOutput = "SPY0554";         // Active
        // SPY0555-SPY0569: Reserved for future source generator diagnostics

        #endregion

        #region Internal errors (SPY0599)

        public const string InternalGeneratedCSharpParseError = "SPY0599"; // Active

        #endregion
    }

    /// <summary>
    /// Informational diagnostic codes (SPY1000-SPY1099).
    /// Non-error, non-warning notes emitted during compilation.
    /// Active: SPY1001, SPY1010 (2 codes)
    /// Reserved: SPY1000, SPY1002-SPY1009, SPY1011-SPY1099 (97 codes)
    /// </summary>
    public static class Info
    {
        public const string ImplicitInterfaceSynthesis = "SPY1001"; // Active
        public const string FunctoolsPartialPlaceholderHint = "SPY1010"; // Active
        // SPY1002-SPY1009, SPY1011-SPY1099: Reserved for future informational diagnostics
    }

    /// <summary>
    /// Infrastructure diagnostic codes (SPY0900-SPY0999).
    /// These cover compiler-level errors not tied to a specific language phase.
    /// Active: SPY0900-SPY0907 (8 codes)
    /// Reserved: SPY0908-SPY0999 (92 codes)
    /// </summary>
    public static class Infrastructure
    {
        public const string CompilationFailed = "SPY0900";          // Active
        public const string CompilationCancelled = "SPY0901";       // Active
        public const string AssemblyCompilationFailed = "SPY0902";  // Active
        public const string FileReadError = "SPY0903";              // Active
        public const string InvariantViolation = "SPY0904";         // Active
        public const string TooManyErrors = "SPY0905";              // Active
        public const string ParserLoopStall = "SPY0906";            // Active
        public const string UnexpectedUnknownType = "SPY0907";      // Active
        // SPY0908-SPY0999: Reserved for future infrastructure diagnostics
    }
}
