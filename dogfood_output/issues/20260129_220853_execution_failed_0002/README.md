# Issue Report: execution_failed

**Timestamp:** 2026-01-29T22:07:47.187187
**Type:** execution_failed
**Feature Focus:** try_except_finally
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test try/except/finally with file operations simulation
# Tests exception handling with resource cleanup

class FileHandle:
    path: str
    is_open: bool

    def __init__(self, path: str):
        self.path = path
        self.is_open = False

    def open(self) -> None:
        print(f"Opening {self.path}")
        self.is_open = True

    def read_line(self, line_num: int) -> str:
        if not self.is_open:
            raise RuntimeError("File not open")
        if line_num < 0:
            raise ValueError("Invalid line number")
        return f"Line {line_num} content"

    def close(self) -> None:
        if self.is_open:
            print(f"Closing {self.path}")
            self.is_open = False

def read_with_error_handling(file: FileHandle, line: int) -> str:
    result: str = ""
    try:
        file.open()
        result = file.read_line(line)
        print(f"Successfully read: {result}")
    except ValueError:
        print("ValueError caught: invalid line number")
    except RuntimeError:
        print("RuntimeError caught: file access error")
    finally:
        file.close()
        print("Cleanup completed")
    
    return result

def main():
    file1: FileHandle = FileHandle("data.txt")
    content: str = read_with_error_handling(file1, 5)
    print("---")
    
    file2: FileHandle = FileHandle("config.txt")
    bad_content: str = read_with_error_handling(file2, -1)

# EXPECTED OUTPUT:
# Opening data.txt
# Successfully read: Line 5 content
# Closing data.txt
# Cleanup completed
# ---
# Opening config.txt
# ValueError caught: invalid line number
# Closing config.txt
# Cleanup completed
```

## Error

```
Compilation failed:
  Semantic error at line 18, column 19: Undefined identifier 'RuntimeError'
  Semantic error at line 20, column 19: Undefined identifier 'ValueError'

```

## Timing

- Generation: 40.43s
- Execution: 4.76s
