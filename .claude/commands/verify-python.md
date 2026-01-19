# Verify Python Behavior

Verify Python 3 behavior before implementing Sharpy semantics.

## Expression or Behavior

$ARGUMENTS

## Verification Commands

```bash
# Simple expression
python3 -c "print($ARGUMENTS)"

# Multi-line code
python3 << 'EOF'
# Your code here
EOF
```

## Common Verifications

```bash
# Division semantics
python3 -c "print(7 // 2)"      # Floor division
python3 -c "print(-7 // 2)"     # Negative floor division

# List operations
python3 -c "print([1,2,3].pop())"
python3 -c "x = [1,2,3]; x.insert(1, 99); print(x)"
python3 -c "print([1,2,3][-1])"  # Negative indexing

# Slicing
python3 -c "print([1,2,3,4,5][1:3])"
python3 -c "print([1,2,3,4,5][::2])"
python3 -c "print([1,2,3,4,5][::-1])"

# Truthiness
python3 -c "print(bool([]))"
python3 -c "print(bool(''))"
python3 -c "print(bool(0))"
```

## Important Note

When Python and .NET semantics conflict, Sharpy follows .NET semantics unless zero-cost abstraction is possible. Document the difference when implementing.

## Decision Framework

| Scenario | Sharpy Behavior |
|----------|-----------------|
| Can match Python exactly | Match Python |
| Python semantics expensive | Use .NET, provide helper function |
| Python semantics impossible | Use .NET, document in spec |
