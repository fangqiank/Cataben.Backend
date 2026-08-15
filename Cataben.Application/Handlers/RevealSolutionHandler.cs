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
            IDistributedTracing tracing
        ) : IRequestHandler<RevealSolutionCommand, RevealSolutionResultDto>
    {
        public async Task<RevealSolutionResultDto> Handle(RevealSolutionCommand request, CancellationToken cancellationToken)
        {
            using var activity = tracing.StartActivity("RevealSolution");
            activity?.SetTag("user.id", request.UserId);
            activity?.SetTag("challenge.id", request.ChallengeId);

            var challenge = await challengeRepository.GetPublicByIdAsync(request.ChallengeId, cancellationToken)
                ?? throw new NotFoundException("Challenge not found");

            var revealsRemaining = await userRepository.TryConsumeRevealAsync(
                request.UserId,
                cancellationToken);
            if (revealsRemaining is null)
                throw new ValidationException("Reveal credits exhausted");

            activity?.SetStatus(ActivityStatusCode.Ok);

            return new RevealSolutionResultDto
            {
                SolutionCode = challenge.SolutionCode,
                RevealsRemaining = revealsRemaining.Value
            };
        }
    }
}
