# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T04:02:34.894065
**Type:** compilation_failed
**Feature Focus:** interface_definition
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex interface definition with interface inheritance and property requirements
# Tests multiple interface inheritance, property requirements, method dispatch

interface IIdentifiable:
    property id: int

interface ILoggable:
    def log(self) -> str: ...

interface IVersioned(IIdentifiable, ILoggable):
    property version: str
    def upgrade(self) -> None: ...

class Document(IVersioned):
    _id: int
    title: str
    _version: str
    
    def __init__(self, doc_id: int, title: str):
        self._id = doc_id
        self.title = title
        self._version = "1.0"
    
    property get id(self) -> int:
        return self._id
    
    property get version(self) -> str:
        return self._version
    
    def log(self) -> str:
        return f"Doc({self.id}): {self.title}"
    
    def upgrade(self) -> None:
        parts: list[str] = self._version.split(".")
        major: int = int(parts[0])
        self._version = f"{major + 1}.0"

class Image(IVersioned):
    _id: int
    width: int
    height: int
    _version: str
    
    def __init__(self, img_id: int, w: int, h: int):
        self._id = img_id
        self.width = w
        self.height = h
        self._version = "0.9"
    
    property get id(self) -> int:
        return self._id
    
    property get version(self) -> str:
        return self._version
    
    def log(self) -> str:
        return f"Img({self.id}): {self.width}x{self.height}"
    
    def upgrade(self) -> None:
        self._version = "1.0"

def process(item: ILoggable) -> str:
    return item.log()

def count_ids(items: list[IIdentifiable]) -> int:
    total: int = 0
    for item in items:
        total += item.id
    return total

def main():
    d = Document(1, "Report")
    i = Image(2, 100, 200)
    
    print(d.id)
    print(d.version)
    print(process(d))
    
    d.upgrade()
    print(d.version)
    
    print(i.id)
    print(process(i))
    
    items: list[IIdentifiable] = [d, i]
    print(count_ids(items))

```

## Error

```
Assembly compilation failed:

error[CS0535]: 'DogfoodTest.Document' does not implement interface member 'DogfoodTest.IVersioned.Version.set'
  --> dogfood_test.cs:28:29
    |
 28 |         return self._version
    |                             ^
    |

error[CS0535]: 'DogfoodTest.Image' does not implement interface member 'DogfoodTest.IVersioned.Version.set'
  --> /tmp/tmpft5cpf_z/dogfood_test.spy:26:26
    |
 26 |     
    |     ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpft5cpf_z/dogfood_test.cs

```

## Timing

- Generation: 171.66s
- Execution: 4.73s
