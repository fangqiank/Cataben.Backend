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

        public TestCase(
            string name, 
            string input, 
            string expectedOutput, 
            bool isPublic = true, 
            int weight = 1, 
            int order = 0): this()
        {
            Name = name;
            Input = input;
            ExpectedOutput = expectedOutput;
            IsPublic = isPublic;
            Weight = weight;
            Order = order;
        }
    }

    public class HiddenTest: TestCase
    {
        public string ValidationType { get; private set; } = "exact";
        public int MinScore { get; private set; }
        public TimeSpan? MaxExecutionTime { get; private set; }
        public long? MaxMemoryUsage { get; private set; }

        public HiddenTest(
            string name, 
            string input, 
            string expectedOutput, 
            string validationType = "exact", 
            int minScore = 100)
            : base(name, input, expectedOutput, false)
        {
            ValidationType = validationType;
            MinScore = minScore;
        }
    }
}
