using Cataben.Domain.Enums;
using Cataben.Domain.ValueObjects;

namespace Cataben.Domain.Entities
{
    public class Challenge
    {
        public Guid Id { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public ChallengeType Type { get; private set; }
        public Difficulty Difficulty { get; private set; } = Difficulty.Easy;
        public string Category { get; private set; } = string.Empty;
        public string InitialCode { get; private set; } = string.Empty;
        public string SolutionCode { get; private set; } = string.Empty;

        // Hints persisted as a JSON array (see ChallengeConfiguration). Empty by default.
        public List<string> Hints { get; private set; } = new();
        public int XpReward { get; private set; }
        public int GemReward { get; private set; }
        public int TimeLimitSeconds { get; private set; } = 10;
        public int MemoryLimitMb { get; private set; } = 256;
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public Guid? CreatedBy { get; private set; }
        public Guid? LearningPathId { get; private set; }
        public int OrderInPath { get; private set; }

        public string? DatabaseSchema { get; private set; }
        public string? SeedDataScript { get; private set; }
        public string? OptimalQuery { get; private set; }

        private readonly List<TestCase> _testCases = new();
        public IReadOnlyCollection<TestCase> TestCases => _testCases.AsReadOnly();

        private readonly List<HiddenTest> _hiddenTests = new();
        public IReadOnlyCollection<HiddenTest> HiddenTests => _hiddenTests.AsReadOnly();

        private Challenge() { }

        public Challenge(string title, string description, ChallengeType type, Difficulty difficulty, string category)
        {
            Id = Guid.NewGuid();
            Title = title;
            Description = description;
            Type = type;
            Difficulty = difficulty;
            Category = category;
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
        }

        public void AddTestCase(TestCase testCase)
        {
            _testCases.Add(testCase);
        }

        public void AddHiddenTest(HiddenTest hiddenTest)
        {
            _hiddenTests.Add(hiddenTest);
        }

        /// <summary>Clears the public test-case set (used by seeding to normalize legacy data).</summary>
        public void ClearTestCases()
        {
            _testCases.Clear();
        }

        public void SetSolution(string solutionCode)
        {
            SolutionCode = solutionCode;
        }

        public void Update(
            string title,
            string description,
            ChallengeType type,
            Difficulty difficulty,
            string category)
        {
            Title = title;
            Description = description;
            Type = type;
            Difficulty = difficulty;
            Category = category;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetActive(bool active)
        {
            IsActive = active;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetInitialCode(string code)
        {
            InitialCode = code;
        }

        public void SetHints(IEnumerable<string> hints)
        {
            Hints = hints?.ToList() ?? new List<string>();
        }

        public void SetDatabaseSchema(string schema, string seedData, string? optimalQuery = null)
        {
            DatabaseSchema = schema;
            SeedDataScript = seedData;
            OptimalQuery = optimalQuery;
        }

        public void UpdateRewards(int xp, int gems)
        {
            XpReward = xp;
            GemReward = gems;
        }

        public void UpdateLimits(int timeLimitSeconds, int memoryLimitMb)
        {
            TimeLimitSeconds = timeLimitSeconds;
            MemoryLimitMb = memoryLimitMb;
        }

        public void Publish()
        {
            IsActive = true;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Unpublish()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetOrderInPath(int order)
        {
            OrderInPath = order;
        }

        public void SetLearningPath(Guid? learningPathId)
        {
            LearningPathId = learningPathId;
        }
    }
}
