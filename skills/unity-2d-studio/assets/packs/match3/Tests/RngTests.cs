using NUnit.Framework;

namespace __NS__.Puzzle.Tests
{
    public class RngTests
    {
        [Test]
        public void Next_SameSeed_SameSequence()
        {
            var a = new Rng(42);
            var b = new Rng(42);

            for (int i = 0; i < 100; i++) Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
        }

        [Test]
        public void Next_NeighbouringSeeds_DifferentFirstValues()
        {
            var a = new Rng(1);
            var b = new Rng(2);

            Assert.That(a.NextUInt(), Is.Not.EqualTo(b.NextUInt()));
        }

        [Test]
        public void Next_KnownSeed_MatchesPinnedValue()
        {
            var rng = new Rng(1234);

            uint[] values = { rng.NextUInt(), rng.NextUInt(), rng.NextUInt() };

            // Pinned (computed independently) so a platform or refactor change that breaks replays shows up here.
            Assert.That(values, Is.EqualTo(new[] { 2055686353u, 3761832703u, 3434535756u }));
        }

        [Test]
        public void Next_AnyMax_StaysInRange()
        {
            var rng = new Rng(7);

            for (int i = 0; i < 1000; i++) Assert.That(rng.Next(5), Is.InRange(0, 4));
        }

        [Test]
        public void Clone_AfterDraws_ContinuesIdentically()
        {
            var rng = new Rng(9);
            rng.NextUInt();

            Rng copy = rng.Clone();

            Assert.That(copy.NextUInt(), Is.EqualTo(rng.NextUInt()));
        }
    }
}
