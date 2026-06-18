# threading

Thread-based concurrency primitives.

```python
import threading
```

## Functions

### `threading.current_thread() -> Thread`

### `threading.active_count() -> int`

### `threading.main_thread() -> Thread`

### `threading.enumerate() -> list[Thread]`

### `threading.lock() -> Lock`

### `threading.r_lock() -> RLock`

### `threading.event() -> Event`

### `threading.semaphore(value: int = 1) -> Semaphore`

### `threading.bounded_semaphore(value: int = 1) -> BoundedSemaphore`

### `threading.barrier(parties: int) -> Barrier`

### `threading.timer(interval: float, function: () -> None) -> Timer`

## BrokenBarrierError

A barrier synchronization primitive, similar to Python's `threading.Barrier`.

## Barrier

A barrier synchronization primitive, similar to Python's `threading.Barrier`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `parties` | `int` |  |
| `broken` | `bool` |  |

### `wait(timeout: float | None = None) -> int`

### `reset()`

### `abort()`

## BoundedSemaphore

A bounded semaphore that checks that the counter never exceeds its initial value,
similar to Python's `threading.BoundedSemaphore`.

### `acquire(blocking: bool = True, timeout: float = -1) -> bool`

### `release()`

### `enter() -> BoundedSemaphore`

### `exit()`

### `__enter__()) -> BoundedSemaphore`

### `__exit__(exc_type: object | None = None, exc_val: object | None = None, null): object? excTb = = > Exit()`

## Event

A thread synchronization event, similar to Python's `threading.Event`.

### `set()`

### `clear()`

### `is_set() -> bool`

### `wait(timeout: float | None = None) -> bool`

## Lock

A non-reentrant mutual exclusion lock, similar to Python's `threading.Lock`.

### `acquire(blocking: bool = True, timeout: float = -1) -> bool`

### `release()`

### `locked() -> bool`

### `enter() -> Lock`

### `exit()`

### `__enter__()) -> Lock`

### `__exit__(exc_type: object | None = None, exc_val: object | None = None, null): object? excTb = = > Exit()`

## RLock

A reentrant mutual exclusion lock, similar to Python's `threading.RLock`.
The same thread may acquire it multiple times without deadlocking.

### `acquire(blocking: bool = True, timeout: float = -1) -> bool`

### `release()`

### `enter() -> RLock`

### `exit()`

### `__enter__()) -> RLock`

### `__exit__(exc_type: object | None = None, exc_val: object | None = None, null): object? excTb = = > Exit()`

## Semaphore

A counting semaphore, similar to Python's `threading.Semaphore`.

### `acquire(blocking: bool = True, timeout: float = -1) -> bool`

### `release()`

### `enter() -> Semaphore`

### `exit()`

### `__enter__()) -> Semaphore`

### `__exit__(exc_type: object | None = None, exc_val: object | None = None, null): object? excTb = = > Exit()`

## Thread

Represents a thread of control, similar to Python's `threading.Thread`.
Unlike CPython, .NET has no GIL — threads run with True parallelism.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `is_alive` | `bool` |  |
| `ident` | `int` |  |

### `start()`

### `join(timeout: float | None = None)`

### `run()`

Override this method when subclassing Thread instead of passing a target callable.

## Timer

A timer that executes a function after a specified interval,
similar to Python's `threading.Timer`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `is_alive` | `bool` |  |

### `start()`

### `cancel()`
