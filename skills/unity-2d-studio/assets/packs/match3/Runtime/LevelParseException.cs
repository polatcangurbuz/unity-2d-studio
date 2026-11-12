using System;

namespace __NS__.Puzzle
{
    public sealed class LevelParseException : FormatException
    {
        public LevelParseException(int line, string message)
            : base(line > 0 ? $"line {line}: {message}" : message)
        {
            Line = line;
        }

        public int Line { get; }
    }
}
