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
        ("List Comprehensions", ListComprehensions),
        ("Properties & Access Modifiers", PropertiesAndAccess),
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
}
