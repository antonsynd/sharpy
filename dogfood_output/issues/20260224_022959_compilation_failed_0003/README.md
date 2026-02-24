# Issue Report: compilation_failed

**Timestamp:** 2026-02-24T02:26:27.112265
**Type:** compilation_failed
**Feature Focus:** dunder_bool
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test __bool__ dunder method with abstract base class and inheritance

@abstract
class Resource:
    _name: str
    
    @abstract
    def __bool__(self) -> bool: ...
    
    def get_status(self) -> str:
        if self:
            return f"{self._name}: active"
        return f"{self._name}: inactive"

class Connection(Resource):
    _connected: bool
    
    def __init__(self, name: str):
        self._name = name
        self._connected = False
    
    @virtual
    def connect(self) -> None:
        self._connected = True
    
    @virtual
    def disconnect(self) -> None:
        self._connected = False
    
    @override
    def __bool__(self) -> bool:
        return self._connected

class SecureConnection(Connection):
    _authenticated: bool
    
    def __init__(self, name: str):
        super().__init__(name)
        self._authenticated = False
    
    @override
    def connect(self) -> None:
        super().connect()
        self._authenticated = True
    
    @override
    def disconnect(self) -> None:
        super().disconnect()
        self._authenticated = False
    
    @override
    def __bool__(self) -> bool:
        return self._connected and self._authenticated

def main():
    conn = Connection("basic")
    print(bool(conn))
    print(conn.get_status())
    
    conn.connect()
    print(bool(conn))
    print(conn.get_status())
    
    secure = SecureConnection("secure")
    print(bool(secure))
    
    secure.connect()
    print(bool(secure))
    
    secure.disconnect()
    print(bool(secure))
    print(secure.get_status())

# EXPECTED OUTPUT:
# False
# basic: inactive
# True
# basic: active
# False
# True
# False
# secure: inactive
```

## Error

```
Unhandled exception. System.NotImplementedException: The method or operation is not implemented.
   at DogfoodTest.Resource.get_IsTrue() in /tmp/tmpjfoqwkad/dogfood_test.spy:line 8
   at DogfoodTest.Resource.op_True(Resource value) in /tmp/tmpjfoqwkad/dogfood_test.spy:line 14
   at DogfoodTest.Resource.GetStatus() in /tmp/tmpjfoqwkad/dogfood_test.spy:line 11
   at DogfoodTest.Main() in /tmp/tmpjfoqwkad/dogfood_test.spy:line 58

```

## Compiler Output

```
False

```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpjfoqwkad/dogfood_test.cs

```

## Timing

- Generation: 197.38s
- Execution: 5.84s
