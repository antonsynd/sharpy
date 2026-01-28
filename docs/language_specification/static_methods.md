# Static Methods

Unlike Python's `@staticmethod` and C# `static`, Sharpy does not require an annotation/decorator/keyword for static methods. Static methods on a class, struct, etc. are like regular methods, except they do not have a `self` parameter (which has no type annotation) as the first parameter. This is how the compiler distinguishes between static methods and instance methods: instance methods always have an initial `self` parameter with no type annotation.

It is however, purely optional to use Sharpy's decorator on static methods, `@static`. Using `@static`
on a method with `self` as the first parameter is a compile-time error as only instance methods
have `self` as the first parameter.

```python
struct Foo:
    x: int
    y: int

    # This is a static method, C# code emission results in `static` keyword being added
    def name() -> str:
        return "Foo"

    # Also valid to use @static, but completely optional
    @static
    def reversed_name() -> str:
        return "ooF"

    # This is an instance method
    def sum(self) -> int:
        return self.x + self.y

    # WRONG: This is a compile-time error as this method is
    # an instance method due to the presence of `self`
    # as the first parameter
    @static
    def negative_sum(self) -> int:
        return -(self.x + self.y)
```

Note that interfaces cannot have static methods; all interfaces methods in the current version of Sharpy are instance methods.

*Implementation*
- *🔄 Lowered - Static methods emit `static` keyword in C#*
