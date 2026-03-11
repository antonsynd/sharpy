# Issue Report: output_mismatch

**Timestamp:** 2026-03-10T11:32:30.052207
**Type:** output_mismatch
**Feature Focus:** function_keyword_args
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex keyword arguments with configuration builder pattern
# Uses default parameters and keyword arguments (simplified for Sharpy)

class DataConfig:
    property threshold: float = 0.5
    property max_items: int = 100
    property debug_mode: bool = False
    
    def __init__(self, threshold: float = 0.5, max_items: int = 100, debug_mode: bool = False):
        self.threshold = threshold
        self.max_items = max_items
        self.debug_mode = debug_mode

class Processor:
    config: DataConfig
    
    def __init__(self, config: DataConfig):
        self.config = config
    
    def process(self, values: list[float], multiplier: float = 1.0) -> list[float]:
        results: list[float] = []
        count: int = 0
        for v in values:
            if count >= self.config.max_items:
                break
            adjusted: float = v * multiplier
            if adjusted > self.config.threshold:
                results.append(adjusted)
                count += 1
        if self.config.debug_mode:
            print(f"Processed {count} items")
        return results

def create_config(strict: bool = False, limit: int = 50) -> DataConfig:
    if strict:
        return DataConfig(threshold=0.9, max_items=limit, debug_mode=True)
    else:
        return DataConfig(threshold=0.1, max_items=limit * 2, debug_mode=False)

def main():
    values: list[float] = [0.2, 0.8, 0.3, 0.95, 0.4, 0.6]
    
    strict_config = create_config(strict=True, limit=3)
    lenient_config = create_config(strict=False, limit=5)
    
    strict_proc = Processor(strict_config)
    lenient_proc = Processor(lenient_config)
    
    strict_results = strict_proc.process(values, multiplier=2.0)
    lenient_results = lenient_proc.process(values, multiplier=0.5)
    
    for r in strict_results:
        print(r)
    for r in lenient_results:
        print(r)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
1.6
1.9
0.4
0.15
0.475
0.3

```

### Actual
```
Processed 3 items
1.6
1.9
1.2
0.4
0.15
0.475
0.2
0.3
```

## Timing

- Generation: 437.75s
- Execution: 5.27s
