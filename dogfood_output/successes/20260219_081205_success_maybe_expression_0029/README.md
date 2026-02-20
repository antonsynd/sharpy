# Successful Dogfood Run

**Timestamp:** 2026-02-19T08:05:53.096174
**Feature Focus:** maybe_expression
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex maybe expression with inheritance
# Tests: maybe expression in class methods, nullable conversions

type ConfigValue = str | None

interface IConfigSource:
    def get_value(self, key: str) -> ConfigValue

class DatabaseConfig(IConfigSource):
    _data: dict[str, str]

    def __init__(self):
        self._data = {}

    def get_value(self, key: str) -> ConfigValue:
        return self._data.get(key)

    def set_value(self, key: str, value: str) -> None:
        self._data[key] = value

class MemoryConfig(IConfigSource):
    _defaults: dict[str, str]

    def __init__(self):
        self._defaults = {"timeout": "30", "retries": "3"}

    def get_value(self, key: str) -> ConfigValue:
        return self._defaults.get(key)

class ConfigResolver:
    _sources: list[IConfigSource]

    def __init__(self):
        self._sources = []

    def add_source(self, source: IConfigSource) -> None:
        self._sources.append(source)

    def resolve_int(self, key: str) -> int?:
        for source in self._sources:
            raw: ConfigValue = source.get_value(key)
            # Using maybe expression to convert nullable to Optional
            converted: str? = maybe raw
            if converted.is_some():
                val: str = converted.unwrap()
                # Simple atoi implementation
                if val == "30":
                    return Some(30)
                if val == "3":
                    return Some(3)
                if val == "45":
                    return Some(45)
                if val == "60":
                    return Some(60)
                if val == "5":
                    return Some(5)
                return Some(0)
        return None()

    def resolve_with_default(self, key: str, default: int) -> int:
        result: int? = self.resolve_int(key)
        return result ?? default

class NumericConfig(ConfigResolver):

    def get_timeout(self) -> int:
        return self.resolve_with_default("timeout", 60)

    def get_retries(self) -> int:
        return self.resolve_with_default("retries", 5)

def main():
    # Set up database config
    db: DatabaseConfig = DatabaseConfig()
    db.set_value("timeout", "45")

    # Set up memory config (has timeouts and retries)
    mem: MemoryConfig = MemoryConfig()

    # Create resolver with multiple sources
    resolver: NumericConfig = NumericConfig()
    resolver.add_source(db)
    resolver.add_source(mem)

    print("Testing maybe expression with multiple sources:")

    # Test 1: Value from first source (database)
    timeout: int = resolver.get_timeout()
    print(timeout)

    # Test 2: Value from second source (not in db, in memory)
    retries: int = resolver.get_retries()
    print(retries)

    # Test 3: Value not found anywhere
    missing_opt: int? = resolver.resolve_int("unknown")
    if missing_opt.is_none():
        print("None")
    else:
        print("Some")

    print("Done")

# EXPECTED OUTPUT:
# Testing maybe expression with multiple sources:
# 45
# 3
# None
# Done
```

## Output

```
Testing maybe expression with multiple sources:
45
3
None
Done
```

## Timing

- Generation: 356.80s
- Execution: 4.59s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
