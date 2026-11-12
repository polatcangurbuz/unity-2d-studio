using NUnit.Framework;

namespace __NS__.Puzzle.Tests
{
    public class LevelParserTests
    {
        private const string Valid =
            "moves: 25\ncolors: 5\nseed: 1234\ngoals: red=20 crate=10 ice=6\ngrid:\n. . . .\n. C2 I1 .\n# # . L\n";

        [Test]
        public void Parse_ValidText_ReadsHeader()
        {
            Level level = LevelParser.Parse(Valid);

            Assert.That(new[] { level.Moves, level.Colors, level.Seed }, Is.EqualTo(new[] { 25, 5, 1234 }));
        }

        [Test]
        public void Parse_ValidText_ReadsGoalsInOrder()
        {
            Level level = LevelParser.Parse(Valid);

            Assert.That(level.Goals, Is.EqualTo(new[]
            {
                new Goal(GoalKind.Color, 0, 20), new Goal(GoalKind.Crate, -1, 10), new Goal(GoalKind.Ice, -1, 6),
            }));
        }

        [Test]
        public void Parse_ValidText_ReadsGridSize()
        {
            Level level = LevelParser.Parse(Valid);

            Assert.That(new[] { level.Width, level.Height }, Is.EqualTo(new[] { 4, 3 }));
        }

        [Test]
        public void Parse_CellTokens_SetLayers()
        {
            Board b = LevelParser.Parse(Valid).Layout;

            Assert.That(b.CrateHp(StepQuery.P(1, 1)), Is.EqualTo(2));
            Assert.That(b.IceHp(StepQuery.P(2, 1)), Is.EqualTo(1));
            Assert.That(b.IsHole(StepQuery.P(0, 2)), Is.True);
            Assert.That(b.ChainHp(StepQuery.P(3, 2)), Is.EqualTo(1));
        }

        [Test]
        public void Parse_PresetPieceTokens_SetColorAndSpecial()
        {
            Board b = LevelParser.Parse("moves: 5\ncolors: 4\ngoals: red=1\ngrid:\n2V I1:3 * L:0\n").Layout;

            Assert.That(b.PieceAt(StepQuery.P(0, 0)).Special, Is.EqualTo(Special.RocketV));
            Assert.That(b.PieceAt(StepQuery.P(1, 0)).Color, Is.EqualTo(3));
            Assert.That(b.PieceAt(StepQuery.P(2, 0)).Special, Is.EqualTo(Special.ColorBomb));
            Assert.That(b.ChainHp(StepQuery.P(3, 0)), Is.EqualTo(1));
        }

        [Test]
        public void Parse_UnknownCellToken_ThrowsWithLineNumber()
        {
            const string text = "moves: 5\ncolors: 4\ngoals: red=1\ngrid:\n. . .\n. X3 .\n";

            var ex = Assert.Throws<LevelParseException>(() => LevelParser.Parse(text));

            Assert.That(ex.Line, Is.EqualTo(6));
            StringAssert.Contains("line 6", ex.Message);
        }

        [Test]
        public void Parse_RaggedRow_ThrowsWithLineNumber()
        {
            const string text = "moves: 5\ncolors: 4\ngoals: red=1\ngrid:\n. . .\n. .\n";

            var ex = Assert.Throws<LevelParseException>(() => LevelParser.Parse(text));

            Assert.That(ex.Line, Is.EqualTo(6));
        }

        [Test]
        public void Parse_UnknownGoal_ThrowsWithGoalsLine()
        {
            const string text = "moves: 5\ncolors: 4\n\ngoals: gems=3\ngrid:\n. . .\n";

            var ex = Assert.Throws<LevelParseException>(() => LevelParser.Parse(text));

            Assert.That(ex.Line, Is.EqualTo(4));
        }

        [Test]
        public void Parse_GoalColorBeyondColorCount_Throws()
        {
            const string text = "moves: 5\ncolors: 3\ngoals: purple=3\ngrid:\n. . .\n";

            Assert.Throws<LevelParseException>(() => LevelParser.Parse(text));
        }

        [Test]
        public void Parse_TrailingComments_Ignored()
        {
            const string text = "// intro\nmoves: 9   // budget\ncolors: 4\ngoals: red=1\ngrid:\n. . .  // top row\n";

            Level level = LevelParser.Parse(text);

            Assert.That(new[] { level.Moves, level.Width }, Is.EqualTo(new[] { 9, 3 }));
        }

        [Test]
        public void Parse_MissingGrid_Throws()
        {
            const string text = "moves: 5\ncolors: 4\ngoals: red=3\n";

            var ex = Assert.Throws<LevelParseException>(() => LevelParser.Parse(text));

            StringAssert.Contains("grid", ex.Message);
        }
    }
}
