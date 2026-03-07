# Issue Report: output_mismatch

**Timestamp:** 2026-03-07T03:11:17.931742
**Type:** output_mismatch
**Feature Focus:** class_inheritance
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
type ConfigKey = str

@abstract
class ConfigSource:
    @virtual
    def get_value(self, key: ConfigKey) -> str?:
        return None()

    @virtual
    def has_key(self, key: ConfigKey) -> bool:
        return False

class EnvironmentConfig(ConfigSource):
    prefix: str

    def __init__(self):
        self.prefix = ""

    def set_prefix(self, prefix: str) -> None:
        self.prefix = prefix

    @override
    def get_value(self, key: ConfigKey) -> str?:
        full_key = self.prefix + key
        if full_key == "APP_NAME":
            return "TestApp"
        if full_key == "VERSION":
            return "2.0.0"
        return None()

    @override
    def has_key(self, key: ConfigKey) -> bool:
        result = self.get_value(key)
        return result is not None

class InMemoryConfig(ConfigSource):
    data: dict[str, str]

    def __init__(self):
        self.data = {}

    def add_data(self, data: dict[str, str]) -> None:
        for k in data.keys():
            self.data[k] = data[k]

    def set(self, key: ConfigKey, value: str) -> None:
        self.data[key] = value

    @override
    def get_value(self, key: ConfigKey) -> str?:
        if key in self.data:
            return self.data[key]
        return None()

    @override
    def has_key(self, key: ConfigKey) -> bool:
        return key in self.data

class ChainedConfig(ConfigSource):
    sources: list[ConfigSource]

    def __init__(self):
        self.sources = []

    def add_source(self, source: ConfigSource) -> None:
        self.sources.append(source)

    @override
    def get_value(self, key: ConfigKey) -> str?:
        i = 0
        while i < len(self.sources):
            value = self.sources[i].get_value(key)
            if value is not None:
                return value
            i += 1
        return None()

    @override
    def has_key(self, key: ConfigKey) -> bool:
        i = 0
        while i < len(self.sources):
            if self.sources[i].has_key(key):
                return True
            i += 1
        return False

def main():
    # Create layered configuration sources
    env: EnvironmentConfig = EnvironmentConfig()
    env.set_prefix("APP_")

    memory: InMemoryConfig = InMemoryConfig()

    # Populate in-memory config
    memory.set("timeout", "30")
    memory.set("retries", "3")

    # Chain: env first, fallback to memory
    chained: ChainedConfig = ChainedConfig()
    chained.add_source(env)
    chained.add_source(memory)

    # Query various keys
    keys: list[str] = ["APP_NAME", "timeout", "missing", "VERSION"]
    for key in keys:
        if chained.has_key(key):
            print(chained.get_value(key))
        else:
            print(f"Key {key} not found")

    print(memory.has_key("retries"))

    # Test fallback with null coalescing
    debug: str = chained.get_value("debug") ?? "disabled"
    print(debug)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
TestApp
30
Key missing not found
2.0.0
True
disabled

```

### Actual
```
Key APP_NAME not found
30
Key missing not found
Key VERSION not found
True
disabled
```

## Timing

- Generation: 697.66s
- Execution: 4.65s
