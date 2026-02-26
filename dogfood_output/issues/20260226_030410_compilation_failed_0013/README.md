# Issue Report: compilation_failed

**Timestamp:** 2026-02-26T03:02:16.714094
**Type:** compilation_failed
**Feature Focus:** set_comprehension
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Set comprehension with filtering and nested iteration
# Extracts unique vowels from words meeting length criteria
def extract_vowels(words: list[str]) -> set[str]:
    # Nested for clauses: outer iterates words, inner iterates characters
    # Filters: word length > 3, and character is a vowel
    return {c.lower() for word in words if len(word) > 3 for c in word if c.lower() in "aeiou"}

def count_consonants(words: list[str]) -> int:
    # Set comprehension for unique consonants across all words
    consonants: set[str] = {c.lower() for word in words for c in word if c.lower() not in "aeiou" and c.isalpha()}
    return len(consonants)

def main():
    words: list[str] = ["Hello", "World", "Programming", "Code", "Test"]
    
    vowels: set[str] = extract_vowels(words)
    consonant_count: int = count_consonants(words)
    
    # Sort for deterministic output
    sorted_vowels: list[str] = sorted(vowels)
    
    print(consonant_count)
    print(len(sorted_vowels))
    for v in sorted_vowels:
        print(v)
```

## Error

```
Assembly compilation failed:

error[CS1929]: 'char' does not contain a definition for 'Lower' and the best extension method overload 'StringExtensions.Lower(string)' requires a receiver of type 'string'
  --> dogfood_test.cs:22:42
    |
 22 |     print(consonant_count)
    |                           ^
    |

error[CS1929]: 'char' does not contain a definition for 'Lower' and the best extension method overload 'StringExtensions.Lower(string)' requires a receiver of type 'string'
  --> dogfood_test.cs:24:38
    |
 24 |     for v in sorted_vowels:
    |                            ^
    |

error[CS0029]: Cannot implicitly convert type 'Sharpy.Set<object>' to 'Sharpy.Set<string>'
  --> /tmp/tmp1j2aiftx/dogfood_test.spy:6:16
    |
  6 |     return {c.lower() for word in words if len(word) > 3 for c in word if c.lower() in "aeiou"}
    |                ^
    |

error[CS1929]: 'char' does not contain a definition for 'Lower' and the best extension method overload 'StringExtensions.Lower(string)' requires a receiver of type 'string'
  --> /tmp/tmp1j2aiftx/dogfood_test.spy:18:39
    |
 18 |     
    |     ^
    |

error[CS1929]: 'char' does not contain a definition for 'Isalpha' and the best extension method overload 'StringExtensions.Isalpha(string)' requires a receiver of type 'string'
  --> /tmp/tmp1j2aiftx/dogfood_test.spy:18:53
    |
 18 |     
    |     ^
    |

error[CS1929]: 'char' does not contain a definition for 'Lower' and the best extension method overload 'StringExtensions.Lower(string)' requires a receiver of type 'string'
  --> /tmp/tmp1j2aiftx/dogfood_test.spy:20:34
    |
 20 |     sorted_vowels: list[str] = sorted(vowels)
    |                                  ^
    |

error[CS0029]: Cannot implicitly convert type 'Sharpy.Set<object>' to 'Sharpy.Set<string>'
  --> /tmp/tmp1j2aiftx/dogfood_test.spy:10:41
    |
 10 |     consonants: set[str] = {c.lower() for word in words for c in word if c.lower() not in "aeiou" and c.isalpha()}
    |                                         ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmp1j2aiftx/dogfood_test.cs

```

## Timing

- Generation: 96.97s
- Execution: 5.38s
