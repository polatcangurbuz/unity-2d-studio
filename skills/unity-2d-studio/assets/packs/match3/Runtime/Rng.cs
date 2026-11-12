namespace __NS__.Puzzle
{
    public sealed class Rng
    {
        private uint _state;

        public Rng(int seed)
        {
            // Hash the seed so neighbouring seeds (seed + run index) give unrelated streams.
            uint z = unchecked((uint)seed * 0x9E3779B1u);
            z ^= z >> 16;
            z = unchecked(z * 0x85EBCA6Bu);
            z ^= z >> 13;
            z = unchecked(z * 0xC2B2AE35u);
            z ^= z >> 16;
            _state = z == 0 ? 0x6D2B79F5u : z;
        }

        private Rng(uint state)
        {
            _state = state;
        }

        public uint State => _state;

        public Rng Clone() => new Rng(_state);

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        public int Next(int maxExclusive) => maxExclusive <= 1 ? 0 : (int)(NextUInt() % (uint)maxExclusive);
    }
}
