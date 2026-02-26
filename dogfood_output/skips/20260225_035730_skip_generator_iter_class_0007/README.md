# Skipped Dogfood Run

**Timestamp:** 2026-02-25T03:46:44.206986
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0220]: 'yield from' requires an iterable, but got 'int'
  --> /tmp/tmpat326fdo/dogfood_test.spy:54:24
    |
 54 |             yield from right_node.__reversed__()
    |                        ^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0220]: 'yield from' requires an iterable, but got 'int'
  --> /tmp/tmpat326fdo/dogfood_test.spy:58:24
    |
 58 |             yield from left_node.__reversed__()
    |                        ^^^^^^^^^^^^^^^^^^^^^^^^
    |


**Feature Focus:** generator_iter_class
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Generator methods for tree traversal (in-order, pre-order, post-order)
class TreeNode:
    value: int
    left: Optional[TreeNode]
    right: Optional[TreeNode]
    
    def __init__(self, value: int):
        self.value = value
        self.left = None()
        self.right = None()
    
    def add(self, value: int) -> None:
        if value < self.value:
            if self.left.is_none():
                self.left = Some(TreeNode(value))
            else:
                self.left.unwrap().add(value)
        else:
            if self.right.is_none():
                self.right = Some(TreeNode(value))
            else:
                self.right.unwrap().add(value)
    
    def __iter__(self) -> int:
        if self.left.is_some():
            for v in self.left.unwrap():
                yield v
        yield self.value
        if self.right.is_some():
            for v in self.right.unwrap():
                yield v
    
    def pre_order(self) -> int:
        yield self.value
        if self.left.is_some():
            for v in self.left.unwrap().pre_order():
                yield v
        if self.right.is_some():
            for v in self.right.unwrap().pre_order():
                yield v
    
    def post_order(self) -> int:
        if self.left.is_some():
            for v in self.left.unwrap().post_order():
                yield v
        if self.right.is_some():
            for v in self.right.unwrap().post_order():
                yield v
        yield self.value
    
    def __reversed__(self) -> int:
        if self.right.is_some():
            right_node: TreeNode = self.right.unwrap()
            yield from right_node.__reversed__()
        yield self.value
        if self.left.is_some():
            left_node: TreeNode = self.left.unwrap()
            yield from left_node.__reversed__()
    
    def __len__(self) -> int:
        count: int = 1
        if self.left.is_some():
            count += len(self.left.unwrap())
        if self.right.is_some():
            count += len(self.right.unwrap())
        return count

def main():
    #       50
    #      /  \
    #    30    70
    #   /  \   /
    # 20   40 60
    root = TreeNode(50)
    root.add(30)
    root.add(70)
    root.add(20)
    root.add(40)
    root.add(60)
    
    print(f"Size: {len(root)}")
    
    print("In-order:")
    for v in root:
        print(v)
    
    print("Pre-order:")
    for v in root.pre_order():
        print(v)
    
    print("Post-order:")
    for v in root.post_order():
        print(v)
    
    print("Reversed:")
    for v in reversed(root):
        print(v)
    
    print("First two:")
    count: int = 0
    for v in root:
        print(v)
        count += 1
        if count >= 2:
            break

# EXPECTED OUTPUT:
# Size: 6
# In-order:
# 20
# 30
# 40
# 50
# 60
# 70
# Pre-order:
# 50
# 30
# 20
# 40
# 70
# 60
# Post-order:
# 20
# 40
# 30
# 60
# 70
# 50
# Reversed:
# 70
# 60
# 50
# 40
# 30
# 20
# 20
# 30
```

## Timing

- Generation: 630.71s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
