# Successful Dogfood Run

**Timestamp:** 2026-01-29T22:10:32.177370
**Feature Focus:** f_string_expressions
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Test f-string expressions with a game scoring system
# Tests: f-strings with arithmetic, method calls, property access, conditionals

@abstract
class Player:
    name: str
    base_score: int
    multiplier: float

    def __init__(self, name: str, score: int):
        self.name = name
        self.base_score = score
        self.multiplier = 1.0

    @virtual
    def get_final_score(self) -> int:
        return int(self.base_score * self.multiplier)

    @abstract
    def get_rank(self) -> str:
        ...

class CompetitivePlayer(Player):
    bonus_points: int
    level: int

    def __init__(self, name: str, score: int, bonus: int, level: int):
        super().__init__(name, score)
        self.bonus_points = bonus
        self.level = level
        self.multiplier = 1.5

    @override
    def get_final_score(self) -> int:
        return int((self.base_score + self.bonus_points) * self.multiplier)

    @override
    def get_rank(self) -> str:
        if self.get_final_score() >= 150:
            return "Master"
        if self.get_final_score() >= 100:
            return "Expert"
        return "Novice"

class CasualPlayer(Player):
    games_played: int

    def __init__(self, name: str, score: int, games: int):
        super().__init__(name, score)
        self.games_played = games
        self.multiplier = 1.2

    @override
    def get_rank(self) -> str:
        if self.games_played > 10:
            return "Veteran"
        return "Beginner"

def main():
    competitive = CompetitivePlayer("Alice", 80, 25, 12)
    casual = CasualPlayer("Bob", 75, 8)
    
    print(f"Player: {competitive.name}, Level: {competitive.level}")
    print(f"Base: {competitive.base_score}, Bonus: {competitive.bonus_points}")
    print(f"Score: {competitive.get_final_score()} ({competitive.multiplier}x multiplier)")
    print(f"Rank: {competitive.get_rank()}, Status: {competitive.get_final_score() > 100}")
    
    print(f"Player: {casual.name}, Games: {casual.games_played}")
    print(f"Average: {casual.base_score // casual.games_played} per game")
    print(f"Total Score: {casual.get_final_score()}")
    print(f"Rank: {casual.get_rank()}, Next level at: {11 - casual.games_played} games")

# EXPECTED OUTPUT:
# Player: Alice, Level: 12
# Base: 80, Bonus: 25
# Score: 157 (1.5x multiplier)
# Rank: Master, Status: True
# Player: Bob, Games: 8
# Average: 9 per game
# Total Score: 90
# Rank: Beginner, Next level at: 3 games
```

## Output

```
Player: Alice, Level: 12
Base: 80, Bonus: 25
Score: 157 (1.5x multiplier)
Rank: Master, Status: True
Player: Bob, Games: 8
Average: 9 per game
Total Score: 90
Rank: Beginner, Next level at: 3 games
```

## Timing

- Generation: 14.51s
- Execution: 1.67s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
