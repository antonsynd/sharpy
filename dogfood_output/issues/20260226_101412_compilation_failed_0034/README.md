# Issue Report: compilation_failed

**Timestamp:** 2026-02-26T10:11:27.540145
**Type:** compilation_failed
**Feature Focus:** generator_yield_from
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex yield from test: Tree traversal with hierarchical generators
# Tests multi-level delegation, inheritance, and conditional yielding

@abstract
class TreeNode:
    @abstract
    def traverse(self) -> str:
        ...

    @abstract
    def get_name(self) -> str:
        ...

class File(TreeNode):
    name: str
    size: int

    def __init__(self, name: str, size: int):
        self.name = name
        self.size = size

    @override
    def traverse(self) -> str:
        yield f"file:{self.name}"

    @override
    def get_name(self) -> str:
        return self.name

class Folder(TreeNode):
    name: str
    children: list[TreeNode]

    def __init__(self, name: str):
        self.name = name
        self.children = []

    def add_child(self, child: TreeNode) -> None:
        self.children.append(child)

    @override
    def get_name(self) -> str:
        return self.name

    # This is the key: yield from delegation through inheritance
    @override
    def traverse(self) -> str:
        yield f"enter:{self.name}"
        for i in range(len(self.children)):
            child = self.children[i]
            # Nested yield from - delegates to child's traverse()
            yield from child.traverse()
        yield f"exit:{self.name}"

# Generator that delegates to multiple sources
def multi_source_walker(root1: TreeNode, root2: TreeNode) -> str:
    yield "== START =="
    yield from root1.traverse()
    yield "-- SEPARATOR --"
    yield from root2.traverse()
    yield "== END =="

def main():
    # Build first tree: root folder
    docs = Folder("docs")
    docs.add_child(File("readme.txt", 100))
    docs.add_child(File("license.txt", 50))

    # Build second tree: nested structure
    src = Folder("src")
    src.add_child(File("main.py", 200))
    tests = Folder("tests")
    tests.add_child(File("test_main.py", 150))
    src.add_child(tests)

    # Use the multi-source walker with yield from
    # This demonstrates: yield from -> traverse() -> yield from -> traverse()
    for item in multi_source_walker(docs, src):
        print(item)
```

## Error

```
Assembly compilation failed:

error[CS0508]: 'DogfoodTest.File.Traverse()': return type must be 'string' to match overridden member 'DogfoodTest.TreeNode.Traverse()'
  --> dogfood_test.cs:21:72
    |
 21 | 
    | ^
    |

error[CS0508]: 'DogfoodTest.Folder.Traverse()': return type must be 'string' to match overridden member 'DogfoodTest.TreeNode.Traverse()'
  --> /tmp/tmpi2rvu3nw/dogfood_test.spy:46:72
    |
 46 |     @override
    |              ^
    |

error[CS0029]: Cannot implicitly convert type 'char' to 'string'
  --> /tmp/tmpi2rvu3nw/dogfood_test.spy:60:26
    |
 60 |     yield from root2.traverse()
    |                          ^
    |

error[CS0029]: Cannot implicitly convert type 'char' to 'string'
  --> /tmp/tmpi2rvu3nw/dogfood_test.spy:62:26
    |
 62 | 
    | ^
    |

error[CS0029]: Cannot implicitly convert type 'char' to 'string'
  --> /tmp/tmpi2rvu3nw/dogfood_test.spy:54:34
    |
 54 | 
    | ^
    |


```

## Compiler Output

```
warning[SPY0451]: Local variable 'child' is assigned but never used
  --> /tmp/tmpi2rvu3nw/dogfood_test.spy:50:13
    |
 50 |             child = self.children[i]
    |             ^^^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Generated C#

```csharp
warning[SPY0451]: Local variable 'child' is assigned but never used
  --> /tmp/tmpi2rvu3nw/dogfood_test.spy:50:13
    |
 50 |             child = self.children[i]
    |             ^^^^^^^^^^^^^^^^^^^^^^^^
    |

Generated C# code written to: /tmp/tmpi2rvu3nw/dogfood_test.cs

```

## Timing

- Generation: 146.82s
- Execution: 4.19s
