# Skipped Dogfood Run

**Timestamp:** 2026-03-10T01:48:22.777592
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0103]: Expected end of statement, got Identifier
  --> /tmp/tmpl87ona2y/dogfood_test.spy:76:5
    |
 76 |     i: int = 0
    |     ^
    |

error[SPY0103]: Expected end of statement, got Identifier
  --> /tmp/tmpl87ona2y/dogfood_test.spy:96:5
    |
 96 |     i: int = 0
    |     ^
    |


**Feature Focus:** match_wildcard
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: match_wildcard - complex wildcard patterns with enumeration
@abstract
class Token:
    @abstract
    def kind(self) -> str:
        ...

class NumberToken(Token):
    _value: float
    def __init__(self, val: float):
        self._value = val

    @override
    def kind(self) -> str:
        return "number"

class IdentifierToken(Token):
    _name: str
    def __init__(self, name: str):
        self._name = name

    @override
    def kind(self) -> str:
        return "identifier"

class OperatorToken(Token):
    _symbol: str
    def __init__(self, sym: str):
        self._symbol = sym

    @override
    def kind(self) -> str:
        return "operator"

class UnknownToken(Token):
    @override
    def kind(self) -> str:
        return "unknown"

enum TokenCategory:
    LITERAL = 1
    NAMED = 2
    SYMBOL = 3
    OTHER = 4

def categorize(token: Token) -> TokenCategory:
    match token:
        case NumberToken():
            return TokenCategory.LITERAL
        case IdentifierToken():
            return TokenCategory.NAMED
        case OperatorToken():
            return TokenCategory.SYMBOL
        case _:
            return TokenCategory.OTHER

def analyze_token(t: Token) -> str:
    cat = categorize(t)
    kind = t.kind()
    if cat == TokenCategory.LITERAL:
        return "literal token"
    elif cat == TokenCategory.NAMED:
        return "named token"
    elif kind == "operator":
        return "symbol with operator kind"
    else:
        return "unrecognized token"

def test_enum_wildcard():
    tokens = [
        NumberToken(3.14),
        IdentifierToken("x"),
        OperatorToken("+"),
        UnknownToken()
    ]
    i: int = 0
    while i < 4:
        t = tokens[i]
        cat = categorize(t)
        match cat:
            case TokenCategory.LITERAL:
                print("category: literal")
            case TokenCategory.NAMED:
                print("category: named")
            case _:
                print("category: other")
        i += 1

def test_categorize_results():
    tokens = [
        NumberToken(3.14),
        IdentifierToken("x"),
        OperatorToken("+"),
        UnknownToken()
    ]
    i: int = 0
    while i < 4:
        t = tokens[i]
        result = analyze_token(t)
        print(result)
        i += 1

def main():
    test_enum_wildcard()
    test_categorize_results()

```

## Timing

- Generation: 751.00s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
