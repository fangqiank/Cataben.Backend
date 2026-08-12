using Cataben.Domain.Enums;

namespace Cataben.Application.DTOs
{
    public class RewardDto
    {
        public Guid Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RewardCategory Category { get; set; }
        public int Cost { get; set; }
        public bool IsProOnly { get; set; }
        public string Icon { get; set; } = string.Empty;
        public bool IsOwned { get; set; }
        public bool IsEquipped { get; set; }
    }

    public class RewardStoreDto
    {
        public List<RewardDto> Rewards { get; set; } = new();
    }
}
