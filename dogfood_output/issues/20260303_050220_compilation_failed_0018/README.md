# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T04:50:14.264771
**Type:** compilation_failed
**Feature Focus:** union_generic
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Generic Result2 union with multiple type parameters
# Uses match statements instead of nested match expressions

union Result2[T, E]:
    case Ok(value: T)
    case Err(error: E)

class Validator:
    @static
    def validate_positive(n: int) -> Result2[int, str]:
        if n > 0:
            return Result2.Ok(n)
        return Result2.Err("must be positive")

    @static
    def validate_even(n: int) -> Result2[int, str]:
        if n % 2 == 0:
            return Result2.Ok(n)
        return Result2.Err("must be even")

    @static
    def combine_results(r1: Result2[int, str], r2: Result2[int, str]) -> Result2[int, str]:
        # Match statements with nested match
        result: Result2[int, str] = Result2.Err("default")
        match r1:
            case Result2.Ok:
                match r2:
                    case Result2.Ok:
                        result = Result2.Ok(r1.value * r2.value)
                    case Result2.Err:
                        result = r2
            case Result2.Err:
                result = r1
        return result

class PairProcessor:
    @static
    def process_both(a: Result2[int, str], b: Result2[int, str]) -> str:
        # Nested match statements
        result: str = ""
        match a:
            case Result2.Ok:
                match b:
                    case Result2.Ok:
                        result = f"Both ok: {a.value} and {b.value}"
                    case Result2.Err:
                        result = f"First ok ({a.value}), second failed: {b.error}"
            case Result2.Err:
                match b:
                    case Result2.Ok:
                        result = f"First failed: {a.error}, second ok ({b.value})"
                    case Result2.Err:
                        result = f"Both failed: {a.error} and {b.error}"
        return result

def main():
    # Test validate_positive
    r1: Result2[int, str] = Validator.validate_positive(10)
    r2: Result2[int, str] = Validator.validate_positive(-3)

    # Test validate_even
    r3: Result2[int, str] = Validator.validate_even(8)
    r4: Result2[int, str] = Validator.validate_even(7)

    # Show results using match statements
    s1: str = ""
    match r1:
        case Result2.Ok:
            s1 = f"positive: {r1.value}"
        case Result2.Err:
            s1 = f"error: {r1.error}"
    print(s1)

    s2: str = ""
    match r2:
        case Result2.Ok:
            s2 = f"positive: {r2.value}"
        case Result2.Err:
            s2 = f"error: {r2.error}"
    print(s2)

    s4: str = ""
    match r4:
        case Result2.Ok:
            s4 = f"even: {r4.value}"
        case Result2.Err:
            s4 = f"error: {r4.error}"
    print(s4)

    # Test combining results
    c1: Result2[int, str] = Validator.combine_results(r1, r3)  # Ok(10) + Ok(8) = Ok(80)
    c2: Result2[int, str] = Validator.combine_results(r1, r4)  # Ok(10) + Err(...) = Err(...)

    s_c1: str = ""
    match c1:
        case Result2.Ok:
            s_c1 = f"combined: {c1.value}"
        case Result2.Err:
            s_c1 = f"failed: {c1.error}"
    print(s_c1)

    s_c2: str = ""
    match c2:
        case Result2.Ok:
            s_c2 = f"combined: {c2.value}"
        case Result2.Err:
            s_c2 = f"failed: {c2.error}"
    print(s_c2)

    # Test PairProcessor with different combinations
    print(PairProcessor.process_both(r1, r3))
    print(PairProcessor.process_both(r1, r2))
    print(PairProcessor.process_both(r2, r2))

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:45:81
    |
 45 |                         result = f"Both ok: {a.value} and {b.value}"
    |                                                                     ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:45:97
    |
 45 |                         result = f"Both ok: {a.value} and {b.value}"
    |                                                                     ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:47:82
    |
 47 |                         result = f"First ok ({a.value}), second failed: {b.error}"
    |                                                                                  ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Error' and no accessible extension method 'Error' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:47:111
    |
 47 |                         result = f"First ok ({a.value}), second failed: {b.error}"
    |                                                                                   ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Error' and no accessible extension method 'Error' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:51:86
    |
 51 |                         result = f"First failed: {a.error}, second ok ({b.value})"
    |                                                                                   ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:51:110
    |
 51 |                         result = f"First failed: {a.error}, second ok ({b.value})"
    |                                                                                   ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Error' and no accessible extension method 'Error' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:53:85
    |
 53 |                         result = f"Both failed: {a.error} and {b.error}"
    |                                                                         ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Error' and no accessible extension method 'Error' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:53:101
    |
 53 |                         result = f"Both failed: {a.error} and {b.error}"
    |                                                                         ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:69:67
    |
 69 |             s1 = f"positive: {r1.value}"
    |                                         ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Error' and no accessible extension method 'Error' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:71:64
    |
 71 |             s1 = f"error: {r1.error}"
    |                                      ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:77:67
    |
 77 |             s2 = f"positive: {r2.value}"
    |                                         ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Error' and no accessible extension method 'Error' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:79:64
    |
 79 |             s2 = f"error: {r2.error}"
    |                                      ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:85:63
    |
 85 |             s4 = f"even: {r4.value}"
    |                                     ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Error' and no accessible extension method 'Error' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:87:64
    |
 87 |             s4 = f"error: {r4.error}"
    |                                      ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:97:68
    |
 97 |             s_c1 = f"combined: {c1.value}"
    |                                           ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Error' and no accessible extension method 'Error' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:99:66
    |
 99 |             s_c1 = f"failed: {c1.error}"
    |                                         ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:105:68
     |
 105 |             s_c2 = f"combined: {c2.value}"
     |                                           ^
     |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Error' and no accessible extension method 'Error' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:107:66
     |
 107 |             s_c2 = f"failed: {c2.error}"
     |                                         ^
     |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:29:69
    |
 29 |                         result = Result2.Ok(r1.value * r2.value)
    |                                                                 ^
    |

error[CS1061]: 'DogfoodTest.Result2<int, string>' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'DogfoodTest.Result2<int, string>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpbemd11cl/dogfood_test.spy:29:80
    |
 29 |                         result = Result2.Ok(r1.value * r2.value)
    |                                                                 ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpbemd11cl/dogfood_test.cs

```

## Timing

- Generation: 701.55s
- Execution: 4.53s
