namespace __NS__.Puzzle
{
    public enum StepKind
    {
        // A, B = swapped cells; PieceId = dragged piece, Value = the other piece id.
        Swapped,
        // A, B = cells that swapped and swapped back; no move spent.
        SwapRejected,
        // Cells = group, Color = group color, Special = special created at A with PieceId (0 = none).
        Matched,
        // A = cell; PieceId, Color, Special = the removed piece; Cause = why.
        Cleared,
        // A = cell, Layer = crate/ice/chain, Value = hp left (0 = destroyed).
        BlockerHit,
        // A = origin, Special (+ Partner on combos), Cells = cells hit, Color = targeted color or -1.
        SpecialFired,
        // A = cell; PieceId keeps its id and becomes Special (colorbomb combo).
        Transformed,
        // PieceId moved from A to B (straight or diagonal).
        Fell,
        // PieceId with Color spawned at A, entering Value rows above the column's top cell.
        Spawned,
        // PieceId moved from A to B by a shuffle.
        Shuffled,
        // Index = goal index, Value = new progress, Color = color for color goals.
        GoalProgressed,
        // Value = 1 won, 0 lost.
        LevelEnded,
    }
}
