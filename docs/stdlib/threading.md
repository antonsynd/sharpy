# threading

Thread-based concurrency primitives.

```python
import threading
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `TIMEOUT_MAX` | `float` | The largest timeout, in seconds, that a blocking call accepts; a larger one raises OverflowError. A wait longer than about 24.8 days is cut short at 24.8 days. |

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

### `acquire(blocking: bool = True, timeout: float | None = None) -> bool`

### `release()`

### `enter() -> BoundedSemaphore`

### `exit()`

### `__enter__() -> BoundedSemaphore`

### `__exit__(exc_type: object | None = None, exc_val: object | None = None, exc_tb: object | None = None)`

## Event

A thread synchronization event, similar to Python's `threading.Event`.

### `set()`

### `clear()`

### `is_set() -> bool`

### `wait(timeout: float | None = None) -> bool`

Wait until the flag is set, or until *timeout* seconds pass, and return
the flag. A set flag returns `True` whatever the timeout; a timeout at or below zero
does not wait.

## Lock

A non-reentrant mutual exclusion lock, similar to Python's `threading.Lock`.

### `acquire(blocking: bool = True, timeout: float = -1) -> bool`

### `release()`

### `locked() -> bool`

### `enter() -> Lock`

### `exit()`

### `__enter__() -> Lock`

### `__exit__(exc_type: object | None = None, exc_val: object | None = None, exc_tb: object | None = None)`

## RLock

A reentrant mutual exclusion lock, similar to Python's `threading.RLock`.
The same thread may acquire it multiple times without deadlocking.

### `acquire(blocking: bool = True, timeout: float = -1) -> bool`

### `release()`

### `enter() -> RLock`

### `exit()`

### `__enter__() -> RLock`

### `__exit__(exc_type: object | None = None, exc_val: object | None = None, exc_tb: object | None = None)`

## Semaphore

A counting semaphore, similar to Python's `threading.Semaphore`.

### `acquire(blocking: bool = True, timeout: float | None = None) -> bool`

### `release()`

### `enter() -> Semaphore`

### `exit()`

### `__enter__() -> Semaphore`

### `__exit__(exc_type: object | None = None, exc_val: object | None = None, exc_tb: object | None = None)`

## Thread

Represents a thread of control, similar to Python's `threading.Thread`.
Unlike CPython, .NET has no GIL — threads run with True parallelism.
Misuse that depends on the thread's state raises `RuntimeError`, as in python:
`start()` twice, `join()` before `start()` or on the current thread, and setting
`daemon` once started.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `ident` | `int` |  |

### `is_alive() -> bool`

### `start()`

### `join(timeout: float | None = None)`

### `run()`

Override this method when subclassing Thread instead of passing a target callable.

## Timer

A thread that executes a function after a specified interval,
similar to Python's `threading.Timer`. A `Timer` is a `Thread`:
`start()` starts it, `join()` waits for it, and `cancel()` stops it
from calling the function while it is still waiting.

### `cancel()`

Stop the timer, and cancel the execution of the timer's action. This only works
if the timer is still in its waiting stage.

### `run()`

Wait for the interval (or until cancelled), then call the function unless cancelled.
