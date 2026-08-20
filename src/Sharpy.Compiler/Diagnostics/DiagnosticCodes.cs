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
    /// Generic diagnostic code used by <see cref="Logging.StructuredLogger"/> for
    /// unstructured log messages that don't originate from a specific compiler phase.
    /// </summary>
    public const string GenericError = "SPY0000";

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
        public const string InvalidFloatLiteral = "SPY0019";        // Reserved — covered by SPY0007 (#670)
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

        #region F-string expression errors (SPY0020-SPY0022, SPY0030)

        public const string UnterminatedFStringExpression = "SPY0020"; // Active
        public const string UnmatchedBraceInFString = "SPY0021";    // Active
        public const string UnterminatedFormatSpec = "SPY0022";      // Active
        public const string InvalidFStringConversion = "SPY0030";   // Active

        #endregion

        #region Backtick identifier errors (SPY0025)

        public const string DotInBacktickIdentifier = "SPY0025";    // Deprecated: dots now allowed in backtick identifiers

        #endregion

        #region Byte string errors (SPY0026-SPY0028)

        public const string UnterminatedByteString = "SPY0026";    // Active
        public const string UnicodeEscapeInByteString = "SPY0027"; // Active
        public const string NonAsciiInByteString = "SPY0028";      // Active

        #endregion

        #region D-string (dedented) errors (SPY0029)

        public const string DedentedStringIndentationError = "SPY0029"; // Active

        #endregion

        // SPY0031-SPY0099: Reserved for future lexer diagnostics
    }

    /// <summary>
    /// Parser diagnostic codes (SPY0100-SPY0199).
    /// Active: SPY0100-SPY0141
    /// Reserved: SPY0142-SPY0199
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
        public const string VariadicNotLast = "SPY0112";            // Reserved — covered by SPY0110 (#671)

        #endregion

        #region Type and collection syntax (SPY0113-SPY0118)

        public const string FreeUnionNotSupported = "SPY0113";      // Active
        public const string EmptyListShorthand = "SPY0114";         // Active
        public const string EmptySetDictShorthand = "SPY0115";      // Active
        public const string ExpectedModuleName = "SPY0116";         // Active
        public const string ExpectedDecoratorName = "SPY0117";      // Active
        public const string MixedNamedUnnamedTupleElements = "SPY0118"; // Active

        #endregion

        #region Control flow and pattern parsing (SPY0119-SPY0125, SPY0140)

        public const string MaxRecursionDepthExceeded = "SPY0119";  // Active
        public const string ExpectedPattern = "SPY0120";            // Active
        public const string ExpectedCase = "SPY0121";               // Active
        public const string RaiseFromNotSupported = "SPY0122";      // Active
        public const string DictSpreadCallNotSupported = "SPY0123"; // Active
        public const string EmptyUnion = "SPY0124";                 // Active
        public const string GenericTypeInPattern = "SPY0125";       // Active
        public const string MultipleStarsInPattern = "SPY0140";     // Active

        #endregion

        #region Parameter marker and placeholder errors (SPY0126-SPY0133)

        public const string SlashAfterStar = "SPY0126";             // Active
        public const string DuplicateSlashMarker = "SPY0127";       // Active
        public const string DuplicateStarMarker = "SPY0128";        // Active
        public const string SlashAtStart = "SPY0129";               // Active
        public const string PlaceholderInKeywordArg = "SPY0130";    // Reserved — keyword '_' placeholders are now supported
        public const string PlaceholderWithSpread = "SPY0131";      // Active
        public const string PlaceholderOutsideCallOrOperator = "SPY0132"; // Active (emitted by TypeChecker.CheckIdentifier)
        public const string NestedPlaceholder = "SPY0133";          // Active (emitted by Parser.LowerPartialApplicationCall)

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

        #region Lambda operand grammar (SPY0141)

        public const string LambdaNotAllowedAsOperand = "SPY0141";  // Active

        #endregion

        // SPY0142-SPY0199: Reserved for future parser diagnostics
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
        // Question mark operator in finally (SPY0211)
        public const string QuestionMarkInFinally = "SPY0211";           // Active
        // SPY0210: Integer literal out of range for its type (#1314). Never previously issued —
        // established via full-history pickaxe (3 comment-only commits: e98b3d7c4, 4738b9265, 5b40df995).
        public const string IntegerLiteralOutOfRange = "SPY0210";       // Active
        // Claims the SPY0212 reservation made for round-8 batch D. The bare spelling of a builtin
        // name always denotes the builtin — types and functions uniformly — so a user symbol with
        // that spelling must be backtick-escaped. Extends the rule hard keywords already follow
        // (bare `class` is the keyword; `` `class` `` is your symbol) to the builtin namespace.
        public const string BuiltinNameShadowed = "SPY0212";         // Active
        // SPY0213 is borrowed from this reserve by the shift family (#1315), whose own operator band
        // (SPY0320-SPY0326) and the module-level reserve (SPY0340-SPY0349) are both fully allocated —
        // the same borrowing SPY0348 documents. A constant negative shift count: CPython raises
        // ValueError, C# masks the count to 5/6 bits and computes a silently different value
        // (`1 << -1` is -2147483648). Runtime negative counts keep .NET's masking (Axiom 1).
        public const string NegativeConstantShiftCount = "SPY0213";  // Active
        public const string BuiltinsNoneLiteral = "SPY0214";  // Active — builtins.None refused
        // SPY0215-SPY0219: Reserved for future name resolution diagnostics

        #endregion

        #region Type checking (SPY0220-SPY0259)

        public const string TypeMismatch = "SPY0220";               // Active
        public const string IncompatibleTypes = "SPY0221";          // Reserved — covered by SPY0220 (#672)
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
        public const string InvalidComprehension = "SPY0238";       // Reserved — covered by SPY0241 (#672)
        public const string InvalidTupleUnpacking = "SPY0239";      // Active
        public const string InvalidAutoVariable = "SPY0240";        // Active
        public const string ConditionNotBoolean = "SPY0241";        // Active
        public const string InvalidRaise = "SPY0242";               // Active
        public const string InvalidMaybeExpression = "SPY0243";     // Active
        public const string InvalidNoneConstructor = "SPY0244";     // Active
        public const string InvalidSomeConstructor = "SPY0245";     // Active (emitted by TypeChecker.TryCheckTaggedUnionConstructor)
        public const string InvalidOkErrConstructor = "SPY0246";    // Active (emitted by TypeChecker.CheckFunctionCall)
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
        public const string VoidMatchScrutinee = "SPY0275";         // Active
        // SPY0276-SPY0279: Reserved for future control flow diagnostics

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
        // SPY0312 takes the next slot in this sub-band rather than a redirect-only one: the label
        // "redirect" was aspirational, the BAND is imports, and an aliased-import refusal is an
        // import diagnostic reported at the import statement. Renumbering the two redirects to
        // carve a new sub-band would break shipped `.error` fixtures for no gain.
        // Retired — superseded by alias transparency (#1527); reserved, never reused
        public const string BuiltinTypeAliasUnsupported = "SPY0312";
        // SPY0313-SPY0319: Reserved for future import diagnostics

        #endregion

        #region Protocol and operator (SPY0320-SPY0326)

        public const string ProtocolMissingMethod = "SPY0320";      // Active
        public const string InvalidOperatorSignature = "SPY0321";   // Active
        public const string InvalidDecoratorUsage = "SPY0322";      // Active
        public const string ConflictingSynthesizedInterface = "SPY0323"; // Active
        public const string WithNotDisposable = "SPY0324";          // Active
        public const string InterfaceMethodNotImplemented = "SPY0325"; // Active
        public const string OptionalRequiresNarrowing = "SPY0326";  // Active
        public const string TupleNonConstantIndex = "SPY0327";      // Active
        public const string IntegerPowerOverflow = "SPY0328";       // Active
        // This code's constant-arithmetic sibling — ConstantIntegerOverflow (#1234): constant +/-/*
        // whose exact result exceeds the result type. No slot was free adjacent to SPY0328, so it
        // borrows SPY0348 from the module-level reserve; declared in that region.
        public const string ConstantIntegerOverflow = "SPY0348";    // Active
        public const string VoidComparisonOperand = "SPY0329";      // Active
        public const string UnknownFutureFeature = "SPY0330";       // Active — from __future__ import of an unknown or mis-scoped feature
        public const string FeatureNotEnabled = "SPY0331";          // Active — use of a construct gated behind an experimental feature that is not enabled
        public const string DeferOutsideFunction = "SPY0332";       // Active — defer statement used outside a function body
        public const string DeferControlFlowEscape = "SPY0333";     // Active — return/break/continue/yield escapes a deferred statement (would leave a finally block)
        public const string RedundantNullableCastTarget = "SPY0334"; // Active — nullable target on the as?/as! failable-cast operators (#1029); the operator owns the failure mode
        public const string GenericFunctionReferenceNotCalled = "SPY0335"; // Active — a generic function reference with explicit type args used as a value instead of being called (#1138)
        public const string AmbiguousCallableReference = "SPY0336";  // Active — an overloaded function/method referenced as a value, with no target type to select an overload (#1170)
        public const string CallSyntaxOnlyReference = "SPY0337";     // Active — a call-syntax-only form (isinstance, a union variant, a builtin type constructor) referenced as a value (#1168, #1170)
        public const string UnsupportedVariableArityTuple = "SPY0338"; // Active — tuple(iterable): a tuple's arity is part of its type, so a runtime-length iterable has no tuple type (#1159)
        public const string GenericTypeReferenceNotConstructed = "SPY0339"; // Active — a generic type reference with explicit type args used as a value instead of being constructed (#1192)
        // The value-position reference family (SPY0335-SPY0339) is full; it continues at SPY0342 below.

        #endregion

        #region Module level (SPY0340-SPY0341)

        public const string ModuleLevelExecutableStatement = "SPY0340"; // Active
        public const string ModuleLevelNoTypeAnnotation = "SPY0341"; // Active
        // SPY0342-SPY0345 were already borrowed from this reserve by families whose own bands filled:
        // SPY0342 the value-position reference family, SPY0343 the inference family, SPY0344-SPY0345
        // the type-test family (see the regions below).
        //
        // Round-8 allocation (2026-08-05) — three more families had no adjacent space, so they borrow
        // here under the same precedent. Reserved, not yet declared: a code is declared together with
        // its DiagnosticExplanations entry, because AllDiagnosticCodes_HaveExplanations reflects over
        // every declared const with no exemption list.
        //   SPY0346: TAKEN — NonConstructibleTypeReference (#1250, batch B), declared in the
        //            value-position reference overflow region below.
        //   SPY0347: TAKEN — TypeParameterDefaultForwardReference (#1245, batch E), declared in the
        //            generic type parameter default overflow region below.
        //   SPY0348: TAKEN — ConstantIntegerOverflow (#1234, batch C). Constant integer +/-/*
        //            whose exact result exceeds the expression's result type. Sibling of SPY0328
        //            (IntegerPowerOverflow), which has no free slot beside it; declared next to
        //            SPY0328 in the region above so the two read together.
        //   SPY0349: TAKEN — IsTypeTestRetired (#1298). `x is TypeName` retired as a type test;
        //            `is` is reference identity only; the remedy is isinstance. This was the last
        //            free slot in the semantic reserve. The next family needing space requires a
        //            structural decision (a new band, or a documented general-overflow region).
        public const string IsTypeTestRetired = "SPY0349";              // Active

        #endregion

        #region Value-position reference overflow (SPY0342)

        // Overflow for the value-position reference family, whose SPY0335-SPY0339 band is full. Kept
        // adjacent to that band rather than appended at the end of the semantic range so the family
        // stays readable; the module-level reserve above shrinks by one slot to pay for it.
        public const string UnpinnedConstructorReference = "SPY0342"; // Active — a builtin type constructor reference used as a value with no signature available to pin it to (#1182)

        // The family's second overflow slot, borrowed from the module-level reserve above under the
        // same precedent as SPY0342. Sibling rather than a flavor of SPY0342: that code means "this
        // position supplies no signature to select", which presumes a signature exists; this one
        // means there is no construction to refer to at all, so no position could ever pin it.
        //
        // Scoped to the kinds that genuinely have no construction — measured under `run`, not
        // assumed, because SPY0908 never appears under `emit diagnostics` (#1250). An interface,
        // enum, union, delegate and abstract class have none. object/bytes/decimal/frozenset/
        // frozendict/Iterator/the view types DO construct, so they are not this code's business
        // even though a REFERENCE to any of them leaks the same SPY0908; that gap is #1272.
        public const string NonConstructibleTypeReference = "SPY0346"; // Active — an interface, enum, union, delegate or abstract class name used as a value, where no construction exists to refer to (#1250)

        #endregion

        #region Inference overflow (SPY0343)

        // Overflow for the inference family — CannotInferType (SPY0227) and CannotInferGenericType
        // (SPY0237) — whose SPY0220-SPY0274 type-error sub-band has no free slot. Borrowed from the
        // module-level reserve above, following the SPY0342 precedent; declared here rather than
        // beside SPY0227 because this file is ordered by ascending region.
        public const string UnresolvedLambdaParameterType = "SPY0343"; // Active — a lambda bound to a name whose unannotated parameter types could not be inferred, and the binding supplies no expected type (#1212)

        #endregion

        #region Type-test operands (SPY0344-SPY0345)

        // The isinstance type-operand classifier's rejections. Both say the same thing in two shapes:
        // a type test must name ONE closed type, because a test that compiles but cannot narrow is
        // worse than a clean refusal (#1207, #1213). Borrowed from the module-level reserve above,
        // which the semantic range's exhaustion left as the only contiguous space. (SPY0343 was
        // reserved when this was written; the inference-overflow region above has since taken it.)
        public const string MultiTypeTypeTest = "SPY0344";          // Active — isinstance against a tuple of types; Sharpy keeps the form single-typed so a successful check narrows (#1213)
        public const string OpenGenericTypeTest = "SPY0345";        // Active — isinstance against a bare generic type name whose type arguments the operand does not determine (#1207)

        #endregion

        #region Generic type parameter default overflow (SPY0347)

        // Overflow for the "Generic type parameter default errors" family (SPY0395-SPY0396), which
        // is boxed in on both sides — SPY0394 is ReturnInExceptStar, SPY0397 is
        // ExceptionFilterNotBoolean — so its third member is borrowed from the module-level reserve
        // above, the same precedent as SPY0342/SPY0343. Deliberately NOT named "ordering": SPY0395
        // already owns that word for "a non-default parameter follows a defaulted one", which is a
        // different rule from this one.
        public const string TypeParameterDefaultForwardReference = "SPY0347"; // Active — a type-parameter default naming a parameter declared after it, itself, or one from an enclosing scope (#1245)

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
        public const string DuplicateCaptureInPattern = "SPY0372";  // Active — same name bound on both sides of an and-pattern

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
        // This band is full (SPY0394 below, SPY0397 above), so the family's third member lives at
        // SPY0347 (TypeParameterDefaultForwardReference, #1245) — see the overflow region above.
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
    /// Transition hints: SPY0470-SPY0479 (advisory; emitted at Hint severity),
    /// Warnings — overflow of 0450-0469: SPY0480-SPY0489.
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
        // SPY0417: variance not allowed on class/struct/method/function type parameters
        public const string VarianceNotAllowed = "SPY0417";    // Active
        public const string CovariantInContravariantPosition = "SPY0418"; // Active
        public const string ContravariantInCovariantPosition = "SPY0419"; // Active

        // Event validation (SPY0420-SPY0423)
        public const string UnpairedEventAccessor = "SPY0420";      // Active
        public const string EventFieldNameConflict = "SPY0421";     // Active
        public const string EventMethodNameConflict = "SPY0422";    // Active
        public const string AbstractEventWithBody = "SPY0423";      // Active
        public const string EventAccessorAbstractnessDisagreement = "SPY0424"; // Active

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
        // @test decorator validation (SPY0448-SPY0449)
        public const string TestDecoratorInvalidTarget = "SPY0448";      // Active
        public const string TestDecoratorInvalidCombination = "SPY0449"; // Active

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

        // Question mark operator errors (SPY0460-SPY0462)
        public const string QuestionMarkNotResultOrOptional = "SPY0460"; // Active
        public const string QuestionMarkIncompatibleReturn = "SPY0461"; // Active
        public const string QuestionMarkOutsideFunction = "SPY0462";    // Active

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
        // @test decorator argument validation (SPY0469)
        public const string TestDecoratorInvalidArgument = "SPY0469";    // Active

        #endregion

        #region Transition diagnostics (SPY0470-SPY0479)

        // Hint-severity diagnostics that warn Python/C# developers about behavioral
        // differences in Sharpy. These share the validation-warning code range but
        // are emitted at Hint severity (advisory; not promoted to errors under
        // -Werror) and share suppression with warnings.

        public const string Utf16StringLengthHint = "SPY0470";        // Active (emitted by TransitionWarningValidator; pending #611 for builtin len resolution)
        public const string StructValueSemanticsHint = "SPY0471";     // Active
        public const string HomogeneousVariadicHint = "SPY0472";      // Active (emitted by TransitionWarningValidator)
        public const string NoClassmethodHint = "SPY0473";            // Active (emitted by TransitionWarningValidator)
        public const string NoAsyncComprehensionHint = "SPY0474";     // Retired (#998 — async comprehensions implemented); reserved, never reused
        public const string SingleIsinstanceTypeHint = "SPY0475";     // Retired — superseded by SPY0344 (#1213): the rejection is a semantic-time error, not a hint, so the tuple form never reaches codegen; reserved, never reused
        public const string NegativeTupleIndexHint = "SPY0476";       // Active (emitted by TransitionWarningValidator)
        public const string UnnecessaryStaticDecoratorHint = "SPY0477"; // Active
        public const string AliasedCollectionAugmentedAssignmentHint = "SPY0478"; // Active (emitted by TransitionWarningValidator)
        // SPY0479: Retired (#1127 — `to` cast operator removed in 0.8.0); reserved, never reused
        // The transition-hint band is now FULL: 0470-0473, 0476-0478 active; 0474/0475/0479
        // retired-and-never-reused. It does NOT extend into SPY0480-SPY0489 — that is the
        // validation-WARNING overflow band (SPY0480-0484 active, see below). The LSP's
        // DiagnosticPublisher.IsTransitionHintCode nevertheless treats 470-489 as transition
        // hints by range, which over-claims those five warnings (#1466). The next transition
        // hint needs a band decision, not a free slot.

        #endregion

        #region Validation warnings — overflow (SPY0480-SPY0489)

        // The primary validation-warning sub-band SPY0450-SPY0469 is fully allocated, so
        // warnings added afterward overflow into SPY0480-SPY0489. These are WARNINGS despite
        // sitting numerically above the transition-hint band; severity is set explicitly at
        // emission via AddWarning, never inferred from the code range (mirrors the SPY0490-0499
        // error-overflow band precedent).

        public const string MustUseValueDiscarded = "SPY0480";       // Active (#1022)
        public const string UnusedSuppression = "SPY0481";           // Active (#1024)
        public const string InvalidSuppressionCode = "SPY0482";      // Active (#1024)
        // A binding in VALUE position — variable, parameter, for-target, with-as, except-as, or a
        // function declaration — spells a builtin name. Legal and honoured (the binding shadows the
        // builtin, C#-style), so this is a warning and not the SPY0212 refusal: only a TYPE
        // declaration can make an annotation ambiguous, and only that is refused.
        public const string BuiltinNameShadowedInValuePosition = "SPY0483";  // Active (#1241)
        // An EXPLICIT `from M import len` rebinding a builtin name in the consumer's file. The
        // star-import form of the same collision is refused at the use site (SPY0492); naming the
        // import is the statement of intent that resolves the ambiguity, so this is a warning —
        // but the rebinding is still worth saying out loud in the file where it takes effect,
        // because SPY0483 fires in the DECLARING file, which the consumer may never open (#1324).
        public const string BuiltinRebornByExplicitImport = "SPY0484";  // Active (#1324)
        // SPY0485-SPY0489: Reserved for future validation warnings

        #endregion

        #region Validation errors — overflow (SPY0490-SPY0499)

        // The primary validation-error sub-band SPY0400-SPY0449 is fully allocated, so
        // validation errors added after Wave 3 overflow into SPY0490-SPY0499 (the tail of the
        // Validation range, previously unused). These are ERRORS despite the numeric position;
        // severity is set explicitly at emission via AddError, never inferred from the code range.

        // Property observer validation (SPY0490-SPY0491, #416)
        public const string PropertyObserverInvalidTarget = "SPY0490"; // Active
        public const string DuplicatePropertyObserver = "SPY0491";     // Active

        // A name bound by `from M import *` that displaces a builtin, used unqualified. An ERROR
        // despite sitting in the Validation range — severity is set at emission, per this band's
        // rule. Reported at the USE, not at the import, which is C#'s CS0104: `using` directives
        // that merely COULD collide are fine, and only an ambiguous reference is refused. An
        // explicit `from M import sum` is the statement of intent that resolves it, exactly as a
        // C# `using Foo = A.B;` alias resolves what two bare `using`s could not (#1324).
        public const string AmbiguousGlobImportOfBuiltin = "SPY0492";  // Active (#1324)

        // Abstract member in non-abstract class (SPY0493, #1307)
        public const string AbstractMemberInNonAbstractClass = "SPY0493"; // Active

        // SPY0494: Retired (#1413 — assert_raises lowers to a flag/try-catch/Sharpy.AssertionError
        // that names no test framework, so the @test-only restriction it enforced for #1283 no
        // longer has anything to protect); reserved, never reused

        // A bracket attribute @[...] naming a type that is in scope nowhere (SPY0495, #1427).
        // Distinct from SPY0444 UnknownDecorator, whose remedy is "use @[...] instead" — advice
        // that says nothing to a name already written in bracket syntax.
        public const string UnknownBracketAttribute = "SPY0495"; // Active (#1427)

        // An accessor's parameter list is not expressible as a C# accessor's (SPY0496, #1406).
        // One code, because it is one rule: a property or event accessor receives exactly the
        // value C# hands it — a setter/init gets the implicit `value`, a getter gets nothing —
        // so `self` plus at most one value parameter is the entire expressible shape. A variadic
        // and a getter's extra parameter are the two ways to leave it.
        public const string AccessorParameterNotExpressible = "SPY0496"; // Active (#1406, #1405)

        // `Self` in a VARIANT position of an interface member — nested in a generic argument,
        // tuple element, or wrapped optional/nullable (`list[Self]`, `Self?`) — has no sound
        // explicit-interface bridge, because C# generics are invariant (`List<Box>` is not
        // `List<IGroupable>`). A top-level `Self` return/parameter IS bridged (SPY0497, #1342 shape 2).
        public const string SelfInVariantInterfacePosition = "SPY0497"; // Active (#1342)

        // A bare payload TYPE pattern over a tagged-union scrutinee (`case str():` over `str?`,
        // `case int():` over `int !E`) is refused with a steer to the constructor-case spellings
        // (#1510, #1476). Emitted at SEMANTIC time from TypeChecker's pattern arm — the code lives
        // in this general validation-overflow band because the semantic range SPY0200-SPY0399 is
        // fully allocated (its own SPY0349 note records that as the last free semantic slot), and a
        // pattern-well-formedness refusal belongs to no family-specific semantic reserve. This is
        // the SPY0522/SPY0523 precedent (a semantic-phase diagnostic taking a number from a later
        // band); unlike those it stays in-scope [0001,0499] for the front-end parity sweep.
        public const string PayloadTypePatternOverUnion = "SPY0498"; // Active (#1510, #1476)

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
    /// Active: SPY0900-SPY0910 (11 codes)
    /// Reserved: SPY0911-SPY0999 (89 codes)
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
        public const string GeneratedCodeCompilationError = "SPY0908"; // Active
        public const string InternalCompilerError = "SPY0909";      // Active

        /// <summary>
        /// The metadata reference set assembled for the generated C# does not contain an assembly
        /// defining <c>System.Object</c>, so the compilation cannot succeed. Reported instead of
        /// letting Roslyn produce a wall of CS0518 that the SPY0908 net would then attribute to a
        /// Sharpy compiler bug (#1482).
        /// </summary>
        public const string ReferenceAcquisitionFailed = "SPY0910"; // Active
        // SPY0911-SPY0999: Reserved for future infrastructure diagnostics
    }
}
