using System.Collections.Generic;

namespace __NS__.Puzzle
{
    internal sealed class MatchGroup
    {
        public MatchGroup(int color)
        {
            Color = color;
        }

        public int Color { get; }

        public List<BoardPos> Cells { get; } = new List<BoardPos>();

        public Special Created { get; set; }

        public BoardPos CreatePos { get; set; }
    }
}
