namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// DiagnosticExplanations partial: Semantic diagnostic entries (SPY0200-SPY0399)
/// </summary>
public static partial class DiagnosticExplanations
{
    private static void AddSemanticEntries(Dictionary<string, DiagnosticExplanation> dict)
    {
        // ── Semantic errors: Name resolution (SPY0200-SPY0219) ──────────

        Add(dict, DiagnosticCodes.Semantic.UndefinedVariable, "Undefined variable", "Semantic",
            "A variable was referenced that has not been declared in the current scope or any enclosing scope. Variables must be declared with a type annotation before use.",
            "def main():\n    print(x)  # x is not defined",
            "Declare the variable before using it:\ndef main():\n    x: int = 42\n    print(x)");

        Add(dict, DiagnosticCodes.Semantic.UndefinedFunction, "Undefined function", "Semantic",
            "A function was called that has not been defined or imported. Check for typos in the function name and ensure the module containing the function has been imported.",
            "def main():\n    result = calculate(1, 2)  # calculate not defined",
            "Define the function or import it:\ndef calculate(a: int, b: int) -> int:\n    return a + b");

        Add(dict, DiagnosticCodes.Semantic.UndefinedType, "Undefined type", "Semantic",
            "A type was referenced in an annotation or expression that has not been defined or imported. This includes class names, struct names, enum names, and type aliases.",
            "x: Widget = Widget()  # Widget not defined",
            "Define the type or import it:\nclass Widget:\n    name: str");

        Add(dict, DiagnosticCodes.Semantic.UndefinedMember, "Undefined member", "Semantic",
            "An attribute or method was accessed on a type that does not define it. This can occur with field access, method calls, or property access.",
            "class Point:\n    x: int\n    y: int\n\np = Point(1, 2)\nprint(p.z)  # Point has no member 'z'",
            "Check the type definition for available members. Fix the member name or add the missing member to the type.");

        Add(dict, DiagnosticCodes.Semantic.DuplicateDefinition, "Duplicate definition", "Semantic",
            "A name was defined more than once in the same scope. Each name can only be defined once per scope (function, class, or module level).",
            "x: int = 1\nx: str = \"hello\"  # duplicate",
            "Use a different name or remove the duplicate definition.");

        Add(dict, DiagnosticCodes.Semantic.DuplicateParameter, "Duplicate parameter", "Semantic",
            "A function or method has two parameters with the same name.",
            "def add(x: int, x: int) -> int:\n    return x + x",
            "Give each parameter a unique name:\ndef add(x: int, y: int) -> int:\n    return x + y");

        Add(dict, DiagnosticCodes.Semantic.InvalidTypeAlias, "Invalid type alias", "Semantic",
            "A type alias definition is invalid. The target type may not be a valid type expression.",
            "type Bad = 42",
            "Use a valid type expression:\n  type IntList = list[int]\n  type Callback = Callable[[int], str]");

        Add(dict, DiagnosticCodes.Semantic.QuestionMarkInFinally, "'?' operator in finally block", "Semantic",
            "The '?' operator (early return on Result/Optional) cannot be used inside a 'finally' block because finally blocks must always complete normally — they cannot return early.",
            "def process() -> int !str:\n    try:\n        pass\n    finally:\n        val: int = get_value()?  # error",
            "Move the '?' expression outside the finally block:\ndef process() -> int !str:\n    val: int = get_value()?\n    try:\n        pass\n    finally:\n        cleanup()");

        // ── Semantic errors: Type checking (SPY0220-SPY0259) ────────────

        Add(dict, DiagnosticCodes.Semantic.TypeMismatch, "Type mismatch", "Semantic",
            "The actual type of an expression does not match the expected type. This can occur in assignments, function arguments, return statements, and other contexts where a specific type is required.",
            "x: int = \"hello\"  # str assigned to int",
            "Ensure the types match. Either change the value, add a type conversion, or change the type annotation.");

        Add(dict, DiagnosticCodes.Semantic.IncompatibleTypes, "Incompatible types", "Semantic",
            "Two types are incompatible in the given context. This is similar to a type mismatch but may involve more complex type relationships such as generic constraints or inheritance hierarchies.",
            "items: list[int] = [1, 2, 3]\nitems = \"hello\"  # list[int] and str are incompatible",
            "Ensure the types are compatible. Use explicit conversions if needed.");

        Add(dict, DiagnosticCodes.Semantic.InvalidBinaryOperation, "Invalid binary operation", "Semantic",
            "A binary operator was used with operand types that do not support it. For example, using + between an int and a bool, or - with strings.",
            "x: str = \"hello\" - \"world\"  # str doesn't support -",
            "Use an operator that is supported for the given types, or convert the operands to compatible types.");

        Add(dict, DiagnosticCodes.Semantic.InvalidUnaryOperation, "Invalid unary operation", "Semantic",
            "A unary operator was used with an operand type that does not support it. For example, using the negation operator (-) on a string.",
            "x: str = -\"hello\"",
            "Use an operator that is supported for the given type.");

        Add(dict, DiagnosticCodes.Semantic.WrongArgumentCount, "Wrong argument count", "Semantic",
            "A function or method was called with the wrong number of arguments. The error message indicates how many arguments were expected and how many were provided.",
            "def greet(name: str) -> str:\n    return f\"Hello, {name}\"\n\ngreet()  # expected 1 argument",
            "Provide the correct number of arguments:\ngreet(\"Alice\")");

        Add(dict, DiagnosticCodes.Semantic.InvalidAssignmentTarget, "Invalid assignment target", "Semantic",
            "The left-hand side of an assignment is not a valid target. Assignments can only target variables, fields, and subscript expressions.",
            "1 + 2 = 3  # cannot assign to expression",
            "Assign to a variable or field:\n  result: int = 1 + 2");

        Add(dict, DiagnosticCodes.Semantic.MissingTypeAnnotation, "Missing type annotation", "Semantic",
            "A variable declaration is missing a type annotation. Sharpy requires explicit type annotations for all variable declarations to ensure static type safety.",
            "x = 42  # missing type annotation",
            "Add a type annotation:\nx: int = 42");

        Add(dict, DiagnosticCodes.Semantic.CannotInferType, "Cannot infer type", "Semantic",
            "The compiler cannot determine the type of an expression. This may occur with complex expressions or when type information is insufficient.",
            "x: auto = some_complex_expression()",
            "Add an explicit type annotation to help the compiler:\n  x: int = some_complex_expression()");

        Add(dict, DiagnosticCodes.Semantic.InvalidCast, "Invalid cast", "Semantic",
            "A type cast is invalid because the source and target types are not compatible. Only related types can be cast to each other.",
            "x: int = int(\"not_a_number\")  # runtime error potential",
            "Ensure the cast is between compatible types or use a conversion function.");

        Add(dict, DiagnosticCodes.Semantic.NullabilityViolation, "Nullability violation", "Semantic",
            "A potentially null value is being used in a context that requires a non-null value. Use Optional[T] for values that can be None, and handle the None case before using the value.",
            "def get_name() -> Optional[str]:\n    return None\n\nname: str = get_name()  # might be None",
            "Handle the null case:\nresult = get_name()\nif result is not None:\n    name: str = result");

        Add(dict, DiagnosticCodes.Semantic.NotCallable, "Type is not callable", "Semantic",
            "An expression was called like a function but its type does not support being called. Only functions, methods, and types with __call__ can be called.",
            "x: int = 42\ny: int = x()  # int is not callable",
            "Ensure you are calling a function or method, not a value.");

        Add(dict, DiagnosticCodes.Semantic.InvalidPipeTarget, "Invalid pipe target", "Semantic",
            "The right-hand side of a pipe operator (|>) is not a valid pipe target. The target must be a callable that accepts the piped value as its first argument.",
            "42 |> \"hello\"  # str is not a valid pipe target",
            "Pipe into a function:\n  42 |> str\n  items |> len");

        Add(dict, DiagnosticCodes.Semantic.InvalidSelfUsage, "Invalid 'self' usage", "Semantic",
            "'self' was used outside of an instance method or in an invalid context. 'self' is only available inside instance methods of a class.",
            "def free_function():\n    print(self.x)  # no self outside a class",
            "Move the code into a class method or pass the object as a parameter.");

        Add(dict, DiagnosticCodes.Semantic.InvalidNothingUsage, "Invalid 'Nothing' usage", "Semantic",
            "'Nothing' was used in an invalid context. Nothing is the bottom type and can only be used in specific type-level contexts.",
            "x: Nothing = 42",
            "Nothing is typically used as a return type for functions that never return:\ndef fail(msg: str) -> Nothing:\n    raise Error(msg)");

        Add(dict, DiagnosticCodes.Semantic.UnknownKeywordArgument, "Unknown keyword argument", "Semantic",
            "A keyword argument was passed to a function using a name that does not match any parameter. Check the function signature for the correct parameter names.",
            "def greet(name: str):\n    print(name)\n\ngreet(nme=\"Alice\")  # typo in 'name'",
            "Use the correct parameter name:\n  greet(name=\"Alice\")");

        Add(dict, DiagnosticCodes.Semantic.DuplicateArgument, "Duplicate argument", "Semantic",
            "The same argument was provided more than once in a function call, either as a duplicate keyword argument or as both a positional and keyword argument.",
            "greet(\"Alice\", name=\"Bob\")  # name supplied twice",
            "Remove the duplicate argument:\n  greet(name=\"Alice\")");

        Add(dict, DiagnosticCodes.Semantic.InvalidNullConditional, "Invalid null-conditional access", "Semantic",
            "The null-conditional operator (?.) was used on a type that is not nullable. This operator is only meaningful on Optional[T] types.",
            "x: int = 42\ny = x?.to_string()  # int is never null",
            "Use regular member access for non-nullable types:\n  y = x.to_string()");

        Add(dict, DiagnosticCodes.Semantic.CannotInferGenericType, "Cannot infer generic type argument", "Semantic",
            "The compiler cannot infer the type arguments for a generic type or function. Provide explicit type arguments.",
            "items = []  # can't infer element type",
            "Specify the type:\n  items: list[int] = []");

        Add(dict, DiagnosticCodes.Semantic.InvalidComprehension, "Invalid comprehension", "Semantic",
            "A list, set, or dict comprehension contains invalid syntax or type mismatches in its clauses.",
            "result: list[int] = [x for x in \"hello\"]  # str elements, not int",
            "Ensure the comprehension expression type matches the target type.");

        Add(dict, DiagnosticCodes.Semantic.InvalidTupleUnpacking, "Invalid tuple unpacking", "Semantic",
            "A tuple unpacking assignment has a mismatch between the number of variables and the number of tuple elements.",
            "a, b = (1, 2, 3)  # 2 targets, 3 values",
            "Match the number of variables to the tuple size:\n  a, b, c = (1, 2, 3)");

        Add(dict, DiagnosticCodes.Semantic.InvalidAutoVariable, "Invalid auto variable", "Semantic",
            "A variable declared with 'auto' type cannot have its type inferred from the initializer. The initializer expression's type is ambiguous or unresolvable.",
            null,
            "Add an explicit type annotation instead of using 'auto'.");

        Add(dict, DiagnosticCodes.Semantic.ConditionNotBoolean, "Condition is not boolean", "Semantic",
            "An expression used as a condition in an if, while, or similar statement does not evaluate to a boolean type. Conditions must be explicitly boolean in Sharpy.",
            "x: int = 42\nif x:  # int is not bool\n    print(\"truthy\")",
            "Use an explicit comparison:\nif x != 0:\n    print(\"non-zero\")");

        Add(dict, DiagnosticCodes.Semantic.InvalidRaise, "Invalid raise statement", "Semantic",
            "A raise statement is used incorrectly. Bare 'raise' can only be used inside an except block, and 'raise X' requires X to be an exception type.",
            "def foo():\n    raise  # bare raise outside except block",
            "Raise a specific exception:\n  raise ValueError(\"something went wrong\")\n\nOr use bare raise inside an except block:\n  try:\n      ...\n  except Exception as e:\n      raise");

        Add(dict, DiagnosticCodes.Semantic.InvalidMaybeExpression, "Invalid Maybe expression", "Semantic",
            "A Maybe expression is used incorrectly. Maybe wraps optional values and must be used with compatible types.",
            null,
            "Ensure the expression is compatible with the Maybe/Optional type pattern.");

        Add(dict, DiagnosticCodes.Semantic.InvalidNoneConstructor, "Invalid None constructor usage", "Semantic",
            "None was used in a context that requires a specific type. None can only be used where an Optional[T] type is expected.",
            "x: int = None  # int is not nullable",
            "Use Optional[T] for nullable types:\n  x: Optional[int] = None");

        Add(dict, DiagnosticCodes.Semantic.InvalidSomeConstructor, "Invalid Some constructor usage", "Semantic",
            "The Some() constructor for Optional types was used incorrectly. Some wraps a non-null value into an Optional.",
            "x: Optional[int] = Some(None)  # Some cannot wrap None",
            "Pass a non-null value to Some:\n  x: Optional[int] = Some(42)");

        Add(dict, DiagnosticCodes.Semantic.InvalidOkErrConstructor, "Invalid Ok/Err constructor usage", "Semantic",
            "An Ok() or Err() constructor for Result types was used incorrectly. Ok wraps a success value and Err wraps an error value.",
            null,
            "Ensure Ok/Err is used with compatible Result[T, E] types.");

        Add(dict, DiagnosticCodes.Semantic.MissingMethodBody, "Missing method body", "Semantic",
            "A non-abstract method in a class or struct is missing its body. Only abstract methods and interface methods can omit the body.",
            "class Foo:\n    def bar(self) -> int:",
            "Add a method body:\nclass Foo:\n    def bar(self) -> int:\n        return 42\n\nOr mark the method as abstract:\nabstract class Foo:\n    def bar(self) -> int: ...");

        Add(dict, DiagnosticCodes.Semantic.InvalidOverride, "Invalid override", "Semantic",
            "A method override is invalid. The overriding method must match the signature of the base class method in parameter types and return type.",
            "class Base:\n    def foo(self) -> int:\n        return 1\n\nclass Sub(Base):\n    def foo(self) -> str:  # return type mismatch\n        return \"hello\"",
            "Match the base class method signature:\nclass Sub(Base):\n    def foo(self) -> int:\n        return 2");

        Add(dict, DiagnosticCodes.Semantic.MissingParameterAnnotation, "Missing parameter type annotation", "Semantic",
            "A function parameter is missing its type annotation. All parameters in Sharpy must have explicit type annotations.",
            "def add(a, b) -> int:\n    return a + b",
            "Add type annotations to all parameters:\ndef add(a: int, b: int) -> int:\n    return a + b");

        Add(dict, DiagnosticCodes.Semantic.InvalidDefaultValue, "Invalid default value", "Semantic",
            "A parameter's default value is not compatible with its declared type.",
            "def greet(name: int = \"hello\"):\n    print(name)",
            "Ensure the default value matches the parameter type:\ndef greet(name: str = \"hello\"):\n    print(name)");

        Add(dict, DiagnosticCodes.Semantic.InterfaceMethodBody, "Interface method has body", "Semantic",
            "An interface method was defined with a body. Interface methods must be abstract (no implementation).",
            "interface Printable:\n    def display(self) -> str:\n        return \"hello\"  # not allowed",
            "Remove the body and use ... (ellipsis) for abstract methods:\ninterface Printable:\n    def display(self) -> str: ...");

        Add(dict, DiagnosticCodes.Semantic.UninitializedStructField, "Uninitialized struct field", "Semantic",
            "A struct field does not have a default value and is not initialized in the constructor. Struct fields must be initialized.",
            "struct Point:\n    x: int\n    y: int\n    z: int  # no default, not set in __init__",
            "Provide a default value or ensure all fields are set in the constructor:\nstruct Point:\n    x: int = 0\n    y: int = 0\n    z: int = 0");

        Add(dict, DiagnosticCodes.Semantic.InvalidEnumValue, "Invalid enum value", "Semantic",
            "An enum member has an invalid value. Enum values must be constant expressions of the correct type.",
            "enum Status:\n    ACTIVE = some_function()  # not a constant",
            "Use a constant value:\nenum Status:\n    ACTIVE = 1\n    INACTIVE = 0");

        Add(dict, DiagnosticCodes.Semantic.InvalidFunctionType, "Invalid function type", "Semantic",
            "A function type annotation is invalid. Function types must use the Callable syntax with proper parameter and return types.",
            "f: int -> str = ...  # invalid syntax",
            "Use Callable syntax:\n  f: Callable[[int], str] = my_func");

        Add(dict, DiagnosticCodes.Semantic.UnrecognizedStatementType, "Unrecognized statement type in type checker", "Semantic",
            "The type checker encountered a statement type that it does not have a handler for. " +
            "This is a compiler bug — the type checker is missing a case for this AST node type, " +
            "which means the statement was not type-checked.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        Add(dict, DiagnosticCodes.Semantic.UnrecognizedExpressionType, "Unrecognized expression type in type checker", "Semantic",
            "The type checker encountered an expression type that it does not have a handler for. " +
            "The expression was assigned the Unknown type, which passes all type checks and may mask errors. " +
            "This is a compiler bug — the type checker is missing a case for this AST node type.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        Add(dict, DiagnosticCodes.Semantic.TuplePatternLengthMismatch, "Tuple pattern length mismatch", "Semantic",
            "A tuple pattern in a match statement has a different number of elements than the scrutinee tuple type. The pattern must have exactly the same number of elements as the tuple being matched.",
            "def main():\n    t = (1, 2, 3)\n    match t:\n        case (a, b):  # 2 elements vs 3\n            pass",
            "Ensure the pattern has the same number of elements as the tuple:\nmatch t:\n    case (a, b, c):\n        print(a, b, c)");

        Add(dict, DiagnosticCodes.Semantic.TupleIndexOutOfRange, "Tuple index out of range", "Semantic",
            "A tuple was indexed with a constant integer that is greater than or equal to the number of elements in the tuple. Tuple indices are zero-based, so for a tuple with N elements, valid indices are 0 through N-1.",
            "def main():\n    t = (1, 2, 3)\n    print(t[3])  # only indices 0, 1, 2 are valid",
            "Use a valid index within range:\ndef main():\n    t = (1, 2, 3)\n    print(t[2])  # last element");

        Add(dict, DiagnosticCodes.Semantic.TupleNegativeIndex, "Negative tuple index", "Semantic",
            "A tuple was indexed with a negative integer. Unlike Python lists, Sharpy tuples do not support negative indexing because tuple access is resolved at compile time to specific .ItemN fields.",
            "def main():\n    t = (1, 2, 3)\n    print(t[-1])  # negative index not supported",
            "Use a positive index instead:\ndef main():\n    t = (1, 2, 3)\n    print(t[2])  # last element");

        // ── Semantic errors: Return and control flow (SPY0260-SPY0279) ──

        Add(dict, DiagnosticCodes.Semantic.MissingReturnValue, "Missing return value", "Semantic",
            "A return statement in a function with a non-void return type is missing the return value. Functions that declare a return type must return a value of that type.",
            "def square(x: int) -> int:\n    return  # missing value",
            "Return a value matching the declared type:\ndef square(x: int) -> int:\n    return x * x");

        Add(dict, DiagnosticCodes.Semantic.MissingReturnType, "Missing return type annotation", "Semantic",
            "A function definition is missing its return type annotation. Sharpy requires explicit return types on all functions. Use -> None for functions that don't return a value.",
            "def greet(name: str):\n    print(f\"Hello, {name}\")",
            "Add a return type:\ndef greet(name: str) -> None:\n    print(f\"Hello, {name}\")");

        Add(dict, DiagnosticCodes.Semantic.ReturnOutsideFunction, "Return outside function", "Semantic",
            "A return statement was used outside of a function body. Return can only be used inside functions and methods.",
            "return 42  # at module level",
            "Move the return inside a function:\ndef main():\n    return 42");

        Add(dict, DiagnosticCodes.Semantic.BreakOutsideLoop, "Break outside loop", "Semantic",
            "A break statement was used outside of a for or while loop. Break can only be used inside loop bodies.",
            "def main():\n    break  # not inside a loop",
            "Move the break inside a loop, or use return to exit a function.");

        Add(dict, DiagnosticCodes.Semantic.ContinueOutsideLoop, "Continue outside loop", "Semantic",
            "A continue statement was used outside of a for or while loop. Continue can only be used inside loop bodies.",
            "def main():\n    continue  # not inside a loop",
            "Move the continue inside a loop.");

        Add(dict, DiagnosticCodes.Semantic.YieldOutsideFunction, "Yield outside function", "Semantic",
            "A yield statement was used outside of a function body. Yield can only be used inside function definitions to create generator functions.",
            "yield 42  # not inside a function",
            "Move the yield inside a function definition:\ndef gen():\n    yield 42");

        Add(dict, DiagnosticCodes.Semantic.NotAllPathsReturn, "Not all paths return a value", "Semantic",
            "A function declared with a non-void return type has execution paths that do not return a value. All possible paths through the function must end with a return statement.",
            "def abs_val(x: int) -> int:\n    if x >= 0:\n        return x\n    # missing return for x < 0",
            "Ensure all paths return a value:\ndef abs_val(x: int) -> int:\n    if x >= 0:\n        return x\n    return -x");

        Add(dict, DiagnosticCodes.Semantic.YieldWithReturn, "Yield with return value in generator", "Semantic",
            "A generator function (one that uses yield) cannot also use 'return' with a value. Use yield to produce values and bare 'return' to stop the generator.",
            "def gen() -> int:\n    yield 1\n    return 42  # cannot return a value",
            "Use bare return to stop the generator:\ndef gen() -> int:\n    yield 1\n    return  # stops iteration");

        Add(dict, DiagnosticCodes.Semantic.YieldInNext, "Yield in __next__ method", "Semantic",
            "The __next__ method cannot contain yield statements. Use __iter__ for generator-based iteration, or implement __next__ as an explicit iterator (with return).",
            "class MyIter:\n    def __next__(self) -> int:\n        yield 1  # not allowed",
            "Use __iter__ for generators:\nclass MyIter:\n    def __iter__(self) -> int:\n        yield 1\n        yield 2");

        Add(dict, DiagnosticCodes.Semantic.GeneratorIterConflict, "Generator __iter__ conflicts with __next__", "Semantic",
            "A class cannot have both a generator __iter__ (using yield) and a __next__ method. Choose either the generator pattern (yield in __iter__) or the explicit iterator pattern (__next__ with return).",
            "class Bad:\n    def __iter__(self) -> int:\n        yield 1\n    def __next__(self) -> int:\n        return 0",
            "Choose one pattern:\nclass GenIter:\n    def __iter__(self) -> int:\n        yield 1\n        yield 2");

        Add(dict, DiagnosticCodes.Semantic.YieldInTryExcept,
            "Yield in try/except block", "Semantic",
            "A 'yield' statement cannot appear inside a 'try' block that has 'except' handlers. " +
            "This is a .NET restriction: the CLR does not support yield return inside try-catch. " +
            "Move the yield outside the try/except block or use try/finally instead.",
            "def gen() -> int:\n    try:\n        yield 1  # not allowed\n    except Exception as e:\n        pass",
            "Move yield outside try/except:\ndef gen() -> int:\n    yield 1\n    try:\n        risky_operation()\n    except Exception as e:\n        handle_error(e)");

        Add(dict, DiagnosticCodes.Semantic.YieldInCatchHandler,
            "Yield in except handler", "Semantic",
            "A 'yield' statement cannot appear inside an 'except' handler. " +
            "This is a .NET restriction: the CLR does not support yield return inside catch blocks (CS1631). " +
            "Move the yield outside the except handler.",
            "def gen() -> int:\n    try:\n        pass\n    except Exception as e:\n        yield 1  # not allowed",
            "Move yield outside the except handler:\ndef gen() -> int:\n    try:\n        risky_operation()\n    except Exception as e:\n        handle_error(e)\n    yield 1");

        Add(dict, DiagnosticCodes.Semantic.YieldInFinallyBlock,
            "Yield in finally block", "Semantic",
            "A 'yield' statement cannot appear inside a 'finally' block. " +
            "This is a .NET restriction: the CLR does not support yield return inside finally blocks (CS1625). " +
            "Move the yield outside the finally block.",
            "def gen() -> int:\n    try:\n        pass\n    finally:\n        yield 1  # not allowed",
            "Move yield outside the finally block:\ndef gen() -> int:\n    yield 1\n    try:\n        risky_operation()\n    finally:\n        cleanup()");

        Add(dict, DiagnosticCodes.Semantic.AwaitOutsideAsync,
            "Await outside async function", "Semantic",
            "The 'await' keyword can only be used inside functions declared with 'async def'. " +
            "Regular functions and lambdas cannot use 'await'.",
            "def fetch() -> str:\n    return await get_data()  # not async",
            "Declare the function as async:\nasync def fetch() -> str:\n    return await get_data()");

        Add(dict, DiagnosticCodes.Semantic.InvalidAwaitOperand,
            "Cannot await non-Task type", "Semantic",
            "The 'await' keyword can only be used with expressions that return a Task type. " +
            "The operand must be a call to an async function or an expression that produces a Task.",
            "async def run():\n    x: int = await 42  # int is not awaitable",
            "Await an async function call:\nasync def run():\n    x: int = await get_value()");

        // ── Semantic errors: Class and inheritance (SPY0280-SPY0299) ────

        Add(dict, DiagnosticCodes.Semantic.AbstractInstantiation, "Cannot instantiate abstract class", "Semantic",
            "An attempt was made to create an instance of an abstract class. Abstract classes can only be subclassed, not instantiated directly.",
            "abstract class Shape:\n    def area(self) -> float:\n        ...\n\ns = Shape()  # cannot instantiate",
            "Create a concrete subclass:\nclass Circle(Shape):\n    radius: float\n    def area(self) -> float:\n        return 3.14159 * self.radius * self.radius");

        Add(dict, DiagnosticCodes.Semantic.InvalidInheritance, "Invalid inheritance", "Semantic",
            "A class attempted to inherit from a type that cannot be used as a base class. For example, inheriting from a struct, enum, or sealed class.",
            "struct Point:\n    x: int\n    y: int\n\nclass Point3D(Point):  # cannot inherit from struct\n    z: int",
            "Use class inheritance only with other classes, or use composition instead.");

        Add(dict, DiagnosticCodes.Semantic.IncompatibleOverride, "Incompatible method override", "Semantic",
            "A method in a subclass overrides a base class method but with an incompatible signature. The parameter types and return type must be compatible with the base method.",
            "class Base:\n    def process(self, x: int) -> str:\n        return str(x)\n\nclass Sub(Base):\n    def process(self, x: str) -> int:  # incompatible\n        return 0",
            "Match the base method's signature:\nclass Sub(Base):\n    def process(self, x: int) -> str:\n        return f\"value: {x}\"");

        Add(dict, DiagnosticCodes.Semantic.AccessViolation, "Access violation", "Semantic",
            "Code attempted to access a member that is not accessible from the current context. Members prefixed with _ are protected (accessible in subclasses), and members prefixed with __ are private (accessible only within the defining class).",
            "class Secret:\n    __key: str = \"hidden\"\n\ns = Secret()\nprint(s.__key)  # private access",
            "Use a public method to expose the value, or change the access level.");

        Add(dict, DiagnosticCodes.Semantic.SuperOutsideClass, "super() outside class", "Semantic",
            "The super() call was used outside of a class method. super() is only valid inside instance methods of a class that has a base class.",
            "def foo():\n    super().__init__()  # not in a class",
            "Use super() only inside a class method:\nclass Sub(Base):\n    def __init__(self):\n        super().__init__()");

        Add(dict, DiagnosticCodes.Semantic.SuperNoParent, "super() in class without parent", "Semantic",
            "super() was called in a class that does not have a base class. super() is only meaningful in classes that inherit from another class.",
            "class Root:\n    def __init__(self):\n        super().__init__()  # Root has no parent",
            "Remove the super() call or add a base class:\nclass Root(Base):\n    def __init__(self):\n        super().__init__()");

        Add(dict, DiagnosticCodes.Semantic.DuplicateClass, "Duplicate class definition", "Semantic",
            "A class name was defined more than once in the same scope. Each class name must be unique.",
            "class Foo:\n    x: int\n\nclass Foo:  # duplicate\n    y: str",
            "Rename one of the classes or merge them into a single definition.");

        Add(dict, DiagnosticCodes.Semantic.InvalidSuperUsage, "Invalid super() usage", "Semantic",
            "super() was used in an invalid way. super() must be called as a function and used to access base class methods.",
            "class Sub(Base):\n    def foo(self):\n        x = super  # not called as a function",
            "Call super() as a function:\nclass Sub(Base):\n    def foo(self):\n        super().foo()");

        Add(dict, DiagnosticCodes.Semantic.CircularInheritance, "Circular inheritance detected", "Semantic",
            "A class or interface inherits from itself through its inheritance chain, creating a cycle. " +
            "For example, class A extends B and class B extends A, or interface IA extends IB and IB extends IA.",
            "class A(B):\n    pass\n\nclass B(A):\n    pass",
            "Break the cycle by removing one of the inheritance relationships or restructuring the type hierarchy.");

        Add(dict, DiagnosticCodes.Semantic.InstanceFieldViaTypeName,
            "Instance field accessed via type name",
            "Semantic",
            "An instance field is being accessed through the type name (e.g., ClassName.field) rather than " +
            "through an instance. Only static and const fields can be accessed via the type name.",
            "class Config:\n    timeout: int = 30\n\ndef main():\n    t = Config.timeout  # error: timeout is an instance field",
            "Mark the field as @static if it should be shared across instances:\nclass Config:\n    @static\n    timeout: int = 30\n\n" +
            "Or use an instance:\ndef main():\n    c = Config()\n    t = c.timeout");

        Add(dict, DiagnosticCodes.Semantic.MaybeOnUnconstrainedTypeParameter,
            "'maybe' on unconstrained generic type parameter",
            "Semantic",
            "The 'maybe' operator cannot be used with an unconstrained generic type parameter because " +
            "Optional.From<T> has separate overloads for reference types (where T : class) and value types " +
            "(where T : struct). When T is unconstrained, the compiler cannot determine which overload to use.",
            "def wrap[T](value: T | None) -> T?:\n    return maybe value  # error: T is unconstrained",
            "Constrain the type parameter or use a different pattern to handle the optional value.");

        // ── Semantic errors: Import (SPY0300-SPY0319) ───────────────────

        Add(dict, DiagnosticCodes.Semantic.ModuleNotFound, "Module not found", "Semantic",
            "An import statement references a module that could not be found. The compiler searches the current directory, configured module paths, and standard library paths.",
            "from utils import helper  # utils.spy not found",
            "Ensure the module file exists and is in a searchable path. Use --module-path to add search directories.");

        Add(dict, DiagnosticCodes.Semantic.ImportError, "Import error", "Semantic",
            "A symbol requested in a from-import statement was not found in the target module. The module exists but does not export the specified name.",
            "from math_utils import nonexistent_func",
            "Check the module's exported symbols. Make sure the name is spelled correctly and is public (not prefixed with _ or __).");

        Add(dict, DiagnosticCodes.Semantic.CircularImport, "Circular import", "Semantic",
            "Two or more modules import each other, creating a cycle. Module A imports from B, and B (directly or indirectly) imports from A.",
            "# a.spy\nfrom b import foo\n\n# b.spy\nfrom a import bar",
            "Break the cycle by:\n  1. Moving shared code to a third module\n  2. Restructuring the dependency graph\n  3. Using lazy imports where possible");

        Add(dict, DiagnosticCodes.Semantic.CircularImportStubError, "Circular import stub error", "Semantic",
            "Only type declarations (class, struct, interface, enum) can be imported from modules involved in a circular dependency. Non-type symbols like functions or variables require the full module to be loaded, which is impossible during a cycle.",
            "# a.spy\nfrom b import some_function  # Error: some_function is not a type\n\n# b.spy\nfrom a import MyClass  # OK: MyClass is a type declaration",
            "Move the non-type symbol to a third module that both can import, or restructure to break the cycle.");

        Add(dict, DiagnosticCodes.Semantic.CircularImportRuntimeUsage, "Circular import runtime usage", "Semantic",
            "A type imported from a circular dependency can only be used in type annotations, not at runtime. Constructor calls, static method access, and other runtime usage require the full type definition, which is not available during a cycle.",
            "from other import MyType\n\nx: MyType  # OK: type annotation\ny = MyType()  # Error: runtime usage",
            "Move the type to a non-circular import, or restructure your modules to break the dependency cycle.");

        Add(dict, DiagnosticCodes.Semantic.CircularImportBaseClass, "Circular import base class", "Semantic",
            "A type from a circular import cannot be used as a base class. Base types require full type information (fields, methods, layout) at compile time, which is not available for types from circular dependencies.",
            "from other import Base\n\nclass Child(Base):  # Error: Base is from a circular import\n    pass",
            "Move the base class to a module that does not participate in the cycle, or restructure the dependency graph.");

        Add(dict, DiagnosticCodes.Semantic.ModuleLoadError, "Module load error", "Semantic",
            "A module was found but could not be loaded, typically due to syntax errors in the module's source file.",
            null,
            "Fix the errors in the imported module first, then retry the compilation.");

        Add(dict, DiagnosticCodes.Semantic.AssemblyNotFound, "Assembly not found", "Semantic",
            "A .NET assembly referenced in an import could not be found. The compiler searches configured assembly paths.",
            "import SomeLibrary  # .NET assembly not found",
            "Ensure the assembly DLL is available and the assembly search path is configured correctly.");

        Add(dict, DiagnosticCodes.Semantic.AssemblyLoadError, "Assembly load error", "Semantic",
            "A .NET assembly was found but could not be loaded, typically due to version incompatibility or missing dependencies.",
            null,
            "Check the assembly's target framework compatibility and ensure all dependencies are available.");

        // ── Semantic errors: Protocol and operator (SPY0320-SPY0339) ────

        Add(dict, DiagnosticCodes.Semantic.ProtocolMissingMethod, "Protocol method not implemented", "Semantic",
            "A class claims to implement an interface (protocol) but is missing one or more required methods. All interface methods must be implemented.",
            "interface Printable:\n    def display(self) -> str: ...\n\nclass Foo(Printable):  # missing display()\n    x: int",
            "Implement all required methods:\nclass Foo(Printable):\n    x: int\n    def display(self) -> str:\n        return str(self.x)");

        Add(dict, DiagnosticCodes.Semantic.InvalidOperatorSignature, "Invalid operator signature", "Semantic",
            "An operator overload method has an incorrect signature. Operator methods must follow specific parameter and return type conventions.",
            "class Vec:\n    def __add__(self) -> Vec:  # missing 'other' parameter\n        return self",
            "Use the correct operator signature:\nclass Vec:\n    def __add__(self, other: Vec) -> Vec:\n        return Vec()");

        Add(dict, DiagnosticCodes.Semantic.InvalidDecoratorUsage, "Invalid decorator usage", "Semantic",
            "A decorator was used in an invalid context or with invalid arguments. Check that the decorator is appropriate for the target (function, method, or class).",
            "@override\ndef top_level_func():  # @override only valid on class methods\n    pass",
            "Use the decorator on an appropriate target, or remove it.");

        Add(dict, DiagnosticCodes.Semantic.ConflictingSynthesizedInterface,
            "Conflicting synthesized interface",
            "Semantic",
            "A class would synthesize a generic interface (e.g., IEquatable<T>, IEnumerator<T>) " +
            "from a dunder method, but an ancestor class already implements the same generic interface " +
            "with different type arguments. C# does not allow a type to implement the same generic interface " +
            "with conflicting type arguments.",
            "class Base:\n    def __eq__(self, other: str) -> bool:\n        return False\n\n" +
            "class Derived(Base):\n    def __eq__(self, other: int) -> bool:  # conflicts with Base's IEquatable<str>\n        return False",
            "Remove the conflicting dunder method from the derived class, or restructure the hierarchy " +
            "so that both classes use the same type argument for the interface.");

        Add(dict, DiagnosticCodes.Semantic.WithNotDisposable,
            "Type not usable in with statement",
            "Semantic",
            "The expression used in a 'with' statement must either implement IDisposable (for .NET types) " +
            "or define __enter__/__exit__ methods (for Sharpy context manager protocol). " +
            "For 'async with', the type must implement IAsyncDisposable or define __aenter__/__aexit__ methods.",
            "class Foo:\n    pass\n\ndef main():\n    with Foo() as f:\n        print(f)",
            "Either implement IDisposable on the class, or add __enter__ and __exit__ methods for the context manager protocol.");

        Add(dict, DiagnosticCodes.Semantic.InterfaceMethodNotImplemented,
            "Interface method not implemented",
            "Semantic",
            "A class or struct declares that it implements an interface but does not provide an implementation " +
            "for one or more of the interface's abstract methods. All abstract methods from all implemented " +
            "interfaces (including base interfaces) must be implemented unless the class is abstract.",
            "interface Drawable:\n    def draw(self) -> str:\n        ...\n\nclass Circle(Drawable):\n    pass",
            "Add the missing method implementation:\nclass Circle(Drawable):\n    def draw(self) -> str:\n        return \"circle\"");

        Add(dict, DiagnosticCodes.Semantic.OptionalRequiresNarrowing,
            "Optional requires narrowing",
            "Semantic",
            "A strict Optional value (T?) was used directly in a protocol operation such as len(), " +
            "membership testing (in), indexing, or iteration. An Optional may be None, so it must be " +
            "narrowed (if x is not None:) or unwrapped (x.unwrap()) before the underlying value's " +
            "protocol is available. This is distinct from a genuinely missing protocol (SPY0320).",
            "def main() -> None:\n    s: str? = \"hello\"\n    print(len(s))  # s may be None",
            "Narrow or unwrap the Optional first:\n    if s is not None:\n        print(len(s))");

        Add(dict, DiagnosticCodes.Semantic.TupleNonConstantIndex,
            "Tuple index must be a constant",
            "Semantic",
            "A tuple was indexed with a non-constant value (e.g. a variable). Tuples are " +
            "heterogeneous — each position can hold a different type — so the element type must be " +
            "known at compile time. A C# ValueTuple also has no runtime indexer; indexing is lowered " +
            "to .ItemN, which requires a literal index.",
            "def main() -> None:\n    t: tuple[int, str] = (1, \"a\")\n    i: int = 0\n    print(t[i])  # i is not a constant",
            "Use a literal index (print(t[0])), unpack the tuple (a, b = t), or use a list[T] " +
            "if you genuinely need dynamic indexing.");

        Add(dict, DiagnosticCodes.Semantic.IntegerPowerOverflow,
            "Integer exponentiation overflows a 64-bit integer",
            "Semantic",
            "A constant integer power (base ** exponent) was constant-folded at compile time and its " +
            "result does not fit a 64-bit integer. Per Axiom 1, Sharpy integers are fixed-width (.NET " +
            "int/long) rather than Python's arbitrary precision, so the overflow is reported at compile " +
            "time instead of silently producing a truncated or lossy value.",
            "def main() -> None:\n    x = 10 ** 50  # exceeds the range of long",
            "Use a floating-point base to get an (approximate) double result (10.0 ** 50), or " +
            "restructure the computation so intermediate values stay within long range.");

        Add(dict, DiagnosticCodes.Semantic.VoidComparisonOperand,
            "Void-returning call used as a comparison operand",
            "Semantic",
            "A call to a function that returns None was used as an operand of == or !=. The call " +
            "has no value to compare, so the comparison is almost certainly a bug. Python would " +
            "evaluate the call and compare its None result, but per Axiom 3 Sharpy rejects this at " +
            "compile time instead of silently comparing against nothing (the None literal itself " +
            "remains valid: x == None is a null check).",
            "def f() -> None:\n    print(\"side effect\")\n\ndef main() -> None:\n    s: str = \"hello\"\n    if s == f():  # f() has no value\n        pass",
            "Call the function as a statement, then compare separately:\n    f()\n    if s == None:\n        pass");

        // ── Semantic errors: Module level (SPY0340-SPY0349) ─────────────

        Add(dict, DiagnosticCodes.Semantic.ModuleLevelExecutableStatement, "Executable statement at module level", "Semantic",
            "An executable statement (like a function call or expression) was found at module level. Only declarations (functions, classes, variables, imports) are allowed at module level. Executable code must be inside the main() function.",
            "print(\"hello\")  # at module level\n\ndef main():\n    pass",
            "Move executable code into the main() function:\ndef main():\n    print(\"hello\")");

        Add(dict, DiagnosticCodes.Semantic.ModuleLevelNoTypeAnnotation, "Module-level variable without type annotation", "Semantic",
            "A variable at module level is missing its type annotation. Module-level variables must always have explicit type annotations.",
            "x = 42  # missing type annotation at module level",
            "Add a type annotation:\n  x: int = 42");

        // ── Semantic errors: Additional (SPY0350-SPY0399) ───────────────

        Add(dict, DiagnosticCodes.Semantic.SelfInitOutsideConstructor, "self.__init__() outside constructor", "Semantic",
            "A call to self.__init__() was found outside of an __init__ method. Constructor chaining via self.__init__() is only valid inside a constructor.",
            "def method(self):\n    self.__init__(42)  # not allowed here",
            "Move self.__init__() calls into an __init__ method for constructor chaining.");

        Add(dict, DiagnosticCodes.Semantic.ConflictingConstructorInitializers, "Conflicting constructor initializers", "Semantic",
            "A constructor has both super().__init__() and self.__init__() calls. A constructor can chain to either a base constructor or another constructor of the same class, but not both.",
            "def __init__(self, x: int):\n    super().__init__()\n    self.__init__(x, 0)",
            "Remove one of the chaining calls — use either super().__init__() or self.__init__(), not both.");

        Add(dict, DiagnosticCodes.Semantic.TypeAliasArityMismatch, "Type alias arity mismatch", "Semantic",
            "A generic type alias was used with the wrong number of type arguments.",
            "type Pair[T] = tuple[T, T]\nx: Pair[int, str] = (1, \"a\")  # Pair takes 1 type argument, got 2",
            "Provide the correct number of type arguments matching the alias definition.");

        Add(dict, DiagnosticCodes.Semantic.AmbiguousOverload, "Ambiguous method overload", "Semantic",
            "A method call matches multiple overloads equally well and the compiler cannot determine which to use.",
            "class Foo:\n    def bar(self, x: int): ...\n    def bar(self, x: float): ...\nfoo.bar(42)",
            "Add an explicit type annotation or cast to disambiguate the call.");

        Add(dict, DiagnosticCodes.Semantic.NoMatchingOverload, "No matching method overload", "Semantic",
            "A method call does not match any of the available overloads for the method.",
            "class Foo:\n    def bar(self, x: int): ...\nfoo.bar(\"hello\")",
            "Check the argument types and ensure they match one of the declared overloads.");

        Add(dict, DiagnosticCodes.Semantic.DuplicateMethodSignature, "Duplicate method signature", "Semantic",
            "Two method overloads have identical parameter signatures. Overloads must differ in parameter count or types.",
            "class Foo:\n    def bar(self, x: int): ...\n    def bar(self, y: int): ...",
            "Change the parameter types or count to differentiate overloads.");

        Add(dict, DiagnosticCodes.Semantic.MultipleStarExpressions, "Multiple star expressions in unpacking", "Semantic",
            "More than one starred expression (*rest) was found in an unpacking target. Only one starred expression is allowed per unpacking.",
            "first, *middle, *end = items",
            "Use only one starred expression:\nfirst, *rest = items");

        Add(dict, DiagnosticCodes.Semantic.SpreadIntoNonVariadic, "Spread into non-variadic parameter", "Semantic",
            "A spread argument (*args) was used in a call to a function that does not have a variadic parameter.",
            "def foo(a: int, b: int): ...\nitems: list[int] = [1, 2]\nfoo(*items)",
            "Use a function with variadic parameters (*args) or pass arguments individually.");

        Add(dict, DiagnosticCodes.Semantic.UnsupportedFeature, "Unsupported feature", "Semantic",
            "A language feature was used that is recognized by the parser but not yet supported in semantic analysis or code generation.",
            "match x:\n    case [1, 2, 3]:  # list patterns not yet supported\n        print(x)",
            "Use a supported pattern type such as literal patterns, binding patterns, wildcard patterns, tuple patterns, or member access patterns.");
        Add(dict, DiagnosticCodes.Semantic.BindingInOrPattern, "Binding pattern in or-pattern", "Semantic",
            "A binding pattern was used inside an or-pattern (|). C# does not allow variable bindings in or-patterns because the variable would only be assigned in one branch.",
            "match x:\n    case y | 2:  # binding 'y' not allowed in or-pattern\n        print(y)",
            "Use literal patterns, wildcard patterns, or member access patterns inside or-patterns:\nmatch x:\n    case 1 | 2:\n        print(\"one or two\")");

        Add(dict, DiagnosticCodes.Semantic.RelationalPatternTypeMismatch, "Relational pattern type mismatch", "Semantic",
            "A relational pattern (>, <, >=, <=) was used with a non-numeric scrutinee type. Relational patterns require a numeric type.",
            "match name:\n    case > 0:  # name is str, not numeric\n        print(name)",
            "Use relational patterns only with numeric types (int, float, etc.).");

        Add(dict, DiagnosticCodes.Semantic.TypePatternIncompatible, "Incompatible type pattern", "Semantic",
            "A type pattern was used with a type that is incompatible with the scrutinee type.",
            "x: int = 42\nmatch x:\n    case str() as s:  # int cannot be str\n        print(s)",
            "Use a type pattern that is compatible with the scrutinee type.");

        Add(dict, DiagnosticCodes.Semantic.PropertyPatternUnknownField, "Unknown field in property pattern", "Semantic",
            "A property pattern referenced a field that does not exist on the matched type.",
            "class Point:\n    x: int\n    y: int\nmatch p:\n    case Point(z=0):  # Point has no field 'z'\n        ...",
            "Use field names that exist on the type being matched.");

        Add(dict, DiagnosticCodes.Semantic.PositionalPatternCountMismatch, "Positional pattern count mismatch", "Semantic",
            "A positional pattern has a different number of elements than the fields of the matched type.",
            "class Point:\n    x: int\n    y: int\nmatch p:\n    case Point(1, 2, 3):  # Point has 2 fields, got 3\n        ...",
            "Provide the correct number of positional elements matching the type's fields.");

        Add(dict, DiagnosticCodes.Semantic.UnsupportedPatternInMemberAccessOr, "Unsupported pattern mixed with member access in or-pattern", "Semantic",
            "An or-pattern that contains a member access pattern also contains a pattern type that cannot be combined with it. Only literal, member access, and wildcard patterns can be mixed with member access patterns in or-patterns.",
            "match x:\n    case int() | Color.RED:  # type pattern cannot mix with member access\n        ...",
            "Use only literal values, member access, or wildcard patterns alongside member access patterns:\nmatch x:\n    case 1 | Color.RED:\n        ...");

        Add(dict, DiagnosticCodes.Semantic.DuplicateUnionCase, "Duplicate union case", "Semantic",
            "A union type has two or more cases with the same name. Each union case must have a unique name.",
            "union Shape:\n    case Circle(radius: float)\n    case Circle(diameter: float)",
            "Give each union case a unique name:\nunion Shape:\n    case Circle(radius: float)\n    case Square(side: float)");

        Add(dict, DiagnosticCodes.Semantic.UnionCaseNameConflict, "Union case name conflicts with union type", "Semantic",
            "A union case has the same name as its enclosing union type. This would produce a C# nested class with the same name as its parent, which is invalid.",
            "union Shape:\n    case Shape(radius: float)  # case name collides with union name",
            "Give the union case a different name:\nunion Shape:\n    case Circle(radius: float)");

        Add(dict, DiagnosticCodes.Semantic.UnionCaseNotFound, "Unknown union case in pattern", "Semantic",
            "A pattern references a case name that does not exist on the union type being matched.",
            "union Result:\n    case Ok(value: int)\n    case Err(msg: str)\nmatch r:\n    case Unknown(v):  # no case 'Unknown' on Result\n        ...",
            "Use a case name that exists on the union type:\nmatch r:\n    case Ok(v):\n        print(v)");

        Add(dict, DiagnosticCodes.Semantic.UnionCaseFieldMismatch, "Wrong number of fields in union case pattern", "Semantic",
            "A union case pattern has a different number of field bindings than the union case declares.",
            "union Result:\n    case Ok(value: int)\nmatch r:\n    case Ok(a, b):  # Ok has 1 field, got 2\n        ...",
            "Provide the correct number of field bindings matching the union case definition:\nmatch r:\n    case Ok(v):\n        print(v)");

        Add(dict, DiagnosticCodes.Semantic.PositionalPatternNoDeconstruct, "Positional pattern without Deconstruct", "Semantic",
            "A positional pattern was used on a type that does not support it. The type has no Deconstruct method and the number of pattern elements does not match the type's field count.",
            "class Point:\n    x: int\n    y: int\nmatch p:\n    case Point(a, b, c):  # Point has 2 fields, not 3\n        ...",
            "Use the correct number of positional elements matching the type's fields, or use a property pattern:\nmatch p:\n    case Point(a, b):\n        print(a, b)");

        Add(dict, DiagnosticCodes.Semantic.PositionalOnlyPassedByKeyword, "Positional-only parameter passed by keyword", "Semantic",
            "A parameter that is declared as positional-only (before '/') was passed as a keyword argument. Positional-only parameters cannot be referred to by name at call sites.",
            "def foo(x: int, /) -> int:\n    return x\nfoo(x=1)  # error: 'x' is positional-only",
            "Pass the argument positionally:\nfoo(1)");

        Add(dict, DiagnosticCodes.Semantic.KeywordOnlyPassedPositionally, "Keyword-only parameter passed positionally", "Semantic",
            "A parameter that is declared as keyword-only (after '*' or '*args') was passed positionally. Keyword-only parameters must be passed by name.",
            "def foo(*, key: int) -> int:\n    return key\nfoo(1)  # error: 'key' is keyword-only",
            "Pass the argument by name:\nfoo(key=1)");

        Add(dict, DiagnosticCodes.Semantic.DuplicateCaptureInPattern, "Duplicate capture in and-pattern", "Semantic",
            "The same capture name is bound on both sides of an 'and' pattern. A name may be captured only once across the combined pattern.",
            "match items:\n    case [x, y] and [x, z]:\n        ...",
            "Use distinct capture names on each side:\nmatch items:\n    case [a, b] and whole:\n        ...");


        // ── Semantic errors: Events (SPY0373-SPY0379) ──────────────────────

        Add(dict, DiagnosticCodes.Semantic.EventTypeNotDelegate, "Event type is not a delegate", "Semantic",
            "An event declaration specifies a type that is not a delegate. Events must be declared with a delegate type that specifies the handler signature.",
            "event on_click: int  # error: int is not a delegate type",
            "Use a delegate type for the event:\ndelegate EventHandler(self) -> None\nevent on_click: EventHandler");

        Add(dict, DiagnosticCodes.Semantic.EventAccessorParamMismatch, "Event accessor parameter mismatch", "Semantic",
            "A function-style event accessor has parameters that don't match the event's delegate type. The handler parameter must be assignable to the delegate type.",
            "delegate ClickHandler(self) -> None\nevent add on_click(self, handler: int):  # error: int != ClickHandler\n    pass",
            "Use the correct delegate type as the handler parameter:\nevent add on_click(self, handler: ClickHandler):\n    pass");

        Add(dict, DiagnosticCodes.Semantic.DirectEventAssignment, "Direct event assignment not allowed", "Semantic",
            "An event was assigned to directly using '=' instead of using '+=' to add a handler or '-=' to remove a handler. Events cannot be overwritten; handlers must be added or removed.",
            "btn.on_click = my_handler  # error: direct assignment",
            "Use += to add a handler or -= to remove one:\nbtn.on_click += my_handler");

        Add(dict, DiagnosticCodes.Semantic.EventHandlerTypeMismatch, "Event handler type mismatch", "Semantic",
            "A handler being added to or removed from an event is not compatible with the event's delegate type. The handler type must be assignable to the delegate type.",
            "delegate ClickHandler(self) -> None\nevent on_click: ClickHandler\ndef invalid_handler(x: int) -> None: pass\nbtn.on_click += invalid_handler  # error: signature mismatch",
            "Use a handler with the correct signature:\ndef valid_handler(self) -> None: pass\nbtn.on_click += valid_handler");

        Add(dict, DiagnosticCodes.Semantic.RaiseEventOutsideClass, "Event raise outside class", "Semantic",
            "An event is being raised (invoked) from outside the class that declares it. Events are protected and can only be raised from within their declaring class.",
            "btn.on_click()  # error: cannot raise from outside Button class",
            "Only call the event from within its declaring class, or provide a public method to raise it:\n# Inside Button class:\ndef do_click(self):\n    self.on_click()  # OK: inside the class");

        Add(dict, DiagnosticCodes.Semantic.EventUnsupportedOperator, "Unsupported event operator", "Semantic",
            "An augmented operator other than '+=' or '-=' was used on an event. Events only support adding handlers with '+=' and removing handlers with '-='.",
            "btn.on_click *= handler  # error: *= not supported",
            "Use '+=' to add a handler or '-=' to remove one:\nbtn.on_click += handler\nbtn.on_click -= handler");

        // ── Semantic errors: Dataclass (SPY0380-SPY0383) ────────────────────

        Add(dict, DiagnosticCodes.Semantic.DataclassOnNonClass, "@dataclass on non-class type", "Semantic",
            "The @dataclass decorator can only be applied to class definitions. Structs and interfaces do not support @dataclass.",
            "@dataclass\nstruct Point:\n    x: int\n    y: int",
            "Use a class instead:\n@dataclass\nclass Point:\n    x: int\n    y: int");

        Add(dict, DiagnosticCodes.Semantic.DataclassFieldOrdering, "Dataclass field ordering error", "Semantic",
            "A field without a default value follows a field with a default value in a @dataclass. Fields without defaults must come before fields with defaults.",
            "@dataclass\nclass Bad:\n    x: int = 10\n    y: int  # error: no default after default",
            "Reorder fields so non-default fields come first:\n@dataclass\nclass Good:\n    y: int\n    x: int = 10");

        Add(dict, DiagnosticCodes.Semantic.DataclassFieldNoType, "Dataclass field missing type annotation", "Semantic",
            "A field in a @dataclass does not have a type annotation. All dataclass fields must have explicit type annotations.",
            "@dataclass\nclass Bad:\n    x = 10  # error: no type annotation",
            "Add a type annotation:\n@dataclass\nclass Good:\n    x: int = 10");

        Add(dict, DiagnosticCodes.Semantic.DataclassInvalidOption, "Invalid @dataclass option", "Semantic",
            "An unrecognized or invalid option was passed to @dataclass. Valid options are frozen, eq, and repr, all of which must be boolean values.",
            "@dataclass(frozen=\"yes\")  # error: must be True/False\nclass Bad:\n    x: int",
            "Use boolean values for @dataclass options:\n@dataclass(frozen=True)\nclass Good:\n    x: int");

        // ── Semantic errors: Self type (SPY0384-SPY0385) ────────────────────

        Add(dict, DiagnosticCodes.Semantic.SelfOutsideClass, "Self type outside class", "Semantic",
            "'Self' can only be used inside a class, struct, or interface definition. It refers to the enclosing type and has no meaning at module level or in standalone functions.",
            "def make() -> Self:\n    ...",
            "Use Self only inside a class:\nclass Builder:\n    def make(self) -> Self:\n        return self");

        Add(dict, DiagnosticCodes.Semantic.SelfInStaticMethod, "Self type in static method", "Semantic",
            "'Self' cannot be used in static methods because there is no instance type to refer to.",
            "class Foo:\n    @static\n    def create() -> Self:\n        ...",
            "Use the concrete class name instead:\nclass Foo:\n    @static\n    def create() -> Foo:\n        return Foo()");

        // ── Semantic errors: Builtin call errors (SPY0386+) ─────────────

        Add(dict, DiagnosticCodes.Semantic.UnsupportedTypeNone, "type(None) is not supported", "Semantic",
            "Python's type(None) returns <class 'NoneType'>, but NoneType has no Sharpy equivalent. None in Sharpy is a null literal, not an instance of a type.",
            "x = type(None)",
            "Use a type annotation or isinstance() check instead of type(None).");

        // ── Semantic errors: Parameter modifier errors (SPY0387-SPY0391) ─

        Add(dict, DiagnosticCodes.Semantic.ModifierWithDefault, "ref/out/in parameter cannot have a default value", "Semantic",
            "Parameters with ref, out, or in modifiers require the caller to explicitly pass the argument at the call site. A default value would bypass this requirement, which is semantically invalid.",
            "def foo(x: ref int = 5): ...",
            "Remove the default value: def foo(x: ref int): ...");

        Add(dict, DiagnosticCodes.Semantic.ModifierWithVariadic, "variadic parameter cannot have a modifier", "Semantic",
            "Variadic parameters (*args) collect multiple arguments into a list. ref/out/in modifiers require pass-by-reference semantics which are incompatible with variadic collection.",
            "def foo(*args: ref int): ...",
            "Remove the modifier or use individual parameters: def foo(a: ref int, b: ref int): ...");

        Add(dict, DiagnosticCodes.Semantic.ModifierRequiresVariable, "ref/out argument must be a variable", "Semantic",
            "ref and out arguments must refer to a storage location (variable, field, or indexer) that can be written to. Literals and expression results have no storage location.",
            "swap(ref 5, ref 10)",
            "Pass a variable instead: x = 5; swap(ref x, ref y)");

        Add(dict, DiagnosticCodes.Semantic.InParameterReassignment, "cannot reassign 'in' parameter", "Semantic",
            "Parameters declared with the 'in' modifier are passed by readonly reference. They cannot be reassigned to prevent unintended mutation of the caller's value.",
            "def foo(x: in int):\n    x = 0  # Error",
            "Remove the 'in' modifier if reassignment is needed, or use a local copy: local_x = x");

        // ── Semantic errors: except* (SPY0391-SPY0394) ─────────────────
        Add(dict, DiagnosticCodes.Semantic.ExceptStarCatchesExceptionGroup, "'except*' cannot catch ExceptionGroup", "Semantic",
            "An 'except*' handler cannot catch ExceptionGroup directly. The except* syntax is designed to match individual exception types within an ExceptionGroup, not the group itself. Use a regular 'except' handler to catch ExceptionGroup.",
            "try:\n    ...\nexcept* ExceptionGroup as eg:\n    ...",
            "Use 'except' instead of 'except*':\n  except ExceptionGroup as eg:\n      ...");

        Add(dict, DiagnosticCodes.Semantic.BreakInExceptStar, "'break' not allowed in except* handler", "Semantic",
            "'break' statements are not allowed inside 'except*' handlers (PEP 654). This restriction exists because except* handlers may execute for only a subset of exceptions in an ExceptionGroup, and control flow statements would interfere with the exception splitting logic.",
            "for i in range(10):\n    try:\n        ...\n    except* ValueError as eg:\n        break  # Error",
            "Handle the break condition outside the except* handler, for example by setting a flag variable.");

        Add(dict, DiagnosticCodes.Semantic.ContinueInExceptStar, "'continue' not allowed in except* handler", "Semantic",
            "'continue' statements are not allowed inside 'except*' handlers (PEP 654). This restriction exists because except* handlers may execute for only a subset of exceptions in an ExceptionGroup, and control flow statements would interfere with the exception splitting logic.",
            "for i in range(10):\n    try:\n        ...\n    except* ValueError as eg:\n        continue  # Error",
            "Handle the continue condition outside the except* handler, for example by setting a flag variable.");

        Add(dict, DiagnosticCodes.Semantic.ReturnInExceptStar, "'return' not allowed in except* handler", "Semantic",
            "'return' statements are not allowed inside 'except*' handlers (PEP 654). This restriction exists because except* handlers may execute for only a subset of exceptions in an ExceptionGroup, and control flow statements would interfere with the exception splitting logic.",
            "def foo():\n    try:\n        ...\n    except* ValueError as eg:\n        return  # Error",
            "Handle the return condition outside the except* handler, for example by setting a flag variable.");

        // ── Semantic errors: Generic type parameter defaults (SPY0395-SPY0396) ─

        Add(dict, DiagnosticCodes.Semantic.TypeParameterDefaultOrdering,
            "Type parameter without default follows one with default",
            "Semantic",
            "In a type parameter list, once one type parameter has a default, all subsequent type parameters must also have defaults (PEP 696).",
            "class Foo[T = int, U]:  # Error: U has no default but T does",
            "Either add a default to all trailing type parameters or reorder them:\nclass Foo[U, T = int]: ...");

        Add(dict, DiagnosticCodes.Semantic.TypeParameterDefaultViolatesConstraint,
            "Type parameter default violates constraint",
            "Semantic",
            "A type parameter's default type does not satisfy its declared constraints. The default type must conform to all constraints (class, struct, or interface/base type).",
            "class Foo[T: class = int]:  # Error: int is a value type, not a class",
            "Use a default type that satisfies the constraint:\nclass Foo[T: class = str]: ...");

        // ── Semantic errors: Exception filters (SPY0397-SPY0398) ──

        Add(dict, DiagnosticCodes.Semantic.ExceptionFilterNotBoolean,
            "Exception filter must be a boolean expression",
            "Semantic",
            "The 'when' clause in an except handler must evaluate to a boolean value. The filter determines whether the handler matches the exception.",
            "except ValueError as e when \"not a bool\":",
            "Use a boolean expression:\nexcept ValueError as e when e.message == \"expected\":");

        Add(dict, DiagnosticCodes.Semantic.ExceptStarWhenNotSupported,
            "'except*' handlers do not support 'when' filters",
            "Semantic",
            "Exception filters (when clauses) cannot be used with except* handlers. This is a language restriction.",
            "except* ValueError as e when True:",
            "Use a regular except handler with a when filter, or filter inside the except* body.");

        // ── Semantic errors: Try expression (SPY0399) ──

        Add(dict, DiagnosticCodes.Semantic.TryExceptionTypeNotException,
            "Try expression exception type must inherit from Exception",
            "Semantic",
            "Every type listed in a 'try[...]' expression must be a subclass of Exception. Only exception types can be caught at runtime; listing a non-exception type would never match.",
            "result = try[ValueError | NotAnException] foo()",
            "Use exception types in the union, or remove the non-exception entries:\nresult = try[ValueError | KeyError] foo()");

    }
}
