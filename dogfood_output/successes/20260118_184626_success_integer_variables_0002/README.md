# Successful Dogfood Run

**Timestamp:** 2026-01-18T18:46:13.122198
**Feature Focus:** integer_variables
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test integer variables with a simple game scoring system

class GameScore:
    player_points: int
    bonus_multiplier: int
    penalty: int

    def __init__(self, initial_points: int):
        self.player_points = initial_points
        self.bonus_multiplier = 1
        self.penalty = 0

    def add_kill(self, points: int) -> None:
        self.player_points += points * self.bonus_multiplier

    def add_bonus_multiplier(self) -> None:
        self.bonus_multiplier += 1

    def apply_penalty(self, amount: int) -> None:
        self.penalty += amount

    def get_final_score(self) -> int:
        return self.player_points - self.penalty

def calculate_rank(score: int) -> int:
    if score >= 100:
        return 3
    elif score >= 50:
        return 2
    else:
        return 1

# Initialize game with starting points
game = GameScore(10)
print(game.player_points)

# Player gets first kill
game.add_kill(15)
print(game.get_final_score())

# Player gets bonus multiplier
game.add_bonus_multiplier()
print(game.bonus_multiplier)

# Player gets second kill with multiplier
game.add_kill(20)
print(game.get_final_score())

# Apply death penalty
game.apply_penalty(10)
final = game.get_final_score()
print(final)

# Calculate player rank
rank = calculate_rank(final)
print(rank)

# EXPECTED OUTPUT:
# 10
# 25
# 2
# 65
# 55
# 2
```

## Output

```
10
25
2
65
55
2
```

## Timing

- Generation: 5.68s
- Execution: 1.35s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
