# Successful Dogfood Run

**Timestamp:** 2026-03-10T03:38:27.976442
**Feature Focus:** builtin_aggregation
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test builtin aggregation functions with manual comparison
# Uses min/max with manual key comparison, all/any for validation

class Student:
    name: str
    score: int
    
    def __init__(self, name: str, score: int):
        self.name = name
        self.score = score

class GradeBook:
    students: list[Student]
    
    def __init__(self):
        self.students = []
    
    def add_student(self, name: str, score: int) -> None:
        self.students.append(Student(name, score))

def analyze_grades(book: GradeBook) -> None:
    scores: list[int] = []
    for s in book.students:
        scores.append(s.score)
    
    # Check if all students passed (score >= 60)
    all_passed: bool = all([s >= 60 for s in scores])
    print(all_passed)
    
    # Check if any student got perfect score (100)
    has_perfect: bool = any([s == 100 for s in scores])
    print(has_perfect)
    
    if len(scores) > 0:
        # Find student with highest score manually
        top_student: Student = book.students[0]
        for s in book.students:
            if s.score > top_student.score:
                top_student = s
        print(top_student.name)
        print(top_student.score)
        
        # Find student with lowest score manually
        bottom_student: Student = book.students[0]
        for s in book.students:
            if s.score < bottom_student.score:
                bottom_student = s
        print(bottom_student.score)
        
        # Calculate total manually
        total: int = 0
        for s in scores:
            total += s
        print(total)

def main():
    book: GradeBook = GradeBook()
    book.add_student("Alice", 85)
    book.add_student("Bob", 72)
    book.add_student("Carol", 100)
    book.add_student("David", 60)
    analyze_grades(book)

```

## Output

```
True
True
Carol
100
60
317
```

## Timing

- Generation: 328.22s
- Execution: 5.44s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
