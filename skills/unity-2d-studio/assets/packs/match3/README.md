# match3 pack

Deterministic match-3 core in pure C# (no UnityEngine). `Runtime/` = core, `Tests/` = NUnit EditMode tests,
`Tests~/` runs the same tests on plain .NET, `Sim~/` = bot simulator, `Levels~/` = example levels.

```
dotnet test Tests~
dotnet run --project Sim~ -- Levels~/001_easy.txt --runs 500 --bot greedy|random [--seed 1]
dotnet run --project Sim~ -- Levels~/*.txt        # one summary line per level (difficulty curve)
```

## Level format (`LevelParser.Parse`)
```
moves: 25            // 1..999
colors: 5            // 3..6, color index order: red green blue yellow purple orange
seed: 1234           // optional, default 1
goals: red=20 crate=10 ice=6 chain=4
grid:
. . . . .
. C2 C2 I1 .
# # L 3 2H
```
`//` starts a comment. Row 0 is the top; pieces fall toward the last row.
- `.` cell, `#` hole (no cell; pieces fall across it), `C<n>` crate hp n (no piece, blocks gravity, hit by adjacent matches),
  `I<n>` ice under the piece (hit when that piece clears), `L` / `L<n>` chain on the piece (no swap, no fall).
- Optional preset piece: `<cell>:<color>[H|V|B]` or `<cell>:*` (colorbomb); a bare `3` / `2H` / `*` means `.:3`.
- Goals count destroyed crates/ice/chains (not hp) and cleared pieces of a color.

## Rules
- 4 in a row (horizontal line) -> RocketV, 4 in a column -> RocketH (rocket fires across its line). L/T -> Bomb, 5+ -> ColorBomb.
- Specials appear at the swapped cell when it is in the group, else at the run centre / L corner.
- Swapping a special with anything fires it (no match needed); two specials combo at the drop cell.
- A match or blast on a chained piece only breaks the chain (one hp); the piece stays.
- Only accepted swaps spend a move; rejected swaps and boosters do not.
- No valid move after a turn -> deterministic shuffle (chained pieces stay).

## View integration
`PuzzleGame.TrySwap` / `UseBooster` return a `TurnResult` whose ordered `Steps` the view animates; `Step.Wave`
is the cascade depth (time waves, raise combo pitch). Field meanings per kind are in `StepKind.cs`.
Same seed + same moves -> identical steps on every platform (`Rng` is a seeded xorshift).
