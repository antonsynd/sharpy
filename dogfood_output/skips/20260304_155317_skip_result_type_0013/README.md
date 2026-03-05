# Skipped Dogfood Run

**Timestamp:** 2026-03-04T15:46:46.121151
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

Type errors:
error[SPY0202]: Unknown type 'Ok' in positional pattern
  --> /tmp/tmp_qko06ab/dogfood_test.spy:65:14
    |
 65 |         case Ok(v):
    |              ^^^^^
    |

error[SPY0202]: Unknown type 'Err' in positional pattern
  --> /tmp/tmp_qko06ab/dogfood_test.spy:67:14
    |
 67 |         case Err(e):
    |              ^^^^^^
    |

error[SPY0202]: Unknown type 'Ok' in positional pattern
  --> /tmp/tmp_qko06ab/dogfood_test.spy:77:14
    |
 77 |         case Ok(s):
    |              ^^^^^
    |

error[SPY0202]: Unknown type 'Err' in positional pattern
  --> /tmp/tmp_qko06ab/dogfood_test.spy:79:14
    |
 79 |         case Err(code):
    |              ^^^^^^^^^
    |

error[SPY0202]: Unknown type 'Ok' in positional pattern
  --> /tmp/tmp_qko06ab/dogfood_test.spy:95:14
    |
 95 |         case Ok(val):
    |              ^^^^^^^
    |

error[SPY0222]: Type 'int !str' does not support operator '+' with operand of type 'int'
  --> /tmp/tmp_qko06ab/dogfood_test.spy:96:23
    |
 96 |             return Ok(val + offset)
    |                       ^^^^^^^^^^^^
    |

error[SPY0202]: Unknown type 'Err' in positional pattern
  --> /tmp/tmp_qko06ab/dogfood_test.spy:97:14
    |
 97 |         case Err(e):
    |              ^^^^^^
    |

error[SPY0220]: Argument type 'int !str' is not compatible with Result Error type 'str'
  --> /tmp/tmp_qko06ab/dogfood_test.spy:98:24
    |
 98 |             return Err(e)
    |                        ^
    |

error[SPY0202]: Type 'Ok' not found
  --> /tmp/tmp_qko06ab/dogfood_test.spy:65:14
    |
 65 |         case Ok(v):
    |              ^^
    |

error[SPY0202]: Type 'Err' not found. Did you mean 'str'?
  --> /tmp/tmp_qko06ab/dogfood_test.spy:67:14
    |
 67 |         case Err(e):
    |              ^^^
    |

error[SPY0202]: Type 'Ok' not found
  --> /tmp/tmp_qko06ab/dogfood_test.spy:77:14
    |
 77 |         case Ok(s):
    |              ^^
    |

error[SPY0202]: Type 'Err' not found. Did you mean 'str'?
  --> /tmp/tmp_qko06ab/dogfood_test.spy:79:14
    |
 79 |         case Err(code):
    |              ^^^
    |

error[SPY0202]: Type 'Ok' not found
  --> /tmp/tmp_qko06ab/dogfood_test.spy:95:14
    |
 95 |         case Ok(val):
    |              ^^
    |

error[SPY0202]: Type 'Err' not found. Did you mean 'str'?
  --> /tmp/tmp_qko06ab/dogfood_test.spy:97:14
    |
 97 |         case Err(e):
    |              ^^^
    |

Validation errors:
error[SPY0266]: Function 'process_chain' must return a value of type 'int !str' in all code paths
  --> /tmp/tmp_qko06ab/dogfood_test.spy:90:1
    |
 90 | def process_chain(n: int, offset: int) -> int !str:
    | ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


**Feature Focus:** result_type
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
def divide(a: float, b: float) -> float !str:
    if b == 0.0:
        return Err("Cannot divide by zero")
    return Ok(a / b)

def parse_number(s: str) -> float !str:
    if s == "invalid":
        return Err("Parsing failed")
    return Ok(100.0)

def perform_calculation(x: float, y: float) -> str !int:
    # This returns an Err with int error code
    if x < 0.0:
        return Err(1)
    if y < 0.0:
        return Err(2)
    return Ok("Success")

def get_data(fail: bool) -> str !bool:
    # Returns string or fails with bool flag
    if fail:
        return Err(True)
    return Ok("Data loaded")

def process_value(n: int) -> int !str:
    if n < 0:
        return Err("Negative value")
    if n > 100:
        return Err("Value too large")
    return Ok(n * 2)

def safe_sqrt(n: float) -> float !str:
    if n < 0.0:
        return Err("Square root of negative")
    if n == 0.0:
        return Err("Zero is special")
    return Ok(n)

def check_division_results() -> None:
    # Test divide with various inputs
    result1: float !str = divide(10.0, 2.0)
    print(result1.unwrap())
    
    result2: float !str = divide(10.0, 0.0)
    print(result2.unwrap_or(0.0))
    
    # Test with map
    mapped: float !str = result1.map(lambda v: v * 2.0)
    print(mapped.unwrap_or(0.0))
    
    # Test map_err
    error_mapped: float !str = result2.map_err(lambda e: f"Error: {e}")
    print(error_mapped.unwrap_or(0.0))

def check_parse_results() -> None:
    # Test parse_number
    p1: float !str = parse_number("123")
    print(p1.unwrap())
    
    p2: float !str = parse_number("invalid")
    print(p2.unwrap_or(-1.0))
    
    # Chain results with match
    match p1:
        case Ok(v):
            print(v)
        case Err(e):
            print(e)

def check_calculation_results() -> None:
    # Test perform_calculation with int error codes
    c1: str !int = perform_calculation(5.0, 10.0)
    print(c1.unwrap())
    
    c2: str !int = perform_calculation(-5.0, 10.0)
    match c2:
        case Ok(s):
            print(s)
        case Err(code):
            print(f"Error code {code}")

def check_data_results() -> None:
    # Test get_data with bool error
    d1: str !bool = get_data(False)
    d2: str !bool = get_data(True)
    
    print(d1.unwrap())
    print(d2.unwrap_or("fallback"))

def process_chain(n: int, offset: int) -> int !str:
    processed: int !str = process_value(n)
    
    # Use match to handle result
    match processed:
        case Ok(val):
            return Ok(val + offset)
        case Err(e):
            return Err(e)

def check_sqrt_results() -> None:
    # Test safe_sqrt with different values
    s1: float !str = safe_sqrt(16.0)
    s2: float !str = safe_sqrt(-4.0)
    
    print(s1.unwrap())
    print(s2.unwrap_or(0.0))
    
    # Test with map transformation
    doubled: float !str = s1.map(lambda v: v * 2.0)
    print(doubled.unwrap_or(0.0))

def main() -> None:
    # Run all Result type tests
    check_division_results()
    check_parse_results()
    check_calculation_results()
    check_data_results()
    
    # Test process_chain
    chain1: int !str = process_chain(25, 10)
    chain2: int !str = process_chain(-5, 10)
    chain3: int !str = process_chain(150, 10)
    
    print(chain1.unwrap())
    print(chain2.unwrap_or(0))
    print(chain3.unwrap_or(0))
    
    check_sqrt_results()
    
    # Final summary
    print("Result type tests complete")

```

## Timing

- Generation: 374.51s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
