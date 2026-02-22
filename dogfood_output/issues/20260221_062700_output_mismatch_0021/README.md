# Issue Report: output_mismatch

**Timestamp:** 2026-02-21T06:16:32.774637
**Type:** output_mismatch
**Feature Focus:** enum_definition
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex enum state machine with generics and inheritance
# Tests enum values in arithmetic, if-elif chains, and abstract classes

enum HttpStatus:
    OK = 200
    CREATED = 201
    BAD_REQUEST = 400
    UNAUTHORIZED = 401
    FORBIDDEN = 403
    NOT_FOUND = 404
    SERVER_ERROR = 500

enum Priority:
    LOW = 1
    MEDIUM = 2
    HIGH = 3
    CRITICAL = 4

type ResponsePair = tuple[HttpStatus, Priority]

@abstract
class RequestHandler:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    @abstract
    def can_handle(self, status: HttpStatus) -> bool:
        ...
    
    @abstract
    def process(self, status: HttpStatus, priority: Priority) -> str:
        ...

class ErrorHandler(RequestHandler):
    def __init__(self):
        super().__init__("ErrorHandler")
    
    @override
    def can_handle(self, status: HttpStatus) -> bool:
        return status.value >= 400
    
    @override
    def process(self, status: HttpStatus, priority: Priority) -> str:
        result: str = ""
        
        # Use if-elif instead of match for enum comparison
        if status.value == HttpStatus.BAD_REQUEST.value:
            result = "bad_request"
        elif status.value == HttpStatus.UNAUTHORIZED.value:
            result = "unauthorized"
        elif status.value == HttpStatus.FORBIDDEN.value:
            result = "forbidden"
        elif status.value == HttpStatus.NOT_FOUND.value:
            result = "not_found"
        elif status.value == HttpStatus.SERVER_ERROR.value:
            result = "server_error"
        else:
            result = "unknown_error"
        
        return result

class SuccessHandler(RequestHandler):
    def __init__(self):
        super().__init__("SuccessHandler")
    
    @override
    def can_handle(self, status: HttpStatus) -> bool:
        return status.value < 300
    
    @override
    def process(self, status: HttpStatus, priority: Priority) -> str:
        multiplier: int = 0
        
        # Use if-elif instead of match for enum comparison
        if priority.value == Priority.LOW.value:
            multiplier = 1
        elif priority.value == Priority.MEDIUM.value:
            multiplier = 2
        elif priority.value == Priority.HIGH.value:
            multiplier = 3
        elif priority.value == Priority.CRITICAL.value:
            multiplier = 4
        else:
            multiplier = 0
        
        base: int = status.value - 200
        return f"success_{base * multiplier}"

class StateMachine:
    _history: list[ResponsePair]
    
    def __init__(self):
        self._history = []
    
    def add_response(self, pair: ResponsePair) -> None:
        self._history.append(pair)
    
    def get_priority_sum(self) -> int:
        total: int = 0
        for pair in self._history:
            status, prio = pair
            if status.value >= 400:
                total -= prio.value
            else:
                total += prio.value
        return total

def calculate_escalation(current: Priority, severity: int) -> Priority:
    new_value: int = current.value + severity
    
    # Use if-elif instead of match for enum comparison
    if new_value == 1:
        return Priority.LOW
    elif new_value == 2:
        return Priority.MEDIUM
    elif new_value >= 4:
        return Priority.CRITICAL
    else:
        return Priority.HIGH

def main():
    machine: StateMachine = StateMachine()
    
    # Test tuple creation
    r1: ResponsePair = (HttpStatus.OK, Priority.HIGH)
    r2: ResponsePair = (HttpStatus.NOT_FOUND, Priority.LOW)
    r3: ResponsePair = (HttpStatus.SERVER_ERROR, Priority.CRITICAL)
    
    machine.add_response(r1)
    machine.add_response(r2)
    machine.add_response(r3)
    
    # Test priority sum calculation
    print(machine.get_priority_sum())
    
    # Test handler dispatch with if-elif chains
    handlers: list[RequestHandler] = [SuccessHandler(), ErrorHandler()]
    responses: list[ResponsePair] = [r1, r2, r3]
    
    for pair in responses:
        status, prio = pair
        for handler in handlers:
            if handler.can_handle(status):
                print(handler.process(status, prio))
    
    # Test priority escalation
    escalated: Priority = calculate_escalation(Priority.LOW, 3)
    print(escalated.value)
    
    # Test walrus with enum comparison
    if (p := Priority.MEDIUM).value > 1:
        print(p.value)

# EXPECTED OUTPUT:
# 5
# success_3
# not_found
# success_-2
# server_error
# 4
# 2
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
5
success_3
not_found
success_-2
server_error
4
2

```

### Actual
```
-2
success_0
not_found
server_error
4
2
```

## Timing

- Generation: 533.97s
- Execution: 5.00s
