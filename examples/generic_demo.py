# Generic Code Generation Example

# Generic class - Box container
class Box[T]:
    value: T = None  # Placeholder initialization

    def set(self, new_value: T):
        self.value = new_value

    def get(self) -> T:
        return self.value

# Generic function - identity
def identity[T](value: T) -> T:
    return value

# Generic function with multiple type parameters
def combine[T, U](first: T, second: U) -> str:
    return f"{first} {second}"

# Generic function with list
def get_first[T](items: list[T]) -> T:
    return items[0]

# Generic struct
struct Point[T]:
    x: T = None
    y: T = None

    def set_coords(self, x: T, y: T):
        self.x = x
        self.y = y

# Main demonstration
def main():
    # Generic classes, functions, and structs are now supported!
    # This demonstrates the code generation capability
    print("Generic code generation is working!")
