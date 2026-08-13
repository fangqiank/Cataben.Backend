using System.Diagnostics;
using Cataben.Application.Commands;
using Cataben.Application.DTOs;
using Cataben.Application.Exceptions;
using MediatR;

namespace Cataben.Application.Handlers
{
    public class RevealSolutionHandler(
            IUserRepository userRepository,
            IChallengeRepository challengeRepository,
            IUnitOfWork unitOfWork,
            IDistributedTracing tracing
        ) : IRequestHandler<RevealSolutionCommand, RevealSolutionResultDto>
    {
        public async Task<RevealSolutionResultDto> Handle(RevealSolutionCommand request, CancellationToken cancellationToken)
        {
            using var activity = tracing.StartActivity("RevealSolution");
            activity?.SetTag("user.id", request.UserId);
            activity?.SetTag("challenge.id", request.ChallengeId);

            var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken)
                ?? throw new NotFoundException("User not found");

            var challenge = await challengeRepository.GetByIdAsync(request.ChallengeId, cancellationToken)
                ?? throw new NotFoundException("Challenge not found");

            // Consume one global reveal credit. UseReveal returns false (no state change) when the
            // budget is exhausted — throw before SaveChanges so ExceptionMiddleware maps it to 400.
            if (!user.UseReveal())
                throw new ValidationException("Reveal credits exhausted");

            await userRepository.UpdateAsync(user);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);

            return new RevealSolutionResultDto
            {
                SolutionCode = challenge.SolutionCode,
                RevealsRemaining = user.RevealsRemaining
            };
        }
    }
}
