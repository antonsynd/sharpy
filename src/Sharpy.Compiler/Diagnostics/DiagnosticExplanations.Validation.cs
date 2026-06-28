namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// DiagnosticExplanations partial: Validation diagnostic entries (SPY0400-SPY0499)
/// </summary>
public static partial class DiagnosticExplanations
{
    private static void AddValidationEntries(Dictionary<string, DiagnosticExplanation> dict)
    {
        // ── Validation errors (SPY0400-SPY0499) ────────────────────────

        Add(dict, DiagnosticCodes.Validation.MutableDefault, "Mutable default parameter", "Validation",
            "A function parameter has a mutable default value (list, dict, or set literal). In Python, mutable defaults are shared across calls, leading to subtle bugs. Sharpy prevents this pattern.",
            "def append_to(item: int, lst: list[int] = []) -> list[int]:\n    lst.append(item)\n    return lst",
            "Use None as the default and create the mutable object inside the function:\ndef append_to(item: int, lst: Optional[list[int]] = None) -> list[int]:\n    if lst is None:\n        lst = []\n    lst.append(item)\n    return lst");

        Add(dict, DiagnosticCodes.Validation.NonConstDefault, "Non-constant default parameter value", "Validation",
            "A function parameter has a default value that is not a compile-time constant. Default values must be literals or constants.",
            "x: int = 10\ndef foo(n: int = x):  # x is not a constant\n    pass",
            "Use a literal or constant default:\ndef foo(n: int = 10):\n    pass");

        Add(dict, DiagnosticCodes.Validation.UnsupportedOperator, "Unsupported operator for type", "Validation",
            "An operator was used with types that don't support it. The validation pipeline checks operator compatibility beyond basic type checking.",
            null,
            "Use an operator that is supported for the given types, or implement the corresponding dunder method on your class.");

        Add(dict, DiagnosticCodes.Validation.MissingMainFunction, "Missing main() function", "Validation",
            "The program does not define a main() function. Every Sharpy program must have a main() function as its entry point.",
            "def helper() -> int:\n    return 42\n# no main function",
            "Add a main function:\ndef main():\n    result: int = helper()\n    print(result)");

        Add(dict, DiagnosticCodes.Validation.InvalidNullCoalesce, "Invalid null-coalescing operator usage", "Validation",
            "The null-coalescing operator (??) was used with a left operand that is not nullable, or with incompatible types.",
            "x: int = 42\ny: int = x ?? 0  # x is never null",
            "Only use ?? with Optional types:\n  x: Optional[int] = get_value()\n  y: int = x ?? 0");

        // ── Validation errors: Property validation (SPY0405-SPY0415) ───

        Add(dict, DiagnosticCodes.Validation.PropertyFieldNameConflict,
            "Property conflicts with field name",
            "Validation",
            "A property has the same name as a field in the same class or struct. Properties and fields occupy the same namespace and cannot share names.",
            "class Foo:\n    name: str\n    property get name(self) -> str:\n        return self._name",
            "Rename the field or the property to avoid the conflict.");

        Add(dict, DiagnosticCodes.Validation.PropertyMethodNameConflict,
            "Property conflicts with method name",
            "Validation",
            "A property has the same name as a method in the same class or struct. Properties and methods cannot share names because they both generate members on the C# type.",
            "class Foo:\n    property get name(self) -> str:\n        return self._name\n    def name(self) -> str:\n        return self._name",
            "Rename the method or the property to avoid the conflict.");

        Add(dict, DiagnosticCodes.Validation.MixedAutoAndFunctionStyleProperty,
            "Mixed auto-property and function-style property",
            "Validation",
            "The same property name has both auto-property and function-style definitions. A property must be either entirely auto-property or entirely function-style.",
            "class Foo:\n    property name: str\n    property get name(self) -> str:\n        return self._name",
            "Choose one style: either auto-property or function-style with getter/setter bodies.");

        Add(dict, DiagnosticCodes.Validation.InitOnlyFunctionStyleProperty,
            "'property init' used with function-style property",
            "Validation",
            "The 'property init' accessor is only valid for auto-properties. Function-style properties cannot use init-only semantics.",
            "class Foo:\n    property init name(self, value: str):\n        self._name = value",
            "Use 'property set' for function-style setters, or switch to an auto-property with 'property init name: str'.");

        Add(dict, DiagnosticCodes.Validation.AbstractPropertyMustHaveEllipsisBody,
            "@abstract property must have ellipsis body",
            "Validation",
            "A property decorated with @abstract must use '...' (ellipsis) as its body. Abstract properties declare the interface without providing an implementation.",
            "class Shape:\n    @abstract\n    property get area(self) -> float:\n        return 0.0",
            "Use ellipsis for the body:\n    @abstract\n    property get area(self) -> float: ...");

        Add(dict, DiagnosticCodes.Validation.FinalWithAbstractOrVirtual,
            "@final combined with @abstract or @virtual on property",
            "Validation",
            "A property cannot be both @final and @abstract or @virtual. @final prevents overriding, while @abstract/@virtual require it.",
            "class Foo:\n    @final\n    @abstract\n    property get name(self) -> str: ...",
            "Remove either @final or @abstract/@virtual.");

        Add(dict, DiagnosticCodes.Validation.InvalidPropertyOverride,
            "Invalid property override",
            "Validation",
            "A property with @override has no matching virtual or abstract property in the base class, or the types are incompatible. " +
            "Override properties must have a corresponding virtual or abstract property in the base class with a compatible type.",
            "class Base:\n    property get name(self) -> str:\n        return \"base\"\n\nclass Derived(Base):\n    @override\n    property get missing(self) -> str:\n        return \"derived\"",
            "Ensure the base class has a virtual or abstract property with the same name and compatible type.");

        Add(dict, DiagnosticCodes.Validation.FinalWithoutOverride,
            "@final without @override on method",
            "Validation",
            "A method is marked @final but not @override. The @final decorator prevents further overriding, " +
            "but only makes sense on a method that is itself an override of a virtual or abstract base method.",
            "class Base:\n    @virtual\n    def greet(self) -> str:\n        return \"hello\"\n\n" +
            "class Child(Base):\n    @final\n    def greet(self) -> str:  # missing @override\n        return \"hi\"",
            "Add @override before @final:\nclass Child(Base):\n    @override\n    @final\n    def greet(self) -> str:\n        return \"hi\"");

        Add(dict, DiagnosticCodes.Validation.DunderInUserInterface,
            "Dunder method in user-defined interface",
            "Validation",
            "Dunder methods (e.g., __len__, __str__, __eq__) cannot be declared in user-defined interfaces. " +
            "Only standard library interfaces (ISized, IBoolConvertible, etc.) may declare dunder methods. " +
            "User-defined interfaces should declare regular methods instead.",
            "interface IMyProtocol:\n    def __len__(self) -> int:\n        ...",
            "Use a regular method name in your interface:\ninterface IMyProtocol:\n    def get_length(self) -> int:\n        ...");

        Add(dict, DiagnosticCodes.Validation.UnknownDunderMethod,
            "Unknown dunder method",
            "Validation",
            "Only recognized operator and protocol dunder methods are supported. " +
            "Unknown dunder methods like __custom__ are rejected at compile time. " +
            "Recognized operator dunders include __add__, __sub__, __eq__, __ne__, __lt__, __gt__, etc. " +
            "Recognized protocol dunders include __len__, __bool__, __str__, __iter__, __next__, __contains__, __reversed__, etc.",
            "class Foo:\n    def __custom__(self) -> int:\n        return 42",
            "Use a regular method name instead:\nclass Foo:\n    def custom(self) -> int:\n        return 42");

        Add(dict, DiagnosticCodes.Validation.VirtualOnStructMethod,
            "@virtual on struct method",
            "Validation",
            "Struct methods cannot be marked @virtual because structs are implicitly sealed in C# — " +
            "they cannot be inherited from. The @virtual decorator only makes sense on class methods.",
            "struct Point:\n    x: int\n    @virtual\n    def __str__(self) -> str:\n        return \"point\"",
            "Remove the @virtual decorator:\nstruct Point:\n    x: int\n    def __str__(self) -> str:\n        return \"point\"");

        Add(dict, DiagnosticCodes.Validation.NonExhaustiveMatchExpression, "Non-exhaustive match expression", "Validation",
            "A match expression does not cover all possible values of the scrutinee type. Match expressions must be exhaustive because they produce a value. " +
            "For enums, all members must be covered. For bools, both True and False must be covered. For tagged unions, all cases must be covered. " +
            "A wildcard pattern (_) or binding pattern covers all remaining cases. Guard clauses do not count toward exhaustiveness.",
            "union Option:\n    case Some(value: int)\n    case None_\nx: int = match opt:\n    case Some(v): v  # missing None_ case",
            "Cover all cases or add a wildcard:\nx: int = match opt:\n    case Some(v): v\n    case _: 0");

        Add(dict, DiagnosticCodes.Validation.VarianceNotAllowed, "Variance annotation not allowed here", "Validation",
            "Type parameter variance annotations (in/out) are only allowed on delegate and interface declarations. " +
            "Classes, structs, methods, and functions cannot have variant type parameters because their type parameters " +
            "may appear in both input and output positions.",
            "class Box[out T]:  # error: variance not allowed on class\n    value: T",
            "Remove the variance annotation:\nclass Box[T]:\n    value: T\n\nOr use a delegate or interface instead:\ndelegate Producer[out T]() -> T");

        Add(dict, DiagnosticCodes.Validation.CovariantInContravariantPosition, "Covariant type parameter in contravariant position", "Validation",
            "A type parameter declared as covariant (out) appears in a contravariant position (e.g., as a parameter type). " +
            "Covariant type parameters can only appear in output positions such as return types.",
            "delegate BadHandler[out T](value: T) -> None  # error: T is covariant but used as parameter",
            "Change the variance to 'in' or remove it:\ndelegate Handler[in T](value: T) -> None");

        Add(dict, DiagnosticCodes.Validation.ContravariantInCovariantPosition, "Contravariant type parameter in covariant position", "Validation",
            "A type parameter declared as contravariant (in) appears in a covariant position (e.g., as a return type). " +
            "Contravariant type parameters can only appear in input positions such as parameter types.",
            "delegate BadProducer[in T]() -> T  # error: T is contravariant but used as return type",
            "Change the variance to 'out' or remove it:\ndelegate Producer[out T]() -> T");


        // ── Event validation (SPY0420-SPY0423) ───────────────────────────

        Add(dict, DiagnosticCodes.Validation.UnpairedEventAccessor, "Unpaired event accessor", "Validation",
            "An event declaration has a function-style accessor (add/remove) without both accessors. Auto-events (without parentheses) generate both add and remove automatically. Function-style events with custom logic must define both.",
            "event add on_click(self, handler: EventHandler):  # error: missing remove accessor\n    pass",
            "Add the missing accessor or use auto-event syntax:\nevent add on_click(self, handler: EventHandler):\n    pass\nevent remove on_click(self, handler: EventHandler):\n    pass");

        Add(dict, DiagnosticCodes.Validation.EventFieldNameConflict, "Event conflicts with field name", "Validation",
            "An event has the same name as a field in the same class or struct. Events and fields occupy the same namespace and cannot share names.",
            "class Button:\n    on_click: str  # field\n    event on_click: EventHandler  # error: conflicts with field",
            "Rename either the field or the event to avoid the conflict.");

        Add(dict, DiagnosticCodes.Validation.EventMethodNameConflict, "Event conflicts with method name", "Validation",
            "An event has the same name as a method in the same class or struct. Events and methods occupy the same namespace and cannot share names.",
            "class Button:\n    def on_click(self) -> None: pass  # method\n    event on_click: EventHandler  # error: conflicts with method",
            "Rename either the method or the event to avoid the conflict.");

        Add(dict, DiagnosticCodes.Validation.AbstractEventWithBody, "Abstract event must not have a body", "Validation",
            "An abstract event has a function-style accessor with a body. Abstract events define only the signature and cannot provide an implementation.",
            "abstract event on_click(self, handler: EventHandler):  # error: abstract events cannot have implementation\n    pass",
            "Remove the body or remove the abstract keyword:\nabstract event on_click(self, handler: EventHandler)  # no body\n\nOr:\nevent add on_click(self, handler: EventHandler):\n    pass");

        // ── Decorator argument validation (SPY0425) ─────────────────────

        Add(dict, DiagnosticCodes.Validation.NonConstantDecoratorArgument, "Decorator argument must be a compile-time constant", "Validation",
            "Custom decorator arguments must be compile-time constant expressions because they map to C# attribute arguments. Allowed: string, int, float, bool literals, None, enum member access (e.g., MyEnum.value), and type(X).",
            "@custom(1 + 2)  # error: arithmetic expression is not a compile-time constant\ndef foo():\n    pass",
            "Use a literal value instead:\n@custom(3)\ndef foo():\n    pass");

        Add(dict, DiagnosticCodes.Validation.InitPropertyNotAssigned, "Init property not assigned in constructor", "Validation",
            "A 'property init' field without a default value must be assigned in every constructor (__init__). Init properties are set-once, so they must be initialized during construction.",
            "class Config:\n    property init port: int\n\n    def __init__(self):\n        pass  # error: 'port' not assigned",
            "Assign the init property in the constructor:\ndef __init__(self):\n    self.port = 8080");

        // ── Validation warnings (SPY0450-SPY0499) ──────────────────────

        Add(dict, DiagnosticCodes.Validation.UnreachableCodeWarning, "Unreachable code detected", "Validation",
            "Code after a return, raise, break, or continue statement can never be executed. This usually indicates dead code that should be removed.",
            "def foo() -> int:\n    return 1\n    x: int = 2  # unreachable",
            "Remove the unreachable code:\ndef foo() -> int:\n    return 1");

        Add(dict, DiagnosticCodes.Validation.UnusedVariable, "Unused variable", "Validation",
            "A local variable is assigned a value but never read. This often indicates a typo in a variable name or leftover debugging code.",
            "def foo():\n    x: int = 42  # x is never used\n    print(\"hello\")",
            "Remove the unused variable, or prefix it with underscore if intentionally unused:\ndef foo():\n    _x: int = 42  # intentionally unused\n    print(\"hello\")");

        Add(dict, DiagnosticCodes.Validation.UnusedImport, "Unused import", "Validation",
            "An imported name is never referenced in the module. Unused imports clutter the code and slow down compilation.",
            "from math import sqrt, pi  # pi is never used\ndef main():\n    print(sqrt(4))",
            "Remove the unused import:\nfrom math import sqrt\ndef main():\n    print(sqrt(4))");

        Add(dict, DiagnosticCodes.Validation.NamingConventionWarning,
            "Naming Convention Warning",
            "Naming",
            "Identifier contains consecutive underscores which may cause name collision after name mangling. " +
            "For example, 'foo__bar' and 'foo_bar' would both mangle to the same C# name.",
            "x: int = 1\nfoo__bar: int = 2  # warning: consecutive underscores",
            "Rename the identifier or use backtick escaping: `foo__bar`");

        Add(dict, DiagnosticCodes.Validation.EqWithoutObjectOverload,
            "__eq__ without object overload",
            "Validation",
            "A class defines '__eq__' but none of its overloads has parameter type 'object'. " +
            "Without '__eq__(self, other: object)', collections like set and dict will use reference equality " +
            "instead of value equality for instances of this class.",
            "class Point:\n    x: int\n    def __eq__(self, other: Point) -> bool:\n        return self.x == other.x",
            "Add an '__eq__(self, other: object)' overload, or if reference equality for collections is intended, suppress the warning.");

        Add(dict, DiagnosticCodes.Validation.EqObjectWithoutHash,
            "__eq__(object) without __hash__",
            "Validation",
            "A class defines '__eq__(self, other: object)' but not '__hash__'. " +
            "The .NET equality contract requires that if Equals is overridden, GetHashCode must also be overridden. " +
            "Without both, the type will behave incorrectly in hash-based collections (set, dict).",
            "class Foo:\n    x: int\n    def __eq__(self, other: object) -> bool:\n        return False",
            "Add a '__hash__(self) -> int' method:\nclass Foo:\n    x: int\n    def __eq__(self, other: object) -> bool:\n        return False\n    def __hash__(self) -> int:\n        return self.x");

        Add(dict, DiagnosticCodes.Validation.HashWithoutEqObject,
            "__hash__ without __eq__(object)",
            "Validation",
            "A class defines '__hash__' but not '__eq__(self, other: object)'. " +
            "The .NET equality contract requires that if GetHashCode is overridden, Equals must also be overridden. " +
            "Without both, the type will behave incorrectly in hash-based collections (set, dict).",
            "class Foo:\n    x: int\n    def __hash__(self) -> int:\n        return self.x",
            "Add an '__eq__(self, other: object) -> bool' method:\nclass Foo:\n    x: int\n    def __eq__(self, other: object) -> bool:\n        return False\n    def __hash__(self) -> int:\n        return self.x");

#pragma warning disable CS0618
        Add(dict, DiagnosticCodes.Validation.UnsupportedDunderReversed,
            "__reversed__ now fully supported via generators",
            "Validation",
            "This diagnostic is no longer emitted. The '__reversed__' dunder method is fully supported " +
            "using generator functions with 'yield'. Define '__reversed__' as a generator that yields " +
            "elements in reverse order, and the compiler will generate a 'GetReverseEnumerator()' method " +
            "returning 'IEnumerator<T>' to satisfy 'IReverseEnumerable<T>'.",
            "class Countdown:\n    start: int\n    def __init__(self, start: int):\n        self.start = start\n    def __reversed__(self) -> int:\n        i = self.start\n        while i > 0:\n            yield i\n            i = i - 1",
            "Use 'yield' in '__reversed__' to produce elements in reverse order.");
#pragma warning restore CS0618

        Add(dict, DiagnosticCodes.Validation.VirtualOnObjectOverride,
            "@virtual is redundant on Object override method",
            "Validation",
            "The @virtual decorator is redundant on __str__, __hash__, and __eq__(self, other: object) because " +
            "these always generate 'override' for the corresponding Object methods (ToString, GetHashCode, Equals). " +
            "The @virtual decorator will be ignored.",
            "class Foo:\n    @virtual\n    def __str__(self) -> str:\n        return \"foo\"",
            "Remove the @virtual decorator — the method is already an override:\nclass Foo:\n    def __str__(self) -> str:\n        return \"foo\"");

        Add(dict, DiagnosticCodes.Validation.StaticFieldViaInstance,
            "Static field accessed via instance",
            "Validation",
            "A static field (marked with @static) is being accessed via 'self' instead of the class name. " +
            "While this works, it is misleading because static fields are shared across all instances. " +
            "Prefer accessing them via the class name for clarity.",
            "class Counter:\n    @static\n    count: int = 0\n    def get(self) -> int:\n        return self.count  # warning: static field via instance",
            "Access the field via the class name:\nclass Counter:\n    @static\n    count: int = 0\n    def get(self) -> int:\n        return Counter.count");

        // ── Validation errors: Question mark operator (SPY0460-SPY0462) ──

        Add(dict, DiagnosticCodes.Validation.QuestionMarkNotResultOrOptional,
            "'?' operator requires Result or Optional type",
            "Validation",
            "The postfix '?' (early-return) operator can only be applied to expressions of type Result[T, E] or Optional[T]. " +
            "It unwraps the success value and returns early with the error/None if the expression is an error or absent.",
            "def process(x: int) -> int !str:\n    val: int = x?  # error: int is not Result or Optional",
            "Apply '?' only to Result or Optional values:\ndef process() -> int !str:\n    val: int = get_value()?  # OK if get_value() returns Result");

        Add(dict, DiagnosticCodes.Validation.QuestionMarkIncompatibleReturn,
            "'?' operator incompatible with function return type",
            "Validation",
            "The postfix '?' operator requires the enclosing function's return type to be compatible. " +
            "If '?' is used on a Result[T, E], the function must return Result[U, E] (or a supertype of E). " +
            "If '?' is used on an Optional[T], the function must return Optional[U].",
            "def process() -> int?:\n    val: int = get_result()?  # error: can't use ? on Result in Optional-returning function",
            "Match the function return type:\ndef process() -> int !str:\n    val: int = get_result()?  # OK: both are Result types");

        Add(dict, DiagnosticCodes.Validation.QuestionMarkOutsideFunction,
            "'?' operator used outside a function",
            "Validation",
            "The postfix '?' (early-return) operator can only be used inside a function body. " +
            "It causes an early return, which requires an enclosing function to return from.",
            "x: int = get_value()?  # error: not inside a function",
            "Use '?' inside a function:\ndef process() -> int !str:\n    val: int = get_value()?");

        // ── Validation warnings: Exhaustiveness (SPY0463) ─────────────

        Add(dict, DiagnosticCodes.Validation.NonExhaustiveMatch, "Non-exhaustive match statement", "Validation",
            "A match statement does not cover all possible values of the scrutinee type. " +
            "For enums, all members should be covered. For bools, both True and False should be covered. For tagged unions, all cases should be covered. " +
            "A wildcard pattern (_) or binding pattern covers all remaining cases. Guard clauses do not count toward exhaustiveness.",
            "union Shape:\n    case Circle(r: float)\n    case Square(s: float)\nmatch shape:\n    case Circle(r):  # missing Square case\n        print(r)",
            "Cover all cases or add a wildcard:\nmatch shape:\n    case Circle(r):\n        print(r)\n    case _:\n        pass");

        // ── Validation errors: Dunder invocation rules (SPY0427-SPY0429)

        Add(dict, DiagnosticCodes.Validation.DunderDirectInvocation,
            "Direct dunder method invocation",
            "Validation",
            "Dunder methods (double-underscore methods like __eq__, __str__, __len__) cannot be called directly from user code. " +
            "They define how a type behaves with operators and built-in functions, but users should invoke that behavior " +
            "through operators (==, +, <) or built-in functions (str(), len()), not by calling dunders directly. " +
            "Dunder-to-dunder calls on self or super() are allowed only inside another dunder method body.",
            "class Foo:\n    value: int\n    def compare(self, other: Foo) -> bool:\n        return self.__eq__(other)  # error: direct dunder call",
            "Use the corresponding operator or built-in function:\nclass Foo:\n    value: int\n    def compare(self, other: Foo) -> bool:\n        return self == other  # use == operator");

        Add(dict, DiagnosticCodes.Validation.DunderWrongReceiver,
            "Dunder call on wrong receiver",
            "Validation",
            "Inside a dunder method, dunder calls are only allowed on 'self' (for cross-dunder synthesis) or 'super()' " +
            "(for calling the base class implementation). Calling a dunder on any other object is not allowed — " +
            "use the corresponding operator or built-in function instead.",
            "class Foo:\n    def __eq__(self, other: object) -> bool:\n        return other.__eq__(self)  # error: receiver is not self or super()",
            "Use the corresponding operator:\nclass Foo:\n    def __eq__(self, other: object) -> bool:\n        return other == self  # use == operator");

        Add(dict, DiagnosticCodes.Validation.DunderCapture,
            "Captured dunder method reference",
            "Validation",
            "Dunder method references cannot be captured (assigned to variables, passed as arguments, etc.). " +
            "Dunder methods must be called immediately as part of a function call expression. " +
            "This restriction ensures that dunder dispatch is always static and verifiable at compile time.",
            "class Foo:\n    def __str__(self) -> str:\n        f = self.__eq__  # error: captured dunder reference\n        return \"Foo\"",
            "Call the dunder method immediately instead of capturing it:\nclass Foo:\n    def __str__(self) -> str:\n        result: bool = self.__eq__(other)  # OK: immediate call\n        return \"Foo\"");

        // ── Validation errors: Access modifier decorators (SPY0430-SPY0431)

        Add(dict, DiagnosticCodes.Validation.ConflictingAccessModifiers,
            "Conflicting access modifier decorators",
            "Validation",
            "A definition has multiple conflicting access modifier decorators (e.g., @private and @protected). " +
            "Only one access modifier decorator is allowed per definition.",
            "class Foo:\n    @private\n    @protected\n    def method(self) -> None:\n        ...",
            "Use only one access modifier decorator:\nclass Foo:\n    @private\n    def method(self) -> None:\n        ...");

        Add(dict, DiagnosticCodes.Validation.AccessModifierOnDunder,
            "Access modifier on dunder method",
            "Validation",
            "An access modifier decorator (@private, @protected, @internal) was applied to a dunder method. " +
            "Dunder methods are protocol methods with well-defined semantics and should not have their access level changed.",
            "class Foo:\n    @private\n    def __init__(self) -> None:\n        ...",
            "Remove the access modifier decorator from the dunder method:\nclass Foo:\n    def __init__(self) -> None:\n        ...");

        // ── Validation errors: Unsupported Python constructs (SPY0432) ──

        Add(dict, DiagnosticCodes.Validation.NamedtupleNotSupported,
            "collections.namedtuple is not supported",
            "Validation",
            "Sharpy does not support collections.namedtuple. Use native named tuples " +
            "(type aliases with named fields) or @dataclass for data-holding classes instead.",
            "from collections import namedtuple\nPoint = namedtuple(\"Point\", [\"x\", \"y\"])",
            "Use native named tuples:\ntype Point = tuple[x: float, y: float]\n\n" +
            "Or use @dataclass:\n@dataclass\nclass Point:\n    x: float\n    y: float");

        // ── Validation errors: Late-bound defaults (SPY0433-SPY0434) ────

        Add(dict, DiagnosticCodes.Validation.LateBoundSelfReference,
            "Late-bound default references its own parameter",
            "Validation",
            "A late-bound default expression (=>) references the parameter it is the default for. " +
            "This would be a circular evaluation and is not allowed.",
            "def f(x: int => x) -> int: ...",
            "Remove the self-reference or use a different expression:\n" +
            "def f(x: int => 0) -> int: ...");

        Add(dict, DiagnosticCodes.Validation.LateBoundForwardReference,
            "Late-bound default references a later parameter",
            "Validation",
            "A late-bound default expression (=>) references a parameter that is declared after it. " +
            "Late-bound defaults may only reference parameters that appear before them in the parameter list.",
            "def f(x: int => y, y: int = 0) -> int: ...",
            "Reorder parameters so that referenced parameters come first:\n" +
            "def f(y: int = 0, x: int => y) -> int: ...");

        // ── Validation errors: Struct field ordering (SPY0435) ─────────

        Add(dict, DiagnosticCodes.Validation.StructFieldOrdering,
            "Non-default struct field follows a field with a default value",
            "Validation",
            "In a struct definition, fields without default values must be declared before fields " +
            "with default values. This ensures the auto-generated constructor has required parameters " +
            "before optional ones, which is required by C#.",
            "struct Bad:\n    x: int = 0\n    y: int  # error: no default after default",
            "Move fields without defaults before fields with defaults:\n" +
            "struct Good:\n    y: int\n    x: int = 0");

        // ── Validation errors: Conversion operators (SPY0436-SPY0439) ────

        Add(dict, DiagnosticCodes.Validation.ConversionOperatorNotStatic,
            "Conversion operator must be @static",
            "Validation",
            "Conversion operators (__implicit__ and __explicit__) must be declared as @static methods with no 'self' parameter.",
            "def __implicit__(self, val: int) -> MyType: ...",
            "Add @static and remove self:\n@static\ndef __implicit__(val: int) -> MyType: ...");

        Add(dict, DiagnosticCodes.Validation.ConversionOperatorParamCount,
            "Conversion operator must have exactly 1 parameter",
            "Validation",
            "Conversion operators must have exactly one parameter (the source value to convert).",
            "@static\ndef __implicit__(a: int, b: int) -> MyType: ...",
            "Use exactly one parameter:\n@static\ndef __implicit__(val: int) -> MyType: ...");

        Add(dict, DiagnosticCodes.Validation.ConversionOperatorNoEnclosingType,
            "At least one type must be the enclosing type",
            "Validation",
            "In a conversion operator, either the parameter type or the return type must be the enclosing class/struct type.",
            "class Foo:\n    @static\n    def __implicit__(val: int) -> str: ...",
            "Ensure one of the types is the enclosing type:\nclass Foo:\n    @static\n    def __implicit__(val: int) -> Foo: ...");

        Add(dict, DiagnosticCodes.Validation.ConversionOperatorDuplicate,
            "Cannot define both implicit and explicit for the same type pair",
            "Validation",
            "A class cannot define both __implicit__ and __explicit__ conversion operators for the same source and target type pair.",
            "@static\ndef __implicit__(val: int) -> Foo: ...\n@static\ndef __explicit__(val: int) -> Foo: ...",
            "Choose either __implicit__ or __explicit__ for each type pair.");

        // ── Validation errors: @final field restrictions (SPY0440-SPY0441) ──

        Add(dict, DiagnosticCodes.Validation.FinalFieldAssignmentOutsideConstructor,
            "Cannot assign to @final field outside of constructor",
            "Validation",
            "A field declared with @final can only be assigned inside the declaring type's __init__ constructor. " +
            "Subsequent assignments in other methods (or in a derived class's __init__) are forbidden. " +
            "@final fields emit as C# readonly.",
            "class Point:\n    @final\n    x: int\n\n    def __init__(self, x: int):\n        self.x = x\n\n    def reset(self):\n        self.x = 0  # SPY0440 error",
            "Either remove @final from the field, or move the assignment into __init__ of the declaring class.");

        Add(dict, DiagnosticCodes.Validation.FinalOnLocalVariable,
            "@final is not valid on a local variable",
            "Validation",
            "The @final decorator may only be applied to class or struct fields. " +
            "Local variables and parameters cannot be marked @final.",
            "def foo():\n    @final\n    x: int = 1  # SPY0441 error",
            "Remove the @final decorator. Use a plain local variable, or move the value to a @final field on a type.");

        // ── Validation errors: lru_cache / cache decorators (SPY0442-SPY0443) ──

        Add(dict, DiagnosticCodes.Validation.LruCacheInvalidMaxSize,
            "Invalid maxsize argument for @lru_cache",
            "Validation",
            "The @lru_cache decorator's 'maxsize' argument must be an integer literal or None. " +
            "Non-literal expressions, negative values, and non-integer types are not supported.",
            "@lru_cache(maxsize=\"big\")\ndef f(x: int) -> int: ...",
            "Use an integer literal or None:\n@lru_cache(maxsize=128)\ndef f(x: int) -> int: ...");

        Add(dict, DiagnosticCodes.Validation.LruCacheOnNonFunction,
            "@lru_cache can only be applied to functions and methods",
            "Validation",
            "The @lru_cache and @cache decorators are only valid on function and method definitions. " +
            "They cannot be applied to classes, fields, or other declarations.",
            "@lru_cache\nclass Foo: ...",
            "Apply @lru_cache to a function instead:\n@lru_cache\ndef compute(x: int) -> int: ...");

        // ── Validation errors: Unknown decorator (SPY0444) ──

        Add(dict, DiagnosticCodes.Validation.UnknownDecorator,
            "Unknown decorator — use @[...] for C# attributes",
            "Validation",
            "The decorator name is not a recognized Sharpy keyword. To apply a .NET attribute, " +
            "use the @[AttributeName] bracket syntax instead of @attribute_name.",
            "@serializable\nclass Config: ...",
            "Use bracket attribute syntax:\n@[Serializable]\nclass Config: ...");

        // ── Source generator validation (SPY0445-SPY0447) ──────────────

        Add(dict, DiagnosticCodes.Validation.InvalidGeneratorSignature,
            "Invalid 'generate' method signature on source generator",
            "Validation",
            "A class that extends SourceGenerator must declare exactly one method named 'generate' " +
            "with the signature '(self, context: GeneratorContext) -> GeneratorOutput'. The generator " +
            "engine invokes this method at compile time and expects the exact shape.",
            "class MyGen(SourceGenerator):\n    def generate(self) -> GeneratorOutput: ...   # missing context param",
            "Declare 'generate' with the full signature:\n" +
            "class MyGen(SourceGenerator):\n    def generate(self, context: GeneratorContext) -> GeneratorOutput:\n        return GeneratorOutput(\"\")");

        Add(dict, DiagnosticCodes.Validation.AbstractGenerator,
            "Source generator class cannot be abstract",
            "Validation",
            "Source generator classes are instantiated by the compiler at compile time, so they must be " +
            "concrete. An @abstract class cannot be invoked as a generator.",
            "@abstract\nclass MyGen(SourceGenerator):\n    def generate(self, context: GeneratorContext) -> GeneratorOutput: ...",
            "Remove the @abstract decorator, or move the abstract logic to a regular base class and have " +
            "the generator extend it concretely.");

        Add(dict, DiagnosticCodes.Validation.GeneratorOnInvalidTarget,
            "Source generator decorator applied to an invalid target",
            "Validation",
            "Bracket attributes that resolve to a source generator may only be applied to class, function, " +
            "or struct declarations. Other declarations (variables, properties, events, etc.) cannot be " +
            "generator targets.",
            "@[GenerateEquals]\nx: int = 0   # SPY0447 — generator on a variable declaration",
            "Apply the generator attribute to a class, function, or struct declaration instead.");

        // ── @test decorator validation (SPY0448-SPY0449) ──────────────

        Add(dict, DiagnosticCodes.Validation.TestDecoratorInvalidTarget,
            "@test decorator applied to invalid target",
            "Validation",
            "The @test decorator is only valid on function and method declarations. " +
            "It cannot be applied to classes, structs, interfaces, enums, properties, events, or dunder methods.",
            "@test\nclass MyClass:\n    pass  # SPY0448 — @test on a class",
            "Move @test to a function or method declaration.");

        Add(dict, DiagnosticCodes.Validation.TestDecoratorInvalidCombination,
            "@test combined with incompatible decorator",
            "Validation",
            "The @test decorator cannot be combined with @abstract, @virtual, or @static. " +
            "Test methods must be concrete instance methods so xUnit can discover and run them.",
            "@test\n@static\ndef test_something():\n    pass  # SPY0449",
            "Remove the incompatible decorator. Test functions should be regular (non-static, non-abstract) methods.");

        // ── @test decorator argument validation (SPY0469) ──────────────

        Add(dict, DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
            "@test decorator has invalid argument",
            "Validation",
            "The @test decorator accepts at most one positional string argument (the test description). " +
            "Non-string arguments and keyword arguments are not supported.",
            "@test(42)\ndef test_something():\n    pass  # SPY0469 — argument must be a string",
            "Use a string literal: @test(\"my test description\") or omit the argument: @test");

        // ── Validation warnings: Deprecation (SPY0464) ─────────────────

        Add(dict, DiagnosticCodes.Validation.DeprecatedBodylessSyntax,
            "Deprecated body-less abstract method syntax",
            "Validation",
            "An abstract method uses the deprecated body-less syntax (no body at all). " +
            "The preferred syntax uses '...' (ellipsis) as the method body to indicate an abstract method.",
            "@abstract\nclass Shape:\n    def area(self) -> float  # deprecated: no body",
            "Use ellipsis as the body:\n@abstract\nclass Shape:\n    def area(self) -> float: ...");

        // ── Validation warnings: Identity operator (SPY0465) ──────────────

        Add(dict, DiagnosticCodes.Validation.IsWithValueTypes,
            "Identity operator used with value types",
            "Validation",
            "The 'is' operator (identity/reference equality) is used with a value type. " +
            "Value types are compared by value, not by reference, so 'is' may not behave as expected. " +
            "Use '==' for value comparison instead.",
            "x: int = 42\nif x is 42: ...",
            "Use '==' instead of 'is' for value types:\nif x == 42: ...");
        // ── Validation warnings: Deprecated usage (SPY0466) ──────────────

        Add(dict, DiagnosticCodes.Validation.DeprecatedUsage,
            "Usage of deprecated symbol",
            "Validation",
            "A function, method, class, or property marked with @deprecated is being used. " +
            "The deprecation message explains why and what to use instead.",
            "@deprecated(\"Use bar() instead\")\ndef foo(): ...\n\nfoo()  # SPY0466 warning",
            "Follow the deprecation message and migrate to the suggested alternative.");

        // ── Validation warnings: Readonly property assignment (SPY0467) ────

        Add(dict, DiagnosticCodes.Validation.ReadonlyPropertyAssignment,
            "Assignment to readonly property",
            "Validation",
            "A property marked with @readonly cannot be assigned to after construction. " +
            "@readonly properties can only be set in __init__.",
            "@readonly\nproperty name: str\n\ndef change(self):\n    self.name = \"new\"  # SPY0467 error",
            "Remove the assignment or change the property to not be @readonly.");

        // ── Validation warnings: Constant pattern shadow (SPY0468) ─────

        Add(dict, DiagnosticCodes.Validation.ConstantPatternShadow,
            "Pattern capture shadows constant",
            "Validation",
            "A capture variable in a match pattern has the same name as a module-level constant. " +
            "The identifier is treated as a constant pattern (matching its value), not a capture binding. " +
            "Use a different name if you intended to capture the matched value.",
            "MAX: Final[int] = 100\nmatch x:\n    case MAX:  # matches value 100, does NOT capture into MAX",
            "Rename the capture variable to avoid ambiguity with the constant.");

        // ── Validation transition hints (SPY0470-SPY0489) ──────────────
        // Hint-severity advisories about behavioral differences from Python/C#.
        // Suppressible like warnings, but never promoted to errors under -Werror.

        Add(dict, DiagnosticCodes.Validation.Utf16StringLengthHint,
            "len(str) returns UTF-16 code units, not Unicode code points",
            "Validation",
            "In Sharpy, len(s) on a string returns the number of UTF-16 code units (matching .NET's String.Length), " +
            "not the number of Unicode code points (as Python does). Strings outside the BMP (e.g., emoji, " +
            "supplementary plane characters) are encoded as surrogate pairs and count as 2 code units. This is a " +
            "deliberate Axiom 1 (.NET first) decision; helper methods in the str module provide code-point counts.",
            "len(\"\\U0001F600\")  # Sharpy: 2 (UTF-16 surrogate pair); Python: 1 (1 code point)",
            "If you need Unicode code-point counts, use the explicit helper (e.g., str.code_point_count(s)).");

        Add(dict, DiagnosticCodes.Validation.StructValueSemanticsHint,
            "Struct assignment copies the value (value semantics)",
            "Validation",
            "Sharpy structs follow .NET value semantics: assigning a struct or passing it to a function copies the " +
            "entire value, so mutations on the copy do not affect the original. This differs from Python (where " +
            "everything is a reference) and from Sharpy classes. Mark a parameter as `ref` (or use a class) if you " +
            "want shared mutation.",
            "@struct\nclass Point:\n    x: int = 0\n\np = Point()\nq = p          # copy — q.x = 5 won't change p.x\nq.x = 5",
            "Use `ref` parameters for in-place mutation, or model the type as a class if reference semantics are required.");

        Add(dict, DiagnosticCodes.Validation.HomogeneousVariadicHint,
            "Variadic parameters in Sharpy are homogeneous and statically typed",
            "Validation",
            "Sharpy's `*args` declares a typed, homogeneous list (`list[T]`), not Python's heterogeneous tuple of " +
            "`Any`. Every argument forwarded through `*args` must satisfy the declared element type. This enforces " +
            "Axiom 3 (type safety) — there is no `Any` escape hatch.",
            "def log(*args: int) -> None: ...\nlog(1, 2, \"three\")  # SPY0220 — \"three\" violates int element type",
            "Annotate the variadic with the broadest concrete type your callers need (e.g., `*args: object`), or define overloads.");

        Add(dict, DiagnosticCodes.Validation.NoClassmethodHint,
            "@classmethod is not supported — use @staticmethod or a factory",
            "Validation",
            "Sharpy intentionally omits Python's @classmethod decorator. .NET's type system does not pass the class " +
            "object as a first parameter, and the feature would require a runtime indirection that conflicts with " +
            "Axiom 1. Use @staticmethod for type-independent helpers, or define a regular factory method.",
            "@classmethod\ndef from_string(cls, s: str) -> Self: ...   # not supported",
            "Use @staticmethod and reference the type by name, or use a factory function on the module.");

        // SPY0474 (NoAsyncComprehensionHint) retired in #998 — async comprehensions are now
        // supported, so the transition hint and its explanation are gone. The code constant is
        // kept reserved (see DiagnosticCodes.cs) so the number is never reused.

        Add(dict, DiagnosticCodes.Validation.SingleIsinstanceTypeHint,
            "isinstance() takes exactly one type argument",
            "Validation",
            "Unlike Python, Sharpy's isinstance() accepts only a single type, not a tuple of types. This keeps the " +
            "result type narrowing precise (the value is narrowed to that one type) and avoids tuple-shaped " +
            "argument quirks. Compose multiple checks with `or`.",
            "if isinstance(x, (int, str)):  # not supported\n    ...",
            "Combine type checks with `or`: `if isinstance(x, int) or isinstance(x, str): ...`");

        Add(dict, DiagnosticCodes.Validation.NegativeTupleIndexHint,
            "Negative tuple indices are rejected at compile time",
            "Validation",
            "Tuples in Sharpy have a fixed, statically known length, so negative indexing (Python's t[-1] for the " +
            "last element) is rejected at compile time as SPY0259 — you should use the positive index of the " +
            "corresponding element. This is a transition hint that explains the diagnostic for Python users.",
            "t = (1, 2, 3)\nlast = t[-1]  # SPY0259 — use t[2] instead",
            "Use the explicit positive index, or convert to a list if dynamic indexing is needed.");

        Add(dict, DiagnosticCodes.Validation.UnnecessaryStaticDecoratorHint,
            "@static / @staticmethod is unnecessary on a method without 'self'",
            "Validation",
            "Sharpy infers static methods automatically: a method declared inside a class, struct, or interface " +
            "is treated as static when its first parameter is not the implicit 'self' (an untyped first " +
            "parameter named 'self'). The '@static' decorator is therefore optional, and the Python " +
            "'@staticmethod' decorator is rejected outright (DecoratorValidator). This hint flags the redundant " +
            "case so users transitioning from Python or C# learn the convention and can simplify their code.",
            "class Math:\n    @static                     # redundant — already static\n    def square(x: int) -> int:\n        return x * x",
            "Drop the '@static' / '@staticmethod' decorator. The method remains static because it has no 'self' parameter.");

    }
}
