namespace Cataben.Domain.ValueObjects
{
    public class TestCase()
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public string Name { get; private set; } = string.Empty;
        public string Input { get; private set; } = string.Empty;
        public string ExpectedOutput { get; private set; } = string.Empty;
        public bool IsPublic { get; private set; } = true;
        public int Weight { get; private set; } = 1;
        public int Order { get; private set; }
        // How this case's output is compared to ExpectedOutput: exact | contains | regex |
        // json | loose | ai. Defaults to "exact" so existing seed data/callers are unchanged.
        public string ValidationType { get; private set; } = "exact";

        public TestCase(
            string name,
            string input,
            string expectedOutput,
            bool isPublic = true,
            int weight = 1,
            int order = 0,
            string validationType = "exact") : this()
        {
            Name = name;
            Input = input;
            ExpectedOutput = expectedOutput;
            IsPublic = isPublic;
            Weight = weight;
            Order = order;
            ValidationType = string.IsNullOrWhiteSpace(validationType) ? "exact" : validationType;
        }
    }

    public class HiddenTest : TestCase
    {
        public int MinScore { get; private set; }
        public TimeSpan? MaxExecutionTime { get; private set; }
        public long? MaxMemoryUsage { get; private set; }

        public HiddenTest(
            string name,
            string input,
            string expectedOutput,
            string validationType = "exact",
            int minScore = 100)
            // Hidden tests are always private. validationType now lives on the base
            // class, so pass it through instead of shadowing it here (the old
            // `new`/hide caused CS0108 and let base/derived disagree).
            : base(name, input, expectedOutput, isPublic: false, validationType: validationType)
        {
            MinScore = minScore;
        }
    }
}
