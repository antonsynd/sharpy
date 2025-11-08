# Errors and error handling

This page discusses how to raise errors in Sharpy programs and how to detect and
handle error conditions.

## Raise an error

The `raise` statement raises an error condition in your program. You provide the
`raise` statement with an error message as a string. For example:

```sharpy
raise "integer overflow"
```

Currently, Sharpy raises all errors as instances of the standard .NET
`System.Exception` type.

An error interrupts the execution flow of your program. If you provide
an error handler (as described in [Handle an error](#handle-an-error)) in the
current function, execution resumes with that handler. If the error isn't
handled in the current function, it propagates to the calling function and so
on. If an error isn't caught by any error handler, your program terminates with
a non-zero exit code and prints the error message.

## Declare a raising function

A function defined using the `fn` keyword is *non-raising* by default. If it
can raise an error, you must include the `raises` keyword in the function
definition. For example:

```sharpy
def incr(n: int) raises -> int:
    if n == int.MAX:
        raise "incr: integer overflow"
    else:
        return n + 1
```

If you don't include the `raises` keyword on an `fn` function,
then the function must explicitly handle any errors that might occur in the code
it executes.

In contrast, a `def` function is *raising* by default. So the following
`incr()` function is equivalent to the `incr()` function defined above with
`fn`:

```sharpy
def incr(n: int) -> int:
    if n == int.MAX:
        raise "incr: integer overflow"
    else:
        return n + 1
```

## Handle an error

Sharpy allows you to detect and handle error conditions using the `try-except`
control flow structure. The full syntax is:

```sharpy
try:
    # Code block to execute that might raise an error
except:
    # Code block to execute if an error occurs
else:
    # Code block to execute if no error occurs
finally:
    # Final code block to execute in all circumstances
```

You must include one or both of the `except` and `finally` clauses. The `else`
clause is optional.

The `try` clause contains a code block to execute that might raise an error. If
no error occurs, the entire code block executes. If an error occurs, execution
of the code block stops at the point that the error is raised. Your program then
continues with the execution of the `except` clause, if provided, or the
`finally` clause.

If the `except` clause is present, its code block executes only if an error
occurred in the `try` clause. The `except` clause "consumes" the error that
occurred in the `try` clause. You can then implement any error handling or
recovery that's appropriate for your application.

You can re-raise an error condition from your `except` clause simply
by executing a `raise` statement from within its code block.

If the `else` clause is present, its code block executes only if an error does
not occur in the `try` clause. Note that the `else` clause is *skipped* if the
`try` clause executes a `continue`, `break`, or `return` that exits from the
`try` block.

If the `finally` clause is present, its code block executes after the `try`
clause and the `except` or `else` clause, if applicable. The `finally` clause
executes even if one of the other code blocks exits by executing a `continue`,
`break`, or `return` statement or by raising an error. The `finally` clause is
often used to release resources used by the `try` clause (such as a file handle)
regardless of whether an error occurred.

As an example, consider the following program:

```sharpy
def incr(n: int) -> int:
    if n == int.MAX:
        raise "incr: integer overflow"
    else:
        return n + 1

def main():
    for value in [0, 1, int.MAX]:
        try:
            print()
            print("try     =>", value)
            if value == 1:
                continue
            result = "{} incremented is {}".format(value, incr(value))
        except:
            print("except  => error occurred")
        else:
            print("else    =>", result)
        finally:
            print("finally => ====================")
```
