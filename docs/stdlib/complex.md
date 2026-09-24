# complex

A complex number type, similar to Python's complex.

## Properties

| Name | Type | Description |
|------|------|-------------|
| `real` | `float` | The real part of the complex number. |
| `imag` | `float` | The imaginary part of the complex number. |

## Methods

### `conjugate() -> Complex`

Return the complex conjugate.

### `__str__() -> str`

`repr()` uses the same method. Render in CPython's `complex` form: `(4+1j)`, `1j`, `(-1-2j)`.

!!! note
    Every rule below was measured against python3 3.12 rather than inferred, because the
    shape is less regular than it looks:
    
    
    A real part of POSITIVE zero drops the parentheses and the real term entirely —
    `complex(0, 1)` is `1j`, not `(0+1j)`, and `complex(0, 0)` is
    `0j`. NEGATIVE zero does not: `complex(-0.0, 1.0)` is `(-0+1j)`. So the
    test is on the sign bit, not on `== 0`.
    
    
    The imaginary sign comes from ITS sign bit too, so `complex(-0.0, -0.0)` is
    `(-0-0j)` while `complex(1, nan)` is `(1+nanj)`.
    
    
    Components print SHORTEST-ROUND-TRIP WITHOUT a trailing `.0`: `complex(4.0, 1.0)`
    is `(4+1j)` even though `repr(4.0)` is `4.0`. Precision is not otherwise
    reduced — `complex(1,2)/complex(3,-1)` is `(0.1+0.7000000000000001j)`, which is
    the cell a naive formatter gets wrong.

### `__format__(format_spec: str) -> str`

Python's `complex.__format__` — the CLR spelling of `__format__` (#2018):
`format(c, spec)`, rendered by `PyFormat.Apply(object, string)`.
