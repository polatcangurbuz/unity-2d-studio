using System.Collections.Generic;

namespace __NS__.Puzzle
{
    public sealed class RandomBot : IBot
    {
        private Rng _rng = new Rng(1);

        public string Name => "random";

        // Offset the seed so the bot's choices do not mirror the board's spawn stream.
        public void Begin(int seed) => _rng = new Rng(seed ^ 0x5BD1E995);

        public bool TryChoose(PuzzleGame game, out Swap move)
        {
            List<Swap> moves = MoveFinder.FindAll(game.Board);
            move = moves.Count > 0 ? moves[_rng.Next(moves.Count)] : default;
            return moves.Count > 0;
        }
    }
}
