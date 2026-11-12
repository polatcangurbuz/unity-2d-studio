namespace __NS__.Puzzle
{
    public sealed class GreedyBot : IBot
    {
        public string Name => "greedy";

        public void Begin(int seed)
        {
        }

        public bool TryChoose(PuzzleGame game, out Swap move) => MoveFinder.TryGetBest(game, out move);
    }
}
