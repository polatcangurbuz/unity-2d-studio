using System.Collections.Generic;

namespace __NS__.Puzzle
{
    internal sealed class StepLog
    {
        public List<Step> Steps { get; } = new List<Step>();

        public int Wave { get; set; }

        public void Add(Step step)
        {
            step.Wave = Wave;
            Steps.Add(step);
        }
    }
}
