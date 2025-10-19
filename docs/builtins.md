# Builtins

Following https://docs.python.org/3/library/stdtypes.html

# Numeric types

## Integers

* int (32-bit)
* uint (32-bit)
* byte (8-bit)
* sbyte (8-bit)
* short (16-bit)
* ushort (16-bit)
* long (64-bit)
* ulong (64-bit)

## Floating point numbers

* float (32-bit)
* double (64-bit)
* decimal (128-bit)

## Complex numbers (`complex`)

This can be implemented as either an alias of `System.Numerics.Complex` with
some compiler intrinsics added to mirror the Pythonic API, or a wrapper struct
with implicit bidi conversion.

In both cases, this struct is readonly.

### Properties

These properties are "native" to Sharpy.

| Name | Type | C# | Notes |
| - | - | - | - |
| `imag: double` | `double Imaginary` | Compiler intrinsic alias to `imaginary` |
| `real: double` | `double Real` | - |

These properties are inherited through .NET.

| Name | C# | Notes |
| - | - | - |
| `imaginary: double` | `double Imaginary` | See `self.imag` |
| `magnitude: double` | `double Magnitude` | - |
| `phase: double` | `double Phase` | - |

### Methods

The .NET static method `Complex.Conjugate()` can be used to implement the
member method intrinsic to the compiler as a "native" Sharpy method.

| Name | C# | Notes |
| - | - | - |
| `def conjugate(self) -> complex` | `static Complex Conjugate(this)` | - |

The other .NET static methods can be transformed into member methods intrinsic
to the compiler, where appropriate (i.e. the first argument is the complex
number itself).
