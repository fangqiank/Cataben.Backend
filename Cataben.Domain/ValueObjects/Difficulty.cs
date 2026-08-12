namespace Cataben.Domain.ValueObjects
{
    public class Difficulty: IComparable<Difficulty>
    {
        public string Name { get; private set; }
        public int Level { get; private set; }
        public int XpMultiplier { get; private set; }

        public static readonly Difficulty Beginner = new("Beginner", 1, 1);
        public static readonly Difficulty Easy = new("Easy", 2, 1);
        public static readonly Difficulty Medium = new("Medium", 3, 2);
        public static readonly Difficulty Hard = new("Hard", 4, 3);
        public static readonly Difficulty Expert = new("Expert", 5, 4);

        private Difficulty(string name, int level, int xpMultiplier)
        {
            Name = name;
            Level = level;
            XpMultiplier = xpMultiplier;
        }

        public static Difficulty FromLevel(int level)
        {
            return level switch
            {
                1 => Beginner,
                2 => Easy,
                3 => Medium,
                4 => Hard,
                5 => Expert,
                _ => Medium
            };
        }

        public static Difficulty FromName(string name)
        {
            return name.ToLower() switch
            {
                "beginner" => Beginner,
                "easy" => Easy,
                "medium" => Medium,
                "hard" => Hard,
                "expert" => Expert,
                _ => Medium
            };
        }

        public int CompareTo(Difficulty? other)
        {
            if (other is null) 
                return 1;
            return Level.CompareTo(other.Level);
        }

        public override string ToString() => Name;
    }
}
