# Successful Dogfood Run

**Timestamp:** 2026-02-25T08:59:23.518579
**Feature Focus:** list_comprehension
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
def process_scores(scores: list[int]) -> tuple[list[int], list[int], int]:
    high_scores: list[int] = [s for s in scores if s >= 80]
    
    boosted: list[int] = [s + 10 for s in scores]
    
    # Nested: comprehension inside len() with condition
    final_count: int = len([s for s in boosted if s >= 90])
    
    return (high_scores, boosted, final_count)

def main():
    grades: list[int] = [75, 92, 68, 85, 88, 72, 95]
    
    high: list[int]
    adjusted: list[int]
    count: int
    high, adjusted, count = process_scores(grades)
    
    print(high)
    print(adjusted)
    print(count)
    
    categories: list[str] = [f"score_{s}" for s in grades if s > 80]
    print(categories)

# EXPECTED OUTPUT:
# [92, 85, 88, 95]
# [85, 102, 78, 95, 98, 82, 105]
# 4
# [score_92, score_85, score_88, score_95]
```

## Output

```
[92, 85, 88, 95]
[85, 102, 78, 95, 98, 82, 105]
4
[score_92, score_85, score_88, score_95]
```

## Timing

- Generation: 154.35s
- Execution: 4.78s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
