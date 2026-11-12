namespace __NS__.Puzzle
{
    public interface IBot
    {
        string Name { get; }

        void Begin(int seed);

        bool TryChoose(PuzzleGame game, out Swap move);
    }
}
