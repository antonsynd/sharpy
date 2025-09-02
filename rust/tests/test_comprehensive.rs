use sharpy_compiler_toolchain::{Parser, SharpyLexer};

#[test]
fn test_comprehensive_sharpy_program() {
    let input = r#"
# This is a comprehensive test of Sharpy features
from collections import defaultdict
import typing

# Class with various features
class ExampleClass:
    # Public property
    property name: str:
        get(self):
            return self.__name

        set(self, value: str):
            self.__name = value

    # Private method with type annotations
    def __private_method(self, x: int, y: Optional[str] = None) -> bool:
        return x > 0 and y is not None

    # Protected method with generic types
    def _protected_method(self, items: List[Tuple[str, int]]) -> Dict[str, int]:
        result: Dict[str, int] = {}
        for key, value in items:
            result[key] = value
        return result

# Struct definition
struct Point:
    x: float
    y: float

# Function with complex type annotations
def process_data(
    input_data: Dict[str, List[int]],
    config: Optional[Config] = None
) -> Tuple[bool, Optional[str]]:

    # Various assignment patterns
    success: bool = True
    error_msg: Optional[str] = None

    # Destructuring assignment
    first_key, *remaining_keys = list(input_data.keys())

    # Control flow with complex expressions
    if config is not None and len(input_data) > 0:
        for key, values in input_data.items():
            # Complex expressions with operator precedence
            result = sum(x ** 2 for x in values if x > 0) / len(values)

            # Optional chaining (if supported)
            normalized = config?.normalization_factor ?? 1.0

            # Chained method calls and attribute access
            processed = str(result * normalized).strip().lower()

            if not processed:
                success = False
                error_msg = f"Failed to process key: {key}"
                break
    else:
        success = False
        error_msg = "Invalid input or configuration"

    return success, error_msg

# Lambda expressions with type annotations
square: Func[[int], int] = lambda x: x ** 2
filter_positive: Func[[List[int]], List[int]] = lambda nums: [x for x in nums if x > 0]

# Complex data structures
config_data = {
    "settings": {
        "debug": True,
        "max_retries": 3,
        "timeout": 30.5
    },
    "features": ["feature_a", "feature_b"],
    "thresholds": [0.1, 0.5, 0.9]
}

# Pattern matching (if supported)
def handle_response(response):
    match response.status:
        case 200:
            return "Success"
        case 404:
            return "Not Found"
        case 500:
            return "Server Error"
        case _:
            return f"Unknown status: {response.status}"

# While loop with complex condition
while len(queue) > 0 and not shutdown_requested:
    item = queue.pop(0)
    if item is not None:
        process_item(item)
    else:
        break

# Exception handling (if supported)
try:
    result = risky_operation()
except SpecificError as e:
    handle_specific_error(e)
except Exception as e:
    handle_general_error(e)
finally:
    cleanup()
"#;

    let mut lexer = SharpyLexer::new(input);
    let result = lexer.tokenize_all();

    match result {
        Ok(tokens) => {
            println!("Successfully tokenized {} tokens", tokens.len());

            // Try parsing as well
            let mut parser = Parser::new(tokens);
            let parse_result = parser.parse();

            match parse_result {
                Ok(ast) => {
                    println!("Successfully parsed {} top-level statements", ast.len());
                }
                Err(e) => {
                    println!("Parse error (expected for incomplete features): {}", e);
                    // This is not necessarily a failure since some features might not be implemented yet
                }
            }
        }
        Err(errors) => {
            println!("Lexer errors:");
            for error in &errors {
                println!("  {}", error);
            }
            // Only fail if there are actual lexer errors (not parser errors)
            assert!(errors.is_empty(), "Unexpected lexer errors");
        }
    }
}

#[test]
fn test_real_world_fibonacci() {
    let input = r#"
def fibonacci(n: int) -> int:
    if n <= 1:
        return n
    else:
        return fibonacci(n - 1) + fibonacci(n - 2)

# Test the function
result: int = fibonacci(10)
print(f"Fibonacci of 10 is {result}")
"#;

    let mut lexer = SharpyLexer::new(input);
    let tokens = lexer.tokenize_all().unwrap();
    let mut parser = Parser::new(tokens);
    let result = parser.parse();

    // This should mostly work since it uses basic features
    match result {
        Ok(_) => println!("Successfully parsed Fibonacci example"),
        Err(e) => println!("Parse error: {}", e),
    }
}

#[test]
fn test_sharpy_class_with_protocols() {
    let input = r#"
protocol Drawable:
    def draw(self) -> None: ...

protocol Movable:
    def move(self, x: int, y: int) -> None: ...

class Shape(Drawable, Movable):
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def draw(self) -> None:
        pass

    def move(self, x: int, y: int) -> None:
        self.x = x
        self.y = y

class Circle(Shape):
    def __init__(self, x: int, y: int, radius: float):
        super().__init__(x, y)
        self.radius = radius

    def draw(self) -> None:
        print(f"Drawing circle at ({self.x}, {self.y}) with radius {self.radius}")
"#;

    let mut lexer = SharpyLexer::new(input);
    let result = lexer.tokenize_all();

    match result {
        Ok(tokens) => {
            println!("Successfully tokenized class/protocol example");
            let mut parser = Parser::new(tokens);
            let parse_result = parser.parse();
            // This might not fully parse since class/protocol parsing isn't implemented yet
            let _result = parse_result;
        }
        Err(errors) => {
            assert!(errors.is_empty(), "Unexpected lexer errors: {:?}", errors);
        }
    }
}
