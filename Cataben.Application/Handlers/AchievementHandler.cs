using Cataben.Application.DTOs;
using Cataben.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cataben.Application.Handlers
{
    public class AchievementHandler(
        IAchievementService achievementService,
        IUserRepository userRepository
        ):  IRequestHandler<GetAchievementsQuery,
            IEnumerable<AchievementDto>>,
            IRequestHandler<GetUserAchievementsQuery, IEnumerable<UserAchievementDto>>,
            IRequestHandler<GetAchievementProgressQuery, AchievementProgressDto?>
    {
        public async Task<IEnumerable<AchievementDto>> Handle(GetAchievementsQuery request, CancellationToken cancellationToken)
        {
            return await achievementService.GetAllAchievementsAsync(cancellationToken);
        }

        public async Task<IEnumerable<UserAchievementDto>> Handle(GetUserAchievementsQuery request, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
                throw new NotFoundException("User not found");

            return await achievementService.GetUserAchievementsAsync(request.UserId, cancellationToken);
        }

        public async Task<AchievementProgressDto?> Handle(GetAchievementProgressQuery request, CancellationToken cancellationToken)
        {
            return await achievementService.GetAchievementProgressAsync(request.UserId, request.AchievementId, cancellationToken);
        }
    }

    public class GetAchievementsQuery : IRequest<IEnumerable<AchievementDto>> { }

    public class GetUserAchievementsQuery : IRequest<IEnumerable<UserAchievementDto>>
    {
        public Guid UserId { get; set; }
    }

    public class GetAchievementProgressQuery : IRequest<AchievementProgressDto?>
    {
        public Guid UserId { get; set; }
        public string AchievementId { get; set; } = string.Empty;
    }
}
