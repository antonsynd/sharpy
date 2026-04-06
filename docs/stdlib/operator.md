# operator

Operator module — functions corresponding to the intrinsic operators of Python.

```python
import operator
```

## Functions

### `operator.abs(x: decimal) -> decimal`

Return the absolute value of x (decimal).

### `operator.abs(x: float) -> float`

Return the absolute value of x (double).

### `operator.abs(x: int) -> int`

Return the absolute value of x (int).

### `operator.abs(x: long) -> long`

Return the absolute value of x (long).

### `operator.abs(x: short) -> short`

Return the absolute value of x (short).

### `operator.abs(x: float32) -> float32`

Return the absolute value of x (float).

### `operator.abs(x: sbyte) -> sbyte`

Return the absolute value of x (sbyte).

### `operator.add(left: int, right: int) -> int`

Return left + right for int operands.

### `operator.add(left: long, right: long) -> long`

Return left + right for long operands.

### `operator.add(left: float32, right: float32) -> float32`

Return left + right for float operands.

### `operator.add(left: float, right: float) -> float`

Return left + right for double operands.

### `operator.add(left: decimal, right: decimal) -> decimal`

Return left + right for decimal operands.

### `operator.add(left: str, right: str) -> str`

Return left + right (concatenation) for string operands.

### `operator.eq(left: IComparable[T], right: T) -> bool`

Return true if left == right using IComparable.

### `operator.eq(left: IComparable, right: object) -> bool`

Return true if left == right using IComparable.

### `operator.eq(left: object, right: object) -> bool`

Return true if left == right using Equals.

### `operator.ge(left: IComparable[T], right: T) -> bool`

Return true if left >= right using IComparable.

### `operator.ge(left: IComparable, right: object) -> bool`

Return true if left >= right using IComparable.

### `operator.ge(left: T, right: T) -> bool`

Return true if left >= right with automatic dispatch.

### `operator.gt(left: IComparable[T], right: T) -> bool`

Return true if left > right using IComparable.

### `operator.gt(left: IComparable, right: object) -> bool`

Return true if left > right using IComparable.

### `operator.gt(left: T, right: T) -> bool`

Return true if left > right with automatic dispatch.

### `operator.i_add(left: ref int, right: int)`

In-place addition: left += right (int).

### `operator.i_add(left: ref long, right: long)`

In-place addition: left += right (long).

### `operator.i_add(left: ref float, right: float32)`

In-place addition: left += right (float).

### `operator.i_add(left: ref double, right: float)`

In-place addition: left += right (double).

### `operator.i_add(left: ref decimal, right: decimal)`

In-place addition: left += right (decimal).

### `operator.i_mul(left: ref int, right: int)`

In-place multiplication: left *= right (int).

### `operator.i_mul(left: ref long, right: long)`

In-place multiplication: left *= right (long).

### `operator.i_mul(left: ref float, right: float32)`

In-place multiplication: left *= right (float).

### `operator.i_mul(left: ref double, right: float)`

In-place multiplication: left *= right (double).

### `operator.i_mul(left: ref decimal, right: decimal)`

In-place multiplication: left *= right (decimal).

### `operator.is(left: object, right: object) -> bool`

Return true if left and right are the same object (identity check).

### `operator.is_not(left: object, right: object) -> bool`

Return true if left and right are not the same object (identity check).

### `operator.le(left: IComparable[T], right: T) -> bool`

Return true if left .

### `operator.le(left: IComparable, right: object) -> bool`

Return true if left <= right using IComparable.

### `operator.le(left: T, right: T) -> bool`

Return true if left <= right with automatic dispatch.

### `operator.lt(left: IComparable[T], right: T) -> bool`

Return true if left .

### `operator.lt(left: IComparable, right: object) -> bool`

Return true if left < right using IComparable.

### `operator.lt(left: T, right: T) -> bool`

Return true if left < right with automatic dispatch.

### `operator.mul(left: int, right: int) -> int`

Return left * right for int operands.

### `operator.mul(left: long, right: long) -> long`

Return left * right for long operands.

### `operator.mul(left: float32, right: float32) -> float32`

Return left * right for float operands.

### `operator.mul(left: float, right: float) -> float`

Return left * right for double operands.

### `operator.mul(left: decimal, right: decimal) -> decimal`

Return left * right for decimal operands.

### `operator.ne(left: IComparable[T], right: T) -> bool`

Return true if left != right using IComparable.

### `operator.ne(left: IComparable, right: object) -> bool`

Return true if left != right using IComparable.

### `operator.ne(left: object, right: object) -> bool`

Return true if left != right using Equals.

### `operator.not(value: bool) -> bool`

Return the logical negation of a boolean value.

### `operator.not(collection: System.Collections.ICollection) -> bool`

Return true if the collection is empty.

### `operator.not(collection: ICollection[T]) -> bool`

Return true if the collection is empty.

### `operator.truth(value: bool) -> bool`

Return the truth value of a boolean.

### `operator.truth(collection: System.Collections.ICollection) -> bool`

Return true if the collection is non-empty.

### `operator.truth(collection: ICollection[T]) -> bool`

Return true if the collection is non-empty.
