using System.Collections.Generic;
using System.Text;

namespace __NS__.Puzzle
{
    public struct Step
    {
        public StepKind Kind;
        public int Wave;
        public BoardPos A;
        public BoardPos B;
        public int PieceId;
        public int Color;
        public Special Special;
        public Special Partner;
        public int Value;
        public int Index;
        public ClearCause Cause;
        public BlockerLayer Layer;
        public IReadOnlyList<BoardPos> Cells;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Wave).Append(':').Append(Kind).Append(" a").Append(A).Append(" b").Append(B)
                .Append(" id").Append(PieceId).Append(" c").Append(Color).Append(' ').Append(Special)
                .Append('/').Append(Partner).Append(" v").Append(Value).Append(" i").Append(Index)
                .Append(' ').Append(Cause).Append(' ').Append(Layer);
            if (Cells != null)
            {
                sb.Append(" [");
                foreach (BoardPos c in Cells) sb.Append(c);
                sb.Append(']');
            }
            return sb.ToString();
        }
    }
}
