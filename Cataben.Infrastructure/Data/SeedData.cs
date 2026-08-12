using Cataben.Domain.Entities;
using Cataben.Domain.Enums;
using Cataben.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Cataben.Infrastructure.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext context, IConfiguration configuration)
    {
        // Add default achievements
        if (!await context.Achievements.AnyAsync())
        {
            var achievements = new List<Achievement>
            {
                new Achievement("first_code", "First Steps", "Write your first code", AchievementCategory.General, AchievementRarity.Common, 1, AchievementType.Count, "🚀"),
                new Achievement("first_challenge", "Challenge Accepted", "Complete your first challenge", AchievementCategory.Challenges, AchievementRarity.Common, 1, AchievementType.Count, "🎯"),
                new Achievement("code_10", "Getting Started", "Complete 10 challenges", AchievementCategory.Challenges, AchievementRarity.Uncommon, 10, AchievementType.Count, "📝"),
                new Achievement("code_50", "Dedicated Coder", "Complete 50 challenges", AchievementCategory.Challenges, AchievementRarity.Rare, 50, AchievementType.Count, "✍️"),
                new Achievement("streak_7", "Weekly Warrior", "Log in for 7 consecutive days", AchievementCategory.Streak, AchievementRarity.Common, 7, AchievementType.Streak, "📅"),
                new Achievement("streak_30", "Monthly Master", "Log in for 30 consecutive days", AchievementCategory.Streak, AchievementRarity.Uncommon, 30, AchievementType.Streak, "📆"),
            };

            await context.Achievements.AddRangeAsync(achievements);
            await context.SaveChangesAsync();
        }

        // Add sample learning paths
        if (!await context.LearningPaths.AnyAsync())
        {
            var beginnerPath = new LearningPath("C# Fundamentals", "Learn the basics of C# programming", "Beginner");
            var intermediatePath = new LearningPath("Advanced C#", "Master advanced C# concepts", "Intermediate");
            // Publish the sample paths so the default catalog (onlyPublished=true) is non-empty out of the box.
            beginnerPath.Publish();
            intermediatePath.Publish();

            await context.LearningPaths.AddRangeAsync(beginnerPath, intermediatePath);
            await context.SaveChangesAsync();
        }

        // Add sample challenges
        if (!await context.Challenges.AnyAsync())
        {
            var helloWorld = new Challenge("Hello World", "Write a program that prints 'Hello World'", ChallengeType.Algorithm, Difficulty.Beginner, "Basics");
            helloWorld.SetHints(new List<string> { "程序入口是 Main()，用 Console.WriteLine 输出一行文本。" });
            helloWorld.SetInitialCode("using System;\n\nclass Program {\n    static void Main() {\n        // Your code here\n    }\n}");
            helloWorld.AddTestCase(new TestCase("Test 1", "—", "Hello World"));
            helloWorld.UpdateRewards(10, 5);

            var fibonacci = new Challenge("Fibonacci", "Calculate the nth Fibonacci number", ChallengeType.Algorithm, Difficulty.Easy, "Algorithms");
            fibonacci.SetHints(new List<string> { "F(0)=0, F(1)=1，其后每项为前两项之和。", "迭代实现比递归更高效。" });
            fibonacci.SetInitialCode("using System;\n\nclass Program {\n    static void Main() {\n        int n = 10;\n        Console.WriteLine(Fibonacci(n));\n    }\n    \n    static int Fibonacci(int n) {\n        // Implement Fibonacci\n        return 0;\n    }\n}");
            fibonacci.AddTestCase(new TestCase("Test 1", "10", "55"));
            fibonacci.UpdateRewards(10, 5);

            await context.Challenges.AddRangeAsync(helloWorld, fibonacci);
            await context.SaveChangesAsync();
        }

        // Add default quests (daily/weekly tasks with real XP/gem rewards).
        if (!await context.Quests.AnyAsync())
        {
            var quests = new List<Quest>
            {
                new("daily_solve_1", "Daily Solver", "Solve 1 challenge today", QuestCadence.Daily, QuestMetric.SolvedChallenges, 1, 15, 3, "🎯", 1),
                new("daily_submit_5", "Persistent", "Make 5 submissions today", QuestCadence.Daily, QuestMetric.Submissions, 5, 20, 5, "💪", 2),
                new("daily_solve_3", "Hat Trick", "Solve 3 challenges today", QuestCadence.Daily, QuestMetric.SolvedChallenges, 3, 40, 8, "🎩", 3),
                new("weekly_solve_5", "Weekly Grind", "Solve 5 challenges this week", QuestCadence.Weekly, QuestMetric.SolvedChallenges, 5, 80, 15, "📅", 4),
                new("weekly_submit_20", "Iron Coder", "Make 20 submissions this week", QuestCadence.Weekly, QuestMetric.Submissions, 20, 100, 20, "🔥", 5),
                new("weekly_diversity_3", "All-Rounder", "Solve challenges in 3 different difficulties this week", QuestCadence.Weekly, QuestMetric.DistinctDifficulties, 3, 120, 25, "🌈", 6),
            };

            await context.Quests.AddRangeAsync(quests);
            await context.SaveChangesAsync();
        }

        // Expand the starter challenge pool so /api/challenge/daily has variety. Independent sentinel
        // gate (Title == "Sum Two Numbers") so it runs even on DBs that already have the original 2.
        // NOTE: the runner compares ONE program stdout against each TestCase.ExpectedOutput and does NOT
        // inject per-case stdin, so these challenges embed a fixed input in the starter code and ask for a
        // deterministic output — the user implements the logic that prints it.
        if (!await context.Challenges.AnyAsync(c => c.Title == "Sum Two Numbers"))
        {
            var sumTwo = new Challenge("Sum Two Numbers", "Print the sum of a and b.", ChallengeType.Algorithm, Difficulty.Beginner, "Basics");
            sumTwo.SetInitialCode("using System;\n\nclass Program {\n    static void Main() {\n        int a = 2;\n        int b = 3;\n        // TODO: print the sum of a and b\n    }\n}");
            sumTwo.AddTestCase(new TestCase("Example", "2 3", "5"));
            sumTwo.UpdateRewards(10, 2);
            sumTwo.SetHints(new List<string> { "用 a + b 求和，再用 Console.WriteLine 打印结果。" });

            var factorial = new Challenge("Factorial", "Compute and print n! (factorial of n).", ChallengeType.Algorithm, Difficulty.Easy, "Algorithms");
            factorial.SetInitialCode("using System;\n\nclass Program {\n    static void Main() {\n        int n = 5;\n        // TODO: compute and print n!\n    }\n}");
            factorial.AddTestCase(new TestCase("Example", "5", "120"));
            factorial.UpdateRewards(15, 3);
            factorial.SetHints(new List<string> { "n! = 1×2×…×n，注意 0! = 1。", "可用一个循环累乘。" });

            var reverseString = new Challenge("Reverse String", "Print the string s reversed.", ChallengeType.Algorithm, Difficulty.Easy, "Strings");
            reverseString.SetInitialCode("using System;\n\nclass Program {\n    static void Main() {\n        string s = \"hello\";\n        // TODO: print s reversed\n    }\n}");
            reverseString.AddTestCase(new TestCase("Example", "hello", "olleh"));
            reverseString.UpdateRewards(15, 3);
            reverseString.SetHints(new List<string> { "可从末尾向前逐字符拼接，或把字符串转为字符数组再反转。" });

            var palindrome = new Challenge("Palindrome Check", "Print True if s is a palindrome, otherwise print False.", ChallengeType.Algorithm, Difficulty.Medium, "Strings");
            palindrome.SetInitialCode("using System;\n\nclass Program {\n    static void Main() {\n        string s = \"racecar\";\n        // TODO: print True if s is a palindrome, False otherwise\n    }\n}");
            palindrome.AddTestCase(new TestCase("Example", "racecar", "True"));
            palindrome.UpdateRewards(25, 5);
            palindrome.SetHints(new List<string> { "双指针从两端向中间比较，全程相等即为回文。", "本题按原串逐字符比较，无需额外处理大小写。" });

            var primeCheck = new Challenge("Prime Check", "Print True if n is a prime number, otherwise print False.", ChallengeType.Algorithm, Difficulty.Medium, "Algorithms");
            primeCheck.SetInitialCode("using System;\n\nclass Program {\n    static void Main() {\n        int n = 17;\n        // TODO: print True if n is prime, False otherwise\n    }\n}");
            primeCheck.AddTestCase(new TestCase("Example", "17", "True"));
            primeCheck.UpdateRewards(25, 5);
            primeCheck.SetHints(new List<string> { "大于 1 且只能被 1 和自身整除的数为素数。", "试除时只需检查到 √n 即可。" });

            var powerOfTwo = new Challenge("Power of Two", "Compute and print 2 raised to the power n (2^n).", ChallengeType.Algorithm, Difficulty.Hard, "Algorithms");
            powerOfTwo.SetInitialCode("using System;\n\nclass Program {\n    static void Main() {\n        int n = 10;\n        // TODO: compute and print 2^n\n    }\n}");
            powerOfTwo.AddTestCase(new TestCase("Example", "10", "1024"));
            powerOfTwo.UpdateRewards(40, 8);
            powerOfTwo.SetHints(new List<string> { "2^n 可用循环累乘，或位运算 1 << n（留意溢出）。" });

            var nQueens = new Challenge("N-Queens Count", "Count and print the number of distinct solutions to the n-Queens problem on an n x n board.", ChallengeType.Algorithm, Difficulty.Expert, "Algorithms");
            nQueens.SetInitialCode("using System;\n\nclass Program {\n    static void Main() {\n        int n = 4;\n        // TODO: count and print the number of n-Queens solutions\n    }\n}");
            nQueens.AddTestCase(new TestCase("Example", "4", "2"));
            nQueens.UpdateRewards(60, 12);
            nQueens.SetHints(new List<string> { "经典回溯：逐行放置皇后，检查同列与两条对角线是否冲突。", "n=4 时答案为 2。" });

            var newChallenges = new List<Challenge> { sumTwo, factorial, reverseString, palindrome, primeCheck, powerOfTwo, nQueens };
            await context.Challenges.AddRangeAsync(newChallenges);
            await context.SaveChangesAsync();
        }

        // Backfill Hints onto the starter challenges for DBs seeded before hints existed (the create-gates
        // above don't re-run once the rows exist). Idempotent: only fills challenges whose Hints list is
        // empty. Hint text is duplicated from the create-blocks above on purpose so this stays self-contained.
        {
            var hintsByTitle = new Dictionary<string, string[]>
            {
                ["Hello World"] = new[] { "程序入口是 Main()，用 Console.WriteLine 输出一行文本。" },
                ["Fibonacci"] = new[] { "F(0)=0, F(1)=1，其后每项为前两项之和。", "迭代实现比递归更高效。" },
                ["Sum Two Numbers"] = new[] { "用 a + b 求和，再用 Console.WriteLine 打印结果。" },
                ["Factorial"] = new[] { "n! = 1×2×…×n，注意 0! = 1。", "可用一个循环累乘。" },
                ["Reverse String"] = new[] { "可从末尾向前逐字符拼接，或把字符串转为字符数组再反转。" },
                ["Palindrome Check"] = new[] { "双指针从两端向中间比较，全程相等即为回文。", "本题按原串逐字符比较，无需额外处理大小写。" },
                ["Prime Check"] = new[] { "大于 1 且只能被 1 和自身整除的数为素数。", "试除时只需检查到 √n 即可。" },
                ["Power of Two"] = new[] { "2^n 可用循环累乘，或位运算 1 << n（留意溢出）。" },
                ["N-Queens Count"] = new[] { "经典回溯：逐行放置皇后，检查同列与两条对角线是否冲突。", "n=4 时答案为 2。" },
            };
            var titles = hintsByTitle.Keys.ToList();
            var existing = await context.Challenges.Where(c => titles.Contains(c.Title)).ToListAsync();
            var changed = false;
            foreach (var challenge in existing.Where(c => c.Hints.Count == 0))
            {
                challenge.SetHints(hintsByTitle[challenge.Title]);
                changed = true;
            }
            if (changed) await context.SaveChangesAsync();
        }

        // Backfill reference solutions (SolutionCode) for the Reveal Solution feature. The create-gates
        // above don't re-run once rows exist, so this idempotent block fills any challenge whose
        // SolutionCode is empty — covering both DBs seeded before solutions existed and fresh DBs. Each
        // solution prints the deterministic output its TestCase expects (the runner compares whole stdout),
        // so revealing a solution then pressing Run still passes.
        {
            var solutionsByTitle = new Dictionary<string, string>
            {
                ["Hello World"] = "using System;\n\nclass Program {\n    static void Main() {\n        Console.WriteLine(\"Hello World\");\n    }\n}",
                ["Fibonacci"] = "using System;\n\nclass Program {\n    static void Main() {\n        int n = 10;\n        Console.WriteLine(Fibonacci(n));\n    }\n\n    static int Fibonacci(int n) {\n        if (n <= 1) return n;\n        int prev = 0, curr = 1;\n        for (int i = 2; i <= n; i++) { int next = prev + curr; prev = curr; curr = next; }\n        return curr;\n    }\n}",
                ["Sum Two Numbers"] = "using System;\n\nclass Program {\n    static void Main() {\n        int a = 2;\n        int b = 3;\n        Console.WriteLine(a + b);\n    }\n}",
                ["Factorial"] = "using System;\n\nclass Program {\n    static void Main() {\n        int n = 5;\n        int result = 1;\n        for (int i = 2; i <= n; i++) result *= i;\n        Console.WriteLine(result);\n    }\n}",
                ["Reverse String"] = "using System;\n\nclass Program {\n    static void Main() {\n        string s = \"hello\";\n        char[] arr = s.ToCharArray();\n        Array.Reverse(arr);\n        Console.WriteLine(new string(arr));\n    }\n}",
                ["Palindrome Check"] = "using System;\n\nclass Program {\n    static void Main() {\n        string s = \"racecar\";\n        bool ok = true;\n        for (int i = 0; i < s.Length / 2; i++)\n            if (s[i] != s[s.Length - 1 - i]) { ok = false; break; }\n        Console.WriteLine(ok ? \"True\" : \"False\");\n    }\n}",
                ["Prime Check"] = "using System;\n\nclass Program {\n    static void Main() {\n        int n = 17;\n        bool isPrime = n > 1;\n        for (int i = 2; i * i <= n; i++)\n            if (n % i == 0) { isPrime = false; break; }\n        Console.WriteLine(isPrime ? \"True\" : \"False\");\n    }\n}",
                ["Power of Two"] = "using System;\n\nclass Program {\n    static void Main() {\n        int n = 10;\n        long result = 1;\n        for (int i = 0; i < n; i++) result *= 2;\n        Console.WriteLine(result);\n    }\n}",
                ["N-Queens Count"] = "using System;\n\nclass Program {\n    static void Main() {\n        int n = 4;\n        int count = 0;\n        Place(0, n, new int[n], ref count);\n        Console.WriteLine(count);\n    }\n\n    static void Place(int row, int n, int[] cols, ref int count) {\n        if (row == n) { count++; return; }\n        for (int c = 0; c < n; c++) {\n            bool ok = true;\n            for (int r = 0; r < row; r++)\n                if (cols[r] == c || System.Math.Abs(cols[r] - c) == row - r) { ok = false; break; }\n            if (ok) { cols[row] = c; Place(row + 1, n, cols, ref count); }\n        }\n    }\n}",
                ["Welcome Aboard"] = "using System;\n\nclass Program {\n    static void Main() {\n        Console.WriteLine(\"Welcome to Katabench!\");\n    }\n}",
                ["Meet the Console"] = "using System;\n\nclass Program {\n    static void Main() {\n        int a = 8;\n        int b = 4;\n        Console.WriteLine(a * b);\n    }\n}",
                ["Combine Strings"] = "using System;\n\nclass Program {\n    static void Main() {\n        string first = \"Hello\";\n        Console.WriteLine(first + \", C#!\");\n    }\n}",
                ["Two Pointers: Reverse Integer"] = "using System;\n\nclass Program {\n    static void Main() {\n        int n = 12345;\n        int reversed = 0, x = n;\n        while (x > 0) { reversed = reversed * 10 + x % 10; x /= 10; }\n        Console.WriteLine(reversed);\n    }\n}",
                ["Sliding Window: Range Sum"] = "using System;\n\nclass Program {\n    static void Main() {\n        int n = 10;\n        Console.WriteLine(n * (n + 1) / 2);\n    }\n}",
                ["Recursion: Greatest Common Divisor"] = "using System;\n\nclass Program {\n    static void Main() {\n        int a = 48, b = 36;\n        while (b != 0) { int t = b; b = a % b; a = t; }\n        Console.WriteLine(a);\n    }\n}",
                ["Bit Manipulation: Count Set Bits"] = "using System;\n\nclass Program {\n    static void Main() {\n        int n = 13, count = 0;\n        while (n > 0) { count += n & 1; n >>= 1; }\n        Console.WriteLine(count);\n    }\n}",
                ["Dynamic Programming: Climbing Stairs"] = "using System;\n\nclass Program {\n    static void Main() {\n        int n = 5, prev = 1, curr = 1;\n        for (int i = 0; i < n; i++) { int next = prev + curr; prev = curr; curr = next; }\n        Console.WriteLine(prev);\n    }\n}",
                ["LINQ Aggregation"] = "using System;\nusing System.Linq;\n\nclass Program {\n    static void Main() {\n        int[] nums = { 3, 1, 4, 1, 5, 9, 2, 6 };\n        Console.WriteLine(nums.Sum());\n    }\n}",
                ["Distinct Count"] = "using System;\nusing System.Linq;\n\nclass Program {\n    static void Main() {\n        int[] nums = { 1, 2, 2, 3, 3, 3, 4 };\n        Console.WriteLine(nums.Distinct().Count());\n    }\n}",
                ["Dictionary Lookup"] = "using System;\nusing System.Collections.Generic;\n\nclass Program {\n    static void Main() {\n        var map = new Dictionary<string, int> { { \"x\", 10 }, { \"y\", 20 } };\n        string key = \"y\";\n        Console.WriteLine(map[key]);\n    }\n}",
                ["Extract Named Constant"] = "using System;\n\nclass Program {\n    static void Main() {\n        int amount = 500;\n        int ratePercent = 20;\n        Console.WriteLine(amount * ratePercent / 100);\n    }\n}",
                ["Simplify Conditional"] = "using System;\n\nclass Program {\n    static void Main() {\n        int age = 20;\n        Console.WriteLine(age >= 18 ? \"Adult\" : \"Minor\");\n    }\n}",
                ["Replace Loop with LINQ"] = "using System;\nusing System.Linq;\n\nclass Program {\n    static void Main() {\n        int[] nums = { 1, 2, 3, 4, 5, 6 };\n        Console.WriteLine(nums.Count(x => x % 2 == 0));\n    }\n}",
                ["Hash Digest Length"] = "using System;\n\nclass Program {\n    static void Main() {\n        // SHA-256 produces a 32-byte digest = 64 hex characters.\n        Console.WriteLine(32 * 2);\n    }\n}",
                ["Password Policy"] = "using System;\n\nclass Program {\n    static void Main() {\n        string password = \"s3cur3P@ssw0rd\";\n        Console.WriteLine(password.Length >= 12 ? \"True\" : \"False\");\n    }\n}",
                ["Email Validation"] = "using System;\n\nclass Program {\n    static void Main() {\n        string email = \"a@b.com\";\n        Console.WriteLine(email.Contains(\"@\") && email.Contains(\".\") ? \"True\" : \"False\");\n    }\n}",
                ["Count the Layers"] = "using System;\n\nclass Program {\n    static void Main() {\n        string[] layers = { \"Domain\", \"Application\", \"Infrastructure\", \"Presentation\" };\n        Console.WriteLine(layers.Length);\n    }\n}",
                ["Dependency Direction"] = "using System;\n\nclass Program {\n    static void Main() {\n        string direction = \"inward\";\n        Console.WriteLine(direction == \"inward\" ? \"True\" : \"False\");\n    }\n}",
                ["Bounded Contexts"] = "using System;\n\nclass Program {\n    static void Main() {\n        string[] contexts = { \"Catalog\", \"Billing\", \"Shipping\" };\n        Console.WriteLine(contexts.Length);\n    }\n}",
            };
            var titles = solutionsByTitle.Keys.ToList();
            var existing = await context.Challenges.Where(c => titles.Contains(c.Title)).ToListAsync();
            var changed = false;
            foreach (var challenge in existing.Where(c => string.IsNullOrEmpty(c.SolutionCode)))
            {
                challenge.SetSolution(solutionsByTitle[challenge.Title]);
                changed = true;
            }

            // Normalize the historically-buggy Hello World test (older seeds gave it Fibonacci's "55"
            // case). If no public case expects "Hello World", reset it to the correct one so the
            // reference solution above passes when Run. No-op on DBs seeded by the fixed create-block.
            var helloWorld = existing.FirstOrDefault(c => c.Title == "Hello World");
            if (helloWorld != null && !helloWorld.TestCases.Any(t => t.ExpectedOutput == "Hello World"))
            {
                helloWorld.ClearTestCases();
                helloWorld.AddTestCase(new TestCase("Test 1", "—", "Hello World"));
                changed = true;
            }

            if (changed) await context.SaveChangesAsync();
        }

        // Link starter challenges to the sample learning paths so /api/learningpath/{id} returns a non-empty,
        // ordered challenge list. Independent gate (no challenge has a LearningPathId yet) so it runs on DBs
        // that already seeded paths/challenges via the earlier blocks.
        if (!await context.Challenges.AnyAsync(c => c.LearningPathId != null))
        {
            var beginnerPath = await context.LearningPaths.FirstOrDefaultAsync(p => p.Name == "C# Fundamentals");
            var intermediatePath = await context.LearningPaths.FirstOrDefaultAsync(p => p.Name == "Advanced C#");

            if (beginnerPath != null && intermediatePath != null)
            {
                var links = new Dictionary<string, (Guid PathId, int Order)>
                {
                    { "Hello World",     (beginnerPath.Id, 1) },
                    { "Sum Two Numbers",  (beginnerPath.Id, 2) },
                    { "Factorial",        (beginnerPath.Id, 3) },
                    { "Reverse String",   (beginnerPath.Id, 4) },
                    { "Palindrome Check", (beginnerPath.Id, 5) },
                    { "Prime Check",      (intermediatePath.Id, 1) },
                    { "Power of Two",     (intermediatePath.Id, 2) },
                    { "N-Queens Count",   (intermediatePath.Id, 3) },
                };

                var titles = links.Keys.ToList();
                var toLink = await context.Challenges.Where(c => titles.Contains(c.Title)).ToListAsync();
                foreach (var challenge in toLink)
                {
                    if (links.TryGetValue(challenge.Title, out var link))
                    {
                        challenge.SetLearningPath(link.PathId);
                        challenge.SetOrderInPath(link.Order);
                    }
                }
                await context.SaveChangesAsync();
            }
        }

        // Seed the six "showcase" learning paths (matching the reference catalog) plus themed, solvable
        // challenges. Independent gate (sentinel = "The Katabench Tour") so it runs even on DBs that
        // already have the original 2 paths. Icons/rewards are also applied to the original paths,
        // which were created without icons in the earlier gate above.
        // NOTE: the runner compares ONE program stdout against each TestCase.ExpectedOutput and does NOT
        // inject per-case stdin, so each challenge embeds a fixed input in the starter code and asks for a
        // deterministic output — the user implements the logic that prints it.
        if (!await context.LearningPaths.AnyAsync(p => p.Name == "The Katabench Tour"))
        {
            static LearningPath NewPath(string name, string description, string level, string icon, int order, int xp, int gems)
            {
                var path = new LearningPath(name, description, level);
                path.SetIcon(icon);
                path.UpdateRewards(xp, gems);
                path.SetOrder(order);
                return path;
            }

            static Challenge Make(string title, string description, ChallengeType type, Difficulty difficulty, string category,
                string initialCode, string input, string expected, int xp, int gems, Guid pathId, int order)
            {
                var challenge = new Challenge(title, description, type, difficulty, category);
                challenge.SetInitialCode(initialCode);
                challenge.AddTestCase(new TestCase("Example", input, expected));
                challenge.UpdateRewards(xp, gems);
                challenge.SetLearningPath(pathId);
                challenge.SetOrderInPath(order);
                return challenge;
            }

            // Tag the two original paths with icons + order/rewards (they were created bare above).
            var fundamentals = await context.LearningPaths.FirstOrDefaultAsync(p => p.Name == "C# Fundamentals");
            var advancedCs = await context.LearningPaths.FirstOrDefaultAsync(p => p.Name == "Advanced C#");
            if (fundamentals != null) { fundamentals.SetIcon("📖"); fundamentals.UpdateRewards(100, 20); fundamentals.SetOrder(7); }
            if (advancedCs != null) { advancedCs.SetIcon("🎓"); advancedCs.UpdateRewards(160, 32); advancedCs.SetOrder(8); }

            var tour = NewPath("The Katabench Tour", "A guided tour of the platform and the C# essentials you'll use every day.", "Beginner", "🗺️", 1, 80, 16);
            var patterns = NewPath("Algorithm Patterns in C#", "Recognize the patterns behind common algorithms — two pointers, sliding window, recursion, and dynamic programming.", "Intermediate", "🧩", 2, 150, 30);
            var dataAccess = NewPath("High-Performance Data Access", "Write allocation-aware C#: LINQ mastery, efficient collections, and fast lookups.", "Advanced", "⚡", 3, 200, 40);
            var refactoring = NewPath("Refactoring Legacy C#", "Turn messy code into clean, maintainable C# using extraction, clear naming, and SOLID principles.", "Advanced", "🔧", 4, 200, 40);
            var security = NewPath("Secure Web APIs in C#", "Defend your APIs with hashing, validation, and safe handling of secrets and input.", "Advanced", "🔒", 5, 200, 40);
            var architecture = NewPath("Architecture Boundaries", "Design systems with clear layers, dependencies that point inward, and bounded contexts.", "Advanced", "🏛️", 6, 200, 40);

            var paths = new[] { tour, patterns, dataAccess, refactoring, security, architecture };
            foreach (var p in paths) p.Publish();
            await context.LearningPaths.AddRangeAsync(paths);

            var challenges = new List<Challenge>();

            // The Katabench Tour
            challenges.Add(Make("Welcome Aboard", "Print the welcome message that greets every new coder.", ChallengeType.Algorithm, Difficulty.Beginner, "Basics", "using System;\n\nclass Program {\n    static void Main() {\n        // TODO: print the welcome message: Welcome to Katabench!\n    }\n}", "—", "Welcome to Katabench!", 10, 2, tour.Id, 1));
            challenges.Add(Make("Meet the Console", "Multiply two integers and print the product.", ChallengeType.Algorithm, Difficulty.Beginner, "Basics", "using System;\n\nclass Program {\n    static void Main() {\n        int a = 8;\n        int b = 4;\n        // TODO: print the product of a and b\n    }\n}", "8 4", "32", 10, 2, tour.Id, 2));
            challenges.Add(Make("Combine Strings", "Concatenate two strings and print the greeting.", ChallengeType.Algorithm, Difficulty.Beginner, "Strings", "using System;\n\nclass Program {\n    static void Main() {\n        string first = \"Hello\";\n        // TODO: print first + \", C#!\"\n    }\n}", "Hello", "Hello, C#!", 10, 2, tour.Id, 3));

            // Algorithm Patterns in C#
            challenges.Add(Make("Two Pointers: Reverse Integer", "Reverse the digits of an integer and print it.", ChallengeType.Algorithm, Difficulty.Easy, "Algorithms", "using System;\n\nclass Program {\n    static void Main() {\n        int n = 12345;\n        // TODO: reverse the digits of n and print the result\n    }\n}", "12345", "54321", 15, 3, patterns.Id, 1));
            challenges.Add(Make("Sliding Window: Range Sum", "Print the sum of every integer from 1 to n.", ChallengeType.Algorithm, Difficulty.Easy, "Algorithms", "using System;\n\nclass Program {\n    static void Main() {\n        int n = 10;\n        // TODO: print the sum of every integer from 1 to n\n    }\n}", "10", "55", 15, 3, patterns.Id, 2));
            challenges.Add(Make("Recursion: Greatest Common Divisor", "Use Euclid's algorithm to print the GCD of two numbers.", ChallengeType.Algorithm, Difficulty.Medium, "Algorithms", "using System;\n\nclass Program {\n    static void Main() {\n        int a = 48;\n        int b = 36;\n        // TODO: print the greatest common divisor of a and b\n    }\n}", "48 36", "12", 25, 5, patterns.Id, 3));
            challenges.Add(Make("Bit Manipulation: Count Set Bits", "Print how many 1-bits are in the binary form of n.", ChallengeType.Algorithm, Difficulty.Medium, "Algorithms", "using System;\n\nclass Program {\n    static void Main() {\n        int n = 13;\n        // TODO: print how many 1-bits are in n\n    }\n}", "13", "3", 25, 5, patterns.Id, 4));
            challenges.Add(Make("Dynamic Programming: Climbing Stairs", "Print the number of distinct ways to climb n stairs taking 1 or 2 steps.", ChallengeType.Algorithm, Difficulty.Medium, "Algorithms", "using System;\n\nclass Program {\n    static void Main() {\n        int n = 5;\n        // TODO: print the number of ways to climb n stairs (1 or 2 steps at a time)\n    }\n}", "5", "8", 25, 5, patterns.Id, 5));

            // High-Performance Data Access
            challenges.Add(Make("LINQ Aggregation", "Use LINQ to sum a sequence and print the total.", ChallengeType.Algorithm, Difficulty.Medium, "LINQ", "using System;\nusing System.Linq;\n\nclass Program {\n    static void Main() {\n        int[] nums = { 3, 1, 4, 1, 5, 9, 2, 6 };\n        // TODO: print the sum of nums\n    }\n}", "3 1 4 1 5 9 2 6", "31", 25, 5, dataAccess.Id, 1));
            challenges.Add(Make("Distinct Count", "Count the distinct values in a sequence and print the count.", ChallengeType.Algorithm, Difficulty.Easy, "LINQ", "using System;\nusing System.Linq;\n\nclass Program {\n    static void Main() {\n        int[] nums = { 1, 2, 2, 3, 3, 3, 4 };\n        // TODO: print the number of distinct values in nums\n    }\n}", "1 2 2 3 3 3 4", "4", 15, 3, dataAccess.Id, 2));
            challenges.Add(Make("Dictionary Lookup", "Look up a key in a dictionary and print its value.", ChallengeType.Algorithm, Difficulty.Easy, "Performance", "using System;\nusing System.Collections.Generic;\n\nclass Program {\n    static void Main() {\n        var map = new Dictionary<string, int> { { \"x\", 10 }, { \"y\", 20 } };\n        string key = \"y\";\n        // TODO: print the value stored under key\n    }\n}", "y", "20", 15, 3, dataAccess.Id, 3));

            // Refactoring Legacy C#
            challenges.Add(Make("Extract Named Constant", "Replace a magic number with a named constant, then print the tax on a base amount.", ChallengeType.Refactoring, Difficulty.Easy, "Clean Code", "using System;\n\nclass Program {\n    static void Main() {\n        int amount = 500;\n        int ratePercent = 20;\n        // TODO: print amount * ratePercent / 100\n    }\n}", "500 @ 20%", "100", 15, 3, refactoring.Id, 1));
            challenges.Add(Make("Simplify Conditional", "Replace a nested if with a clear expression and print the membership tier.", ChallengeType.Refactoring, Difficulty.Easy, "Clean Code", "using System;\n\nclass Program {\n    static void Main() {\n        int age = 20;\n        // TODO: print \"Adult\" when age >= 18, otherwise print \"Minor\"\n    }\n}", "age 20", "Adult", 15, 3, refactoring.Id, 2));
            challenges.Add(Make("Replace Loop with LINQ", "Rewrite a counting loop as a LINQ query and print the number of even values.", ChallengeType.Refactoring, Difficulty.Medium, "Clean Code", "using System;\nusing System.Linq;\n\nclass Program {\n    static void Main() {\n        int[] nums = { 1, 2, 3, 4, 5, 6 };\n        // TODO: print how many values in nums are even\n    }\n}", "1 2 3 4 5 6", "3", 25, 5, refactoring.Id, 3));

            // Secure Web APIs in C#
            challenges.Add(Make("Hash Digest Length", "Print the length, in hex characters, of a SHA-256 digest.", ChallengeType.Security, Difficulty.Easy, "Security", "using System;\n\nclass Program {\n    static void Main() {\n        // A SHA-256 digest is 32 bytes.\n        // TODO: print its length expressed in hexadecimal characters\n    }\n}", "SHA-256", "64", 15, 3, security.Id, 1));
            challenges.Add(Make("Password Policy", "Print True when a password meets the minimum length policy, otherwise False.", ChallengeType.Security, Difficulty.Easy, "Security", "using System;\n\nclass Program {\n    static void Main() {\n        string password = \"s3cur3P@ssw0rd\";\n        // TODO: print True when password.Length >= 12, otherwise False\n    }\n}", "12 chars", "True", 15, 3, security.Id, 2));
            challenges.Add(Make("Email Validation", "Print True when an address has a valid structure, otherwise False.", ChallengeType.Security, Difficulty.Medium, "Security", "using System;\n\nclass Program {\n    static void Main() {\n        string email = \"a@b.com\";\n        // TODO: print True when email contains \"@\" and \".\", otherwise False\n    }\n}", "a@b.com", "True", 25, 5, security.Id, 3));

            // Architecture Boundaries
            challenges.Add(Make("Count the Layers", "Print the number of layers in Clean Architecture.", ChallengeType.Architecture, Difficulty.Easy, "Architecture", "using System;\n\nclass Program {\n    static void Main() {\n        string[] layers = { \"Domain\", \"Application\", \"Infrastructure\", \"Presentation\" };\n        // TODO: print the number of layers\n    }\n}", "layers", "4", 15, 3, architecture.Id, 1));
            challenges.Add(Make("Dependency Direction", "Print True when a dependency points inward toward the domain.", ChallengeType.Architecture, Difficulty.Medium, "Architecture", "using System;\n\nclass Program {\n    static void Main() {\n        string direction = \"inward\";\n        // TODO: print True when direction == \"inward\", otherwise False\n    }\n}", "inward", "True", 25, 5, architecture.Id, 2));
            challenges.Add(Make("Bounded Contexts", "Count the bounded contexts in a system and print the total.", ChallengeType.Architecture, Difficulty.Easy, "Architecture", "using System;\n\nclass Program {\n    static void Main() {\n        string[] contexts = { \"Catalog\", \"Billing\", \"Shipping\" };\n        // TODO: print the number of bounded contexts\n    }\n}", "3 contexts", "3", 15, 3, architecture.Id, 3));

            await context.Challenges.AddRangeAsync(challenges);
            await context.SaveChangesAsync();
        }

        // Seed the reward shop catalog (titles / themes / streak-freeze packs). Independent sentinel
        // gate (Key == "bug-hunter") so it runs on DBs that already have other seed data. Costs are gem
        // prices; IsProOnly items are not gem-redeemable (membership-gated). NOTE: equipping a theme does
        // NOT restyle the app yet (single-light-theme design) — ownership/equip is persisted regardless.
        if (!await context.Rewards.AnyAsync(r => r.Key == "bug-hunter"))
        {
            var rewards = new List<Reward>
            {
                // Titles
                new("bug-hunter", "捉虫猎人", "献给永不放弃的 Debug 者。", RewardCategory.Title, 100, false, "bug", 1),
                new("insomniac", "夜猫子", "深夜还在写代码？这个称号属于你。", RewardCategory.Title, 250, false, "moon", 2),
                new("optimizer", "优化师", "绝不满足于 O(n²)。", RewardCategory.Title, 400, false, "gauge", 3),
                new("architect", "架构大师", "用清晰的分层与边界构筑系统。", RewardCategory.Title, 900, false, "building-2", 4),
                new("grandmaster", "宗师", "专属 Pro 称号，象征顶尖实力。", RewardCategory.Title, 0, true, "crown", 5),

                // Themes
                new("default-theme", "默认", "经典清爽的亮色外观。", RewardCategory.Theme, 0, false, "palette", 1, isDefault: true),
                new("midnight", "午夜", "深邃专注的深色调（即将上线）。", RewardCategory.Theme, 750, false, "moon-star", 2),
                new("forest", "森林", "护眼的绿色系（即将上线）。", RewardCategory.Theme, 500, false, "trees", 3),
                new("neon", "霓虹", "鲜艳的霓虹高亮。", RewardCategory.Theme, 0, true, "sparkles", 4),
                new("aurora", "极光", "流动的极光渐变。", RewardCategory.Theme, 0, true, "rainbow", 5),

                // Streak freezes
                new("streak-freeze", "连续保护", "漏打一天也能保住连签。", RewardCategory.StreakFreeze, 60, false, "snowflake", 1),
                new("triple-freeze", "三连保护", "三份连续保护，更划算。", RewardCategory.StreakFreeze, 150, false, "package", 2),
            };

            await context.Rewards.AddRangeAsync(rewards);
            await context.SaveChangesAsync();
        }

        // Promote configured admins (user-secrets / env "Admin:ExternalIds", e.g. Clerk user ids
        // like "user_2AbC…") to UserRole.Admin on every boot. Runs unconditionally (bare block) so it
        // catches admins whose User row was created later by the Clerk user.created webhook, and is
        // idempotent: once a user is already Admin (or higher) it no-ops. Demotion is intentionally NOT
        // done here — removing an id from the list leaves existing Admins untouched.
        {
            var adminIds = configuration.GetSection("Admin:ExternalIds").Get<string[]>() ?? [];
            if (adminIds.Length > 0)
            {
                var existing = await context.Users.Where(u => adminIds.Contains(u.ExternalId)).ToListAsync();
                var changed = false;
                foreach (var user in existing.Where(u => (int)u.Role < (int)UserRole.Admin))
                {
                    user.UpdateRole(UserRole.Admin);
                    changed = true;
                }
                if (changed) await context.SaveChangesAsync();
            }
        }
    }
}