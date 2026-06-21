namespace Sharpy.Playground.Examples;

public static class ExampleSnippets
{
    public static IReadOnlyList<(string Name, string Source)> All { get; } =
    [
        ("Hello World", HelloWorld),
        ("Classes & Inheritance", ClassesAndInheritance),
        ("Tagged Unions", TaggedUnions),
        ("Optional Types", OptionalTypes),
        ("Result Types", ResultTypes),
        ("? Operator (Early Return)", QuestionMarkOperator),
        ("List Comprehensions", ListComprehensions),
        ("Properties & Access Modifiers", PropertiesAndAccess),
        ("Operator Overloading", OperatorOverloading),
        ("Pipe Operator", PipeOperator),
        ("Generators", Generators),
        ("Partial Application", PartialApplication),
        ("Try & Maybe Expressions", TryAndMaybeExpressions),
        ("Null-Conditional & Coalescing", NullConditionalAndCoalescing),
    ];

    private const string HelloWorld = """
        def main():
            print("Hello, World!")
            print(f"2 + 2 = {2 + 2}")
        """;

    private const string ClassesAndInheritance = """
        class Animal:
            _name: str
            _sound: str

            def __init__(self, name: str, sound: str):
                self._name = name
                self._sound = sound

            @virtual
            def speak(self) -> str:
                return f"{self._name} says {self._sound}"

            def __str__(self) -> str:
                return self._name

        class Dog(Animal):
            __tricks: list[str]

            def __init__(self, name: str):
                super().__init__(name, "Woof")
                self.__tricks = []

            def learn_trick(self, trick: str):
                self.__tricks.append(trick)

            @override
            def speak(self) -> str:
                return f"{super().speak()}! I know {len(self.__tricks)} tricks."

        def main():
            dog = Dog("Rex")
            dog.learn_trick("sit")
            dog.learn_trick("shake")
            print(dog.speak())
        """;

    private const string TaggedUnions = """
        union Shape:
            case Circle(radius: float)
            case Rectangle(width: float, height: float)
            case Triangle(base_len: float, height: float)

        def describe(shape: Shape) -> str:
            match shape:
                case Circle(r):
                    return f"Circle with radius {r}, area = {3.14159 * r * r}"
                case Rectangle(w, h):
                    return f"Rectangle {w}x{h}, area = {w * h}"
                case _:
                    return "Unknown shape"

        def main():
            c = Shape.Circle(5.0)
            r = Shape.Rectangle(3.0, 4.0)
            t = Shape.Triangle(6.0, 3.0)
            print(describe(c))
            print(describe(r))
            print(describe(t))
        """;

    private const string OptionalTypes = """
        def find_item(items: list[str], target: str) -> str?:
            for item in items:
                if item == target:
                    return Some(item)
            return None()

        def describe(val: int?) -> str:
            match val:
                case Some(v):
                    if v > 100:
                        return f"big value: {v}"
                    return f"value: {v}"
                case None():
                    return "nothing"

        def main():
            fruits = ["apple", "banana", "cherry"]

            # Pattern matching on Optional
            result = find_item(fruits, "banana")
            match result:
                case Some(fruit):
                    print(f"Found: {fruit}")
                case None():
                    print("Not found")

            result2 = find_item(fruits, "grape")
            match result2:
                case Some(f):
                    print(f"Found: {f}")
                case None():
                    print("Not found")

            # Nested logic in match arms
            print(describe(Some(42)))
            print(describe(Some(200)))
            print(describe(None()))

            # unwrap helpers still work too
            some_val: int? = Some(10)
            print(f"Unwrapped: {some_val.unwrap()}")
            none_val: int? = None()
            print(f"Fallback: {none_val.unwrap_or(0)}")
        """;

    private const string ResultTypes = """
        def validate_age(age: int) -> int !str:
            if age < 0:
                return Err("Age cannot be negative")
            elif age > 150:
                return Err("Age seems unrealistic")
            return Ok(age)

        def classify(val: int !str) -> str:
            match val:
                case Ok(v):
                    if v >= 18:
                        return f"adult, age {v}"
                    return f"minor, age {v}"
                case Err(e):
                    return f"invalid: {e}"

        def main():
            # Pattern matching on Result
            ages = [25, -5, 200, 10]
            for age in ages:
                result = validate_age(age)
                match result:
                    case Ok(v):
                        print(f"Valid age: {v}")
                    case Err(e):
                        print(f"Error: {e}")

            # Nested logic in match arms
            print(classify(Ok(25)))
            print(classify(Ok(10)))
            print(classify(Err("bad input")))

            # try expression wraps exceptions in Result
            r1: int !Exception = try int("42")
            print(f"Parsed: {r1.unwrap_or(0)}")

            r2: int !Exception = try int("hello")
            print(f"Failed: {r2.unwrap_or(-1)}")
        """;

    private const string QuestionMarkOperator = """
        # The ? operator propagates errors upward — like Rust's ?.
        # It unwraps Result/Optional on success, or early-returns on failure.

        def parse_int(s: str) -> int !str:
            if s.isdigit():
                return Ok(int(s))
            return Err(f"'{s}' is not a number")

        def parse_point(input: str) -> tuple[int, int] !str:
            parts = input.split(",")
            if len(parts) != 2:
                return Err("expected 'x,y'")
            # Each ? propagates the Err upward if parsing fails
            x: int = parse_int(parts[0])?
            y: int = parse_int(parts[1])?
            return Ok((x, y))

        # ? works with Optional too
        def first_positive(items: list[int]) -> int?:
            for item in items:
                if item > 0:
                    return Some(item)
            return None()

        def double_first(items: list[int]) -> int?:
            # If first_positive returns None, we return None immediately
            val: int = first_positive(items)?
            return Some(val * 2)

        def main():
            # Result ? — success path
            r1 = parse_point("10,20")
            match r1:
                case Ok(point):
                    print(f"Point: {point}")
                case Err(e):
                    print(f"Error: {e}")

            # Result ? — error propagates from second ?
            r2 = parse_point("10,abc")
            match r2:
                case Ok(point):
                    print(f"Point: {point}")
                case Err(e):
                    print(f"Error: {e}")

            # Optional ? — success path
            result = double_first([0, -1, 5, 3])
            print(result.unwrap_or(0))

            # Optional ? — None propagation
            result2 = double_first([-1, -2, -3])
            print(result2.unwrap_or(0))
        """;

    private const string ListComprehensions = """
        def main():
            # Basic list comprehension
            squares = [x ** 2 for x in range(10)]
            print(f"Squares: {squares}")

            # Filtered comprehension
            evens = [x for x in range(20) if x % 2 == 0]
            print(f"Evens: {evens}")
        """;

    private const string PropertiesAndAccess = """
        class Temperature:
            __celsius: float

            def __init__(self, celsius: float):
                self.__celsius = celsius

            property get celsius(self) -> float:
                return self.__celsius

            property set celsius(self, value: float):
                if value < -273.15:
                    raise ValueError("Below absolute zero!")
                self.__celsius = value

            property get fahrenheit(self) -> float:
                return self.__celsius * 9.0 / 5.0 + 32.0

            def __str__(self) -> str:
                return f"{self.__celsius}C ({self.fahrenheit}F)"

        def main():
            temp = Temperature(100.0)
            print(temp)
            temp.celsius = 0.0
            print(temp)
        """;

    private const string OperatorOverloading = """
        class Vector:
            x: int
            y: int

            def __init__(self, x: int, y: int):
                self.x = x
                self.y = y

            def __add__(self, other: Vector) -> Vector:
                return Vector(self.x + other.x, self.y + other.y)

            def __sub__(self, other: Vector) -> Vector:
                return Vector(self.x - other.x, self.y - other.y)

            def __mul__(self, scalar: int) -> Vector:
                return Vector(self.x * scalar, self.y * scalar)

            def __neg__(self) -> Vector:
                return Vector(-self.x, -self.y)

            @override
            def __str__(self) -> str:
                return f"({self.x}, {self.y})"

        def main():
            a = Vector(5, 10)
            b = Vector(2, 3)
            print(a + b)
            print(a - b)
            print(a * 3)
            print(-a)
        """;

    private const string PipeOperator = """
        def double(x: int) -> int:
            return x * 2

        def add_one(x: int) -> int:
            return x + 1

        def to_str(x: int) -> str:
            return str(x)

        def format_with_prefix(text: str, prefix: str) -> str:
            return f"{prefix}: {text}"

        def main():
            # Basic pipe
            result = 5 |> double()
            print(result)

            # Chained pipes
            result2: str = 3 |> double() |> add_one() |> to_str()
            print(result2)

            # Pipe with extra arguments
            msg: str = "hello" |> format_with_prefix("MSG")
            print(msg)
        """;

    private const string Generators = """
        def count_up(n: int) -> int:
            i = 0
            while i < n:
                yield i
                i += 1

        def fibonacci(limit: int) -> int:
            a = 0
            b = 1
            while a < limit:
                yield a
                a, b = b, a + b

        def inner() -> int:
            yield 10
            yield 20

        def outer() -> int:
            yield from inner()
            yield 30

        def main():
            for x in count_up(5):
                print(x)

            for f in fibonacci(50):
                print(f)

            for x in outer():
                print(x)
        """;

    private const string PartialApplication = """
        def add(x: int, y: int) -> int:
            return x + y

        def clamp(value: float, minimum: float, maximum: float) -> float:
            if value < minimum:
                return minimum
            if value > maximum:
                return maximum
            return value

        def main():
            # Fix one argument
            add_five: (int) -> int = add(5, _)
            print(add_five(3))

            add_to_ten: (int) -> int = add(_, 10)
            print(add_to_ten(7))

            # Operator sections
            doubler: (int) -> int = (_ * 2)
            is_positive: (int) -> bool = (_ > 0)
            negate: (int) -> int = (-_)
            print(doubler(5))
            print(is_positive(-3))
            print(negate(7))

            # Practical: clamping function
            limit_0_100 = clamp(_, 0.0, 100.0)
            print(limit_0_100(150.0))
            print(limit_0_100(-50.0))
        """;

    private const string TryAndMaybeExpressions = """
        def parse_number(text: str) -> int !ValueError:
            return try[ValueError] int(text)

        class Config:
            value: str

            def __init__(self, value: str):
                self.value = value

            def get_value(self) -> str:
                return self.value

        def get_config() -> Config | None:
            return Config("production")

        def get_none_config() -> Config | None:
            return None

        def main():
            # try wraps exceptions into Result
            r1 = parse_number("42")
            print(r1.unwrap())

            r2 = parse_number("oops")
            print(r2.unwrap_or(-1))

            # maybe converts T|None to T? (Optional)
            cfg: Config? = maybe get_config()
            print(cfg?.get_value() ?? "fallback")

            cfg2: Config? = maybe get_none_config()
            print(cfg2?.get_value() ?? "fallback")
        """;

    private const string NullConditionalAndCoalescing = """
        class City:
            _name: str

            def __init__(self, name: str):
                self._name = name

            def get_name(self) -> str:
                return self._name

        class Address:
            _city: City?

            def __init__(self, city: City?):
                self._city = city

            def get_city(self) -> City?:
                return self._city

        def main():
            # Null-coalescing: provide defaults
            x: int? = None()
            print(x ?? 42)

            y: int? = Some(100)
            print(y ?? 0)

            # Chained coalescing
            first: int? = None()
            second: int? = None()
            print(first ?? second ?? 77)

            # Null-conditional: safe navigation
            addr = Address(Some(City("Tokyo")))
            name: str? = addr.get_city()?.get_name()
            print(name ?? "unknown")

            empty_addr = Address(None())
            name2: str? = empty_addr.get_city()?.get_name()
            print(name2 ?? "unknown")
        """;
}
