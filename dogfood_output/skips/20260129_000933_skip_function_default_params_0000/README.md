# Skipped Dogfood Run

**Timestamp:** 2026-01-29T00:09:03.805775
**Skip Reason:** Pre-validation failed after 3 attempts: Line 45: tuple unpacking (not fully supported) - 'score2: int = calculate_score(base=50, bonus=20)...'
**Feature Focus:** function_default_params
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test: Function default parameters with configuration builder pattern
# Uses: default params, method chaining, multiple parameters, keyword args

class ServerConfig:
    host: str
    port: int
    timeout: int
    retries: int

    def __init__(self, host: str, port: int, timeout: int, retries: int):
        self.host = host
        self.port = port
        self.timeout = timeout
        self.retries = retries

    def display(self) -> None:
        print(f"Host: {self.host}")
        print(f"Port: {self.port}")
        print(f"Timeout: {self.timeout}s")
        print(f"Retries: {self.retries}")

def create_config(host: str = "localhost", port: int = 8080, timeout: int = 30, retries: int = 3) -> ServerConfig:
    return ServerConfig(host, port, timeout, retries)

def calculate_score(base: int, bonus: int = 10, multiplier: int = 1) -> int:
    return (base + bonus) * multiplier

def main():
    # Test 1: All defaults
    config1: ServerConfig = create_config()
    config1.display()
    
    print("100")
    
    # Test 2: Override some params with keyword args
    config2: ServerConfig = create_config(host="api.example.com", port=443)
    config2.display()
    
    print("200")
    
    # Test 3: Default params in calculations
    score1: int = calculate_score(50)
    print(f"{score1}")
    
    score2: int = calculate_score(base=50, bonus=20)
    print(f"{score2}")
    
    score3: int = calculate_score(base=50, bonus=15, multiplier=2)
    print(f"{score3}")

# EXPECTED OUTPUT:
# Host: localhost
# Port: 8080
# Timeout: 30s
# Retries: 3
# 100
# Host: api.example.com
# Port: 443
# Timeout: 30s
# Retries: 3
# 200
# 60
# 70
# 130
```

## Timing

- Generation: 29.85s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
