# Control flow

Sharpy includes several traditional control flow structures for conditional and
repeated execution of code blocks.

## The `if` statement

Sharpy supports the `if` statement for conditional code execution. With it you can
conditionally execute an indented code block if a given boolean expression
evaluates to `True`.

```sharpy
temp_celsius = 25
if temp_celsius > 20:
    print("It is warm.")
    print("The temperature is", temp_celsius * 9 / 5 + 32, "Fahrenheit." )
```

```output
It is warm.
The temperature is 77.0 Fahrenheit.
```

You can write the entire `if` statement as a single line if all you need to
execute conditionally is a single, short statement.

```sharpy
temp_celsius = 22
if temp_celsius < 15: print("It is cool.") # Skipped because condition is False
if temp_celsius > 20: print("It is warm.")
```

```output
It is warm.
```

Optionally, an `if` statement can include any number of additional `elif`
clauses, each specifying a boolean condition and associated code block to
execute if `True`. The conditions are tested in the order given. When a
condition evaluates to `True`, the associated code block is executed and no
further conditions are tested.

Additionally, an `if` statement can include an optional `else` clause providing
a code block to execute if all conditions evaluate to `False`.

```sharpy
temp_celsius = 25
if temp_celsius <= 0:
    print("It is freezing.")
elif temp_celsius < 20:
    print("It is cool.")
elif temp_celsius < 30:
    print("It is warm.")
else:
    print("It is hot.")
```

```output
It is warm.
```

:::note

Sharpy currently does not support pattern matching or switch statements.

:::

### Short-circuit evaluation

Sharpy follows [short-circuit evaluation](https://en.wikipedia.org/wiki/Short-circuit_evaluation)
semantics for boolean operators. If the first argument to an `or` operator
evaluates to `True`, the second argument is not evaluated.

```sharpy
def true_func() -> Bool:
    print("Executing true_func")
    return True

def false_func() -> Bool:
    print("Executing false_func")
    return False

print('Short-circuit "or" evaluation')
if true_func() or false_func():
    print("True result")
```

```output
Short-circuit "or" evaluation
Executing true_func
True result
```

If the first argument to an `and` operator evaluates to `False`, the second
argument is not evaluated.

```sharpy
print('Short-circuit "and" evaluation')
if false_func() and true_func():
    print("True result")
```

```output
Short-circuit "and" evaluation
Executing false_func
```

### Conditional expressions

Sharpy also supports conditional expressions (or what is sometimes called a
[*ternary conditional operator*](https://en.wikipedia.org/wiki/Ternary_conditional_operator))
using the syntax<code><var>true_result</var> if <var>boolean_expression</var> else <var>false_result</var></code>, just as
in Python. This is most often used as a concise way to assign one of two
different values to a variable, based on a boolean condition.

```sharpy
temp_celsius = 15
forecast = "warm" if temp_celsius > 20 else "cool"
print("The forecast for today is", forecast)
```

```output
The forecast for today is cool
```

The alternative, written as a multi-line `if` statement, is more verbose.

```sharpy
if temp_celsius > 20:
    forecast = "warm"
else:
    forecast = "cool"
print("The forecast for today is", forecast)
```

```output
The forecast for today is cool
```

## The `while` statement

The `while` loop repeatedly executes a code block while a given boolean
expression evaluates to `True`. For example, the following loop prints values
from the Fibonacci series that are less than 50.

```sharpy
fib_prev = 0
fib_curr = 1

print(fib_prev, end="")
while fib_curr < 50:
    print(",", fib_curr, end="")
    fib_prev, fib_curr = fib_curr, fib_prev + fib_curr
```

```output
0, 1, 1, 2, 3, 5, 8, 13, 21, 34
```

A `continue` statement skips execution of the rest of the code block and
resumes with the loop test expression.

```sharpy
n = 0
while n < 5:
    n += 1
    if n == 3:
        continue
    print(n, end=", ")
```

```output
1, 2, 4, 5,
```

A `break` statement terminates execution of the loop.

```sharpy
n = 0
while n < 5:
    n += 1
    if n == 3:
        break
    print(n, end=", ")
```

```output
1, 2,
```

Optionally, a `while` loop can include an `else` clause. The body of the `else`
clause executes when the loop's boolean condition evaluates to `False`, even if
it occurs the first time tested.

```sharpy
n = 5

while n < 4:
    print(n)
    n += 1
else:
    print("Loop completed")

```

```output
Loop completed
```

:::note

The `else` clause does *not* execute if a `break` or `return` statement
exits the `while` loop.

:::

```sharpy
n = 0
while n < 5:
    n += 1
    if n == 3:
        break
    print(n)
else:
    print("Executing else clause")
```

```output
1
2
```

## The `for` statement

The `for` loop iterates over a sequence, executing a code block for each
element in the sequence.

### Iterating over Sharpy collections

Sharpy collection types like `list`, `dict`, and `set` support `for` loop
iteration.

The following shows an example of iterating over a Sharpy list.

```sharpy
states = ["California", "Hawaii", "Oregon"]
for state in states:
    print(state)
```

```output
California
Hawaii
Oregon
```

You can iterate over a dictionary to get its keys:

```sharpy
capitals: dict[str, str] = {
    "California": "Sacramento",
    "Hawaii": "Honolulu",
    "Oregon": "Salem"
}

for state in capitals:
    print(capitals[state] + ", " + state)
```

```output
Sacramento, California
Honolulu, Hawaii
Salem, Oregon
```

### Iterating ranges

Sharpy provides the `range()` function to generate a sequence of integers.
For example:

```sharpy
for i in range(5):
    print(i, end=", ")
```

```output
0, 1, 2, 3, 4,
```

### `for` loop control statements

A `continue` statement skips execution of the rest of the code block and
resumes the loop with the next element of the collection.

```sharpy
for i in range(5):
    if i == 3:
        continue
    print(i, end=", ")
```

```output
0, 1, 2, 4,
```

A `break` statement terminates execution of the loop.

```sharpy
for i in range(5):
    if i == 3:
        break
    print(i, end=", ")
```

```output
0, 1, 2,
```

Optionally, a `for` loop can include an `else` clause. The body of the `else`
clause executes after iterating over all of the elements in a collection.

```sharpy
for i in range(5):
    print(i, end=", ")
else:
    print("\nFinished executing 'for' loop")
```

```output
0, 1, 2, 3, 4,
Finished executing 'for' loop
```

The `else` clause executes even if the collection is empty.

```sharpy
empty = []
for i in empty:
    print(i)
else:
    print("Finished executing 'for' loop")
```

```output
Finished executing 'for' loop
```

:::note

The `else` clause does *not* execute if a `break` or `return` statement
terminates the `for` loop.

:::

```sharpy
animals = ["cat", "aardvark", "hippopotamus", "dog"]
for animal in animals:
    if animal == "dog":
        print("Found a dog")
        break
else:
    print("No dog found")
```

```output
Found a dog
```

### Iterating over Python collections

The Sharpy `for` loop supports iterating over Python collection types. Each item
retrieved by the loop is a
[`PythonObject`](/sharpy/stdlib/python/python_object/PythonObject) wrapper around
the Python object. Refer to the [Python types](/sharpy/manual/python/types)
documentation for more information on manipulating Python objects from Sharpy.

The following is a simple example of iterating over a mixed-type Python list.

```sharpy
from python import Python

def main():
    # Create a mixed-type Python list
    py_list = Python.list(42, "cat", 3.14159)
    for py_obj in py_list:  # Each element is of type "PythonObject"
        print(py_obj)
```

```output
42
cat
3.14159
```

There are two techniques for iterating over a Python dictionary. The first is to
iterate directly using the dictionary, which produces a sequence of its keys.

```sharpy
from python import Python

def main():
    # Create a mixed-type Python dictionary
    py_dict = Python.evaluate("{'a': 1, 'b': 2.71828, 'c': 'sushi'}")
    for py_key in py_dict:  # Each key is of type "PythonObject"
        print(py_key, py_dict[py_key])
```

```output
a 1
b 2.71828
c sushi
```

The second approach to iterating over a Python dictionary is to invoke its
`items()` method, which produces a sequence of 2-tuple objects.
Within the loop body, you can then access the key and value by index.

```sharpy
from python import Python

def main():
    # Create a mixed-type Python dictionary
    py_dict = Python.evaluate("{'a': 1, 'b': 2.71828, 'c': 'sushi'}")
    for py_tuple in py_dict.items():  # Each 2-tuple is of type "PythonObject"
        print(py_tuple[0], py_tuple[1])
```

```output
a 1
b 2.71828
c sushi
```
