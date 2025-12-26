# Functions

Sharpy supports two keywords to declare functions: `def` and `fn`. You can use
either declaration with any function, including the `main()` function, but they
have different default behaviors, as described on this page.

Both `def` and `fn` have good use cases. Deciding which to use is a matter of
personal taste and which style best fits a given task.

:::note

Functions declared inside a struct are called "methods," but they have all the
same qualities as "functions" described here.

:::

## Anatomy of a function

Both `def` and `fn` function declarations have the same basic components (here
demonstrated with a `def` function):

<pre>
<strong>def</strong> <var>function_name</var><strong>(
&#8203;    </strong><var>arguments ...</var><strong>
) -&gt;</strong> <var>return_value_type</var>:
&#8203;    <var>function_body</var>
</pre>

Functions can have:

- Arguments: A function can optionally take one or more arguments.
- Return value: A function can optionally return a value.
- Function body: Statements that are executed when you call the function.
  Function definitions must include a body.

All of the optional parts of the function can be omitted, so the minimal
function is something like this:

```sharpy
def do_nothing():
    pass
```

The parentheses are always required.

Although you can't leave out the function body, you can use the `pass` statement
to define a function that does nothing.

### Arguments

Functions take arguments as inputs. Arguments are run-time values passed into
the function.

```sharpy
def add(a: int, b: int) -> int:
    return a + b
```

## `def` and `fn` comparison

Defining a function using `def` and `fn` have much in common. They both have the
following requirements:

* You must declare the type of each function argument.

* If a function doesn't return a value, you can either omit the return type or
  declare `None` as the return type.

  ```sharpy
  # The following function definitions are equivalent

  def greet(name: str):
    print("Hello,", name)

  def greet(name: str) -> None:
    print("Hello,", name)
  ```

* If the function returns a value, you must declare the return type using
  the <code><strong>-></strong> <var>type</var></code> syntax.

  ```sharpy
  def incr(a: int) -> int:
    return a + 1
  ```

Where `def` and `fn` differ is error handling.

* The compiler doesn't allow a function declared with `fn` to raise an error
  condition unless it explicitly includes a `raises` declaration. In contrast,
  the compiler assumes that *all* functions declared with `def` *might* raise an
  error.

As far as a function caller is concerned, there is no difference between
invoking a function declared with `def` vs a function declared with `fn`. You
could reimplement a `def` function as an `fn` function without making any
changes to code that calls the function.

## Function arguments

The rules for arguments described in this section apply to both `def`
and `fn` functions.

:::note Variadic arguments

An argument name prefixed by a single star character, like `*names`, identifies a
[variadic argument](#variadic-arguments) that accepts multiple positional values.

```sharpy
def myfunc(*names: str):
    pass
```

**Note:** Sharpy does not support Python's `**kwargs` (variadic keyword arguments),
positional-only parameters (`/`), or keyword-only parameter markers (`*`). See
[Unsupported Python parameter features](#unsupported-python-parameter-features)
for details.

:::

### Optional arguments

An optional argument is one that includes a default value, such as the `exp`
argument here:

```sharpy
fn my_pow(base: Int, exp: Int = 2) -> Int:
    return base ** exp

fn use_defaults():
    # Uses the default value for `exp`
    z = my_pow(3)
    print(z)
```

However, you can't define a default value for an argument that's declared with
the [`mut`](/sharpy/manual/values/ownership#mutable-arguments-mut) argument
convention.

Any optional arguments must appear after any required arguments.

### Keyword arguments

You can also use keyword arguments when calling a function. Keyword arguments
are specified using the format
<code><var>argument_name</var> = <var>argument_value</var></code>.
You can pass keyword arguments in any order:

```sharpy
fn my_pow(base: Int, exp: Int = 2) -> Int:
    return base ** exp

fn use_keywords():
    # Uses keyword argument names (with order reversed)
    z = my_pow(exp=3, base=2)
    print(z)
```

### Variadic arguments

Variadic arguments let a function accept a variable number of arguments. To
define a function that takes a variadic argument, use the variadic argument
syntax <code>*<var>argument_name</var></code>:

```sharpy
def sum(*values: int) -> int:
  total: int = 0
  for value in values:
    total = total + value
  return total
```

The variadic argument `values` here is a placeholder that accepts any number of
passed positional arguments. All variadic arguments must be the same type.

You can define zero or more arguments before the variadic argument. The variadic
argument must be the last parameter in the function signature.

### Unsupported Python parameter features

Sharpy does not support the following Python parameter features because C# has
no direct equivalent and there is no zero-cost way to implement them:

**Positional-only parameters (`/`):** In Python, parameters before `/` can only
be passed positionally. C# does not support restricting parameters to
positional-only—callers can always use named arguments.

```sharpy
# ❌ Not supported in Sharpy
fn min(a: Int, b: Int, /) -> Int:
    return a if a < b else b
```

**Keyword-only parameter marker (`*`):** In Python, a bare `*` forces all
following parameters to be keyword-only. C# does not support this restriction.

```sharpy
# ❌ Not supported in Sharpy
fn configure(host: str, *, port: int, debug: bool) -> None:
    pass
```

**Variadic keyword arguments (`**kwargs`):** Python's `**kwargs` accepts
arbitrary keyword arguments as a dictionary. This cannot be statically typed.

```sharpy
# ❌ Not supported in Sharpy
def configure(**kwargs):
    pass
```

**Alternatives:** Use explicit named parameters with default values, or pass a
typed configuration object. See
[Function Parameters](/sharpy/language_specification/function_parameters) for
details.

In Sharpy, all parameters can be passed either positionally or by name—the
choice is the caller's.

## Overloaded functions

All function declarations must specify argument types, so if you want a
want a function to work with different data types, you need to implement
separate versions of the function that each specify different argument types.
This is called "overloading" a function.

For example, here's an overloaded `add()` function that can accept either
`Int` or `str` types:

```sharpy
fn add(x: Int, y: Int) -> Int:
    return x + y

fn add(x: str, y: str) -> str:
    return x + y
```

If you pass anything other than `Int` or `str` to the `add()` function,
you'll get a compiler error. That is, unless `Int` or `str` can implicitly
cast the type into their own type. For example, `str` includes an overloaded
version of its constructor (`__init__()`) that supports
[implicit conversion](/sharpy/manual/lifecycle/life#constructors-and-implicit-conversion)
from a `strLiteral` value. Thus, you can also pass a `strLiteral` to a
function that expects a `str`.

When resolving an overloaded function call, the Sharpy compiler tries each
candidate function and uses the one that works (if only one version works), or
it picks the closest match (if it can determine a close match), or it reports
that the call is ambiguous (if it can't figure out which one to pick). For
details on how Sharpy picks the best candidate, see
[Overload resolution](#overload-resolution).

If the compiler can't figure out which function to use, you can resolve the
ambiguity by explicitly casting your value to a supported argument type. For
example, the following code calls the overloaded `foo()` function,
but both implementations accept an argument that supports [implicit
conversion](/sharpy/manual/lifecycle/life#constructors-and-implicit-conversion)
from `strLiteral`. So, the call to `foo(string)` is ambiguous and creates a
compiler error. You can fix this by casting the value to the type you really
want:

```sharpy
struct Mystr:
    @implicit
    fn __init__(out self, string: strLiteral):
        pass

fn foo(name: str):
    print("str")

fn foo(name: Mystr):
    print("Mystr")

fn call_foo():
    alias string: strLiteral = "Hello"
    # foo(string) # error: ambiguous call to 'foo' ... This call is ambiguous because two `foo` functions match it
    foo(Mystr(string))
```

Overloading also works with combinations of both `fn` and `def` function
declarations.

### Overload resolution

When resolving an overloaded function, Sharpy does not consider the return type
or other contextual information at the call site—it considers only argument
types and whether the functions are instance methods or static methods.

The overload resolution logic filters for candidates according to the following
rules, in order of precedence:

1. Candidates with parameter names matching all named arguments in the call.
2. Candidates requiring the smallest number of implicit conversions.
3. Candidates without variadic arguments.
4. Candidates with the shortest argument signature.
5. Non-`@staticmethod` candidates (over `@staticmethod` ones, if available).

If there is more than one candidate after applying these rules, the overload
resolution fails.

#### Named arguments in overload resolution

Named arguments participate in overload resolution by filtering which overloads
are candidates. An overload is only considered if it has a parameter matching
each named argument's name:

```sharpy
fn do_work(num: Int, message: str = "Hello"):
    print(f"{num}: {message}")

fn do_work(count: Int):
    print(f"Count: {count}")

fn main():
    do_work(21)         # Calls do_work(count) - prefers no optional params
    do_work(num=21)     # Calls do_work(num, message) - only overload with 'num'
    do_work(count=21)   # Calls do_work(count) - only overload with 'count'
```

This allows named arguments to disambiguate between overloads that have the
same parameter types but different parameter names.

## Return values

Return value types are declared in the signature using the
<code><strong>-></strong> <var>type</var></code> syntax. Values are
passed using the `return` keyword, which ends the function and returns the
identified value (if any) to the caller.

```sharpy
def get_greeting() -> str:
    return "Hello"
```

By default, the value is returned to the caller as an owned value. As with
arguments, a return value may be implicitly converted to the named return type.
For example, the previous example calls `return` with a string literal,
`"Hello"`, which is implicitly converted to a `str`.

## Raising and non-raising functions

By default, when a function raises an error, the function terminates immediately
and the error propagates to the calling function. If the calling function
doesn't handle the error, it continues to propagate up the call stack.

```sharpy
def raises_error():
    raise Error("There was an error.")
```

The Sharpy compiler *always* treats a function declared with `def` as a *raising
function*, even if the body of the function doesn't contain any code that could
raise an error.

Functions declared with `fn` without the `raises` keyword are *non-raising
functions*—that is, they are not allowed to propagate an error to the calling
function. If a non-raising function calls a raising function, it **must handle
any possible errors.**

```sharpy
# This function will not compile
fn unhandled_error():
    raises_error()   # Error: can't call raising function in a non-raising context

# Explicitly handle the error
fn handle_error():
    try:
        raises_error()
    except e:
        print("Handled an error:", e)

# Explicitly propagate the error
fn propagate_error() raises:
    raises_error()

```

If you're writing code that you expect to use widely or distribute as a package,
you may want to use `fn` functions for APIs that don't raise errors to limit
the number of places users need to add unnecessary error handling code. For some
extremely performance-sensitive code, it may be preferable to avoid run-time
error-handling.

For more information, see
[Errors, error handling, and context managers](/sharpy/manual/errors).
