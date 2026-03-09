# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T13:43:09.045704
**Type:** compilation_failed
**Feature Focus:** auto_property
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Auto-properties with inheritance, interfaces, and pattern matching
# This tests read-write, read-only, init-only, and computed properties
# along with virtual/override property dispatch and interface implementation

interface ILocatable:
    property location: str

@abstract
class Asset:
    property id: int = 0
    property get category(self) -> str: ...

    def __init__(self, asset_id: int):
        self.id = asset_id

class Document(Asset, ILocatable):
    # Auto-properties with defaults
    property title: str = "Untitled"
    word_count: int = 0
    
    # Init-only property
    property init author: str
    
    # Computed property with backing field
    _content: str = ""
    property get content(self) -> str:
        return self._content
    property set content(self, value: str):
        self._content = value
        self.word_count = len(value.split(" ")) if len(value) > 0 else 0
    
    # Virtual computed property
    @virtual
    property get category(self) -> str:
        return "Document"
    
    # Static property
    @static
    property doc_count: int = 0
    
    def __init__(self, doc_id: int, title: str, author: str):
        super().__init__(doc_id)
        self.title = title
        self.author = author
        self._content = ""
        Document.doc_count += 1
    
    # Interface implementation
    property get location(self) -> str:
        return f"docs/{self.id}"

class SecureDocument(Document):
    property clearance_level: int = 1
    
    @override
    property get category(self) -> str:
        base: str = "Document"  # simplified, in real code would call base
        return f"Secure-{base}"
    
    def __init__(self, doc_id: int, title: str, author: str, level: int):
        super().__init__(doc_id, title, author)
        self.clearance_level = level

def classify_asset(asset: Asset) -> str:
    match asset:
        case SecureDocument() as sd:
            return f"SECURE[{sd.clearance_level}]: {sd.category}"
        case Document() as d:
            return f"DOC: {d.category}"
        case _:
            return "UNKNOWN"

def main():
    # Create documents with auto-properties
    d1: Document = Document(1, "Report", "Alice")
    d1.content = "This is a quarterly report document"
    
    d2: Document = Document(2, "Notes", "Bob")
    d2.content = "Meeting notes from the team sync"
    
    secure: SecureDocument = SecureDocument(100, "Classified", "Admin", 5)
    secure.content = "Top secret information here"
    
    # Test read-write auto-properties
    print(d1.title)
    d1.title = "Annual Report"
    print(d1.title)
    
    # Test init-only property (readable but set only in __init__)
    print(d1.author)
    
    # Test computed property with side effects
    print(d1.word_count)
    
    # Test virtual/override property dispatch
    print(d1.category)
    print(secure.category)
    
    # Test static property
    print(Document.doc_count)
    
    # Test interface property
    loc: ILocatable = d1
    print(loc.location)
    
    # Test pattern matching with properties
    print(classify_asset(d1))
    print(classify_asset(secure))
    
    # Test read-only property (id from base class)
    print(d1.id)

```

## Error

```
Assembly compilation failed:

error[CS0534]: 'DogfoodTest.Document' does not implement inherited abstract member 'DogfoodTest.Asset.Category.get'
  --> /tmp/tmppqxv232y/dogfood_test.spy:18:18
    |
 18 |     property title: str = "Untitled"
    |                  ^
    |

error[CS0535]: 'DogfoodTest.Document' does not implement interface member 'DogfoodTest.ILocatable.Location.set'
  --> /tmp/tmppqxv232y/dogfood_test.spy:18:36
    |
 18 |     property title: str = "Untitled"
    |                                    ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmppqxv232y/dogfood_test.cs

```

## Timing

- Generation: 152.11s
- Execution: 4.89s
