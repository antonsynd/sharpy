# Successful Dogfood Run

**Timestamp:** 2026-03-06T23:52:54.379422
**Feature Focus:** import_statement
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Single-module test: Generic containers, inheritance, and type operations
# No external imports - all definitions are self-contained

type Score = float
type UserId = int
type UserMap = dict[UserId, str]

enum Status:
    INACTIVE = 0
    ACTIVE = 1
    SUSPENDED = 2

@abstract
class DataContainer[T]:
    @abstract
    def get_value(self) -> T: ...

    @abstract
    def set_value(self, value: T) -> None: ...

    @abstract
    def is_empty(self) -> bool: ...

class IntContainer(DataContainer[int]):
    _value: int

    def __init__(self, initial: int):
        self._value = initial

    @override
    def get_value(self) -> int:
        return self._value

    @override
    def set_value(self, value: int) -> None:
        self._value = value

    @override
    def is_empty(self) -> bool:
        return self._value == 0

class ScoreContainer(DataContainer[Score]):
    _score: Score = 0.0

    def __init__(self, initial: Score):
        self._score = initial

    @override
    def get_value(self) -> Score:
        return self._score

    @override
    def set_value(self, value: Score) -> None:
        self._score = value

    @override
    def is_empty(self) -> bool:
        return self._score == 0.0

def calculate_bonus(base: Score, multiplier: int) -> Score:
    return base * multiplier to Score

def format_user_id(uid: UserId) -> str:
    return f"User-{uid}"

def is_active(status: Status) -> bool:
    return status == Status.ACTIVE

def process_containers() -> None:
    int_c = IntContainer(42)
    print(int_c.get_value())
    print(int_c.is_empty())
    int_c.set_value(0)
    print(int_c.is_empty())

    score_c = ScoreContainer(85.5)
    print(score_c.get_value())
    bonus: Score = calculate_bonus(score_c.get_value(), 2)
    print(bonus)

def process_status() -> None:
    s: Status = Status.ACTIVE
    print(is_active(s))
    print(is_active(Status.INACTIVE))
    for st in Status:
        print(st.value)

def process_users() -> None:
    users: UserMap = {1: "Alice", 2: "Bob"}
    for uid in users:
        formatted: str = format_user_id(uid)
        print(formatted)
        print(users[uid])

def main():
    process_containers()
    process_status()
    process_users()

```

## Output

```
42
False
True
85.5
171.0
True
False
0
1
2
User-1
Alice
User-2
Bob
```

## Timing

- Generation: 581.19s
- Execution: 4.93s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
