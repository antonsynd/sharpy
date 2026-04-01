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

        def main():
            fruits = ["apple", "banana", "cherry"]

            result = find_item(fruits, "banana")
            print(f"Found: {result.unwrap()}")

            result2 = find_item(fruits, "grape")
            print(f"Fallback: {result2.unwrap_or('nothing')}")

            some_val: int? = Some(42)
            print(f"Value: {some_val.unwrap()}")

            no_val: int? = None()
            print(f"Default: {no_val.unwrap_or(0)}")
        """;

    private const string ResultTypes = """
        def validate_age(age: int) -> int !str:
            if age < 0:
                return Err("Age cannot be negative")
            elif age > 150:
                return Err("Age seems unrealistic")
            return Ok(age)

        def main():
            # Ok path
            r1 = validate_age(25)
            print(f"Valid age: {r1.unwrap_or(-1)}")

            # Error paths
            r2 = validate_age(-5)
            print(f"Negative: {r2.unwrap_or(-1)}")

            r3 = validate_age(200)
            print(f"Too high: {r3.unwrap_or(-1)}")

            # try expression wraps exceptions in Result
            r4: int !Exception = try int("42")
            print(f"Parsed: {r4.unwrap_or(0)}")

            r5: int !Exception = try int("hello")
            print(f"Failed: {r5.unwrap_or(-1)}")
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
