using Cataben.API.Services;
using Cataben.Application.DTOs;
using Cataben.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text;
using System.Text.Json;

namespace Cataben.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableRateLimiting("Default")]
    public class AuthController(
        IUserRepository userRepository,
        ISubmissionRepository submissionRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IConfiguration configuration,
        ILogger<AuthController> logger
        ) : ControllerBase
    {
        [HttpPost("webhook/clerk")]
        public async Task<IActionResult> ClerkWebhook()
        {
            try
            {
                Request.EnableBuffering();
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                var rawBody = await reader.ReadToEndAsync();
                Request.Body.Position = 0;

                var secret = configuration["Clerk:WebhookSecret"] ?? string.Empty;
                if (!ClerkWebhookVerifier.TryVerify(Request, rawBody, secret, out var signatureError))
                    return Unauthorized(new { error = signatureError });

                var payload = JsonSerializer.Deserialize<ClerkWebhookPayload>(
                    rawBody,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (payload == null || string.IsNullOrWhiteSpace(payload.Data.Id))
                    return BadRequest(new { error = "Invalid Clerk webhook payload" });

                if (payload.Type == "user.created" || payload.Type == "user.updated")
                {
                    var externalId = payload.Data.Id;
                    var email = payload.Data.EmailAddresses?.FirstOrDefault()?.EmailAddress ?? "";
                    var username = payload.Data.Username ?? email.Split('@')[0];

                    var existingUser = await userRepository.GetByExternalIdAsync(externalId);
                    if (existingUser == null)
                    {
                        var user = new User(email, username, externalId);
                        await userRepository.AddAsync(user);
                        await unitOfWork.SaveChangesAsync();

                        logger.LogInformation("User created from Clerk webhook: {UserId} ({Email})", user.Id, email);
                    }
                    else
                    {
                        existingUser.UpdateProfile(email, username);
                        await userRepository.UpdateAsync(existingUser);
                        await unitOfWork.SaveChangesAsync();

                        logger.LogInformation("User updated from Clerk webhook: {UserId} ({Email})", existingUser.Id, email);
                    }
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing Clerk webhook");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var resolvedUserId = await currentUser.GetUserIdAsync();
            if (resolvedUserId is null) return NotFound();
            var userId = resolvedUserId.Value;

            var user = await userRepository.GetByIdBasicAsync(userId);
            if (user == null)
                return NotFound();

            var submissionsCount = await submissionRepository.GetUserSubmissionCountAsync(userId);
            var successfulSubmissions = await submissionRepository.GetUserSuccessfulCountAsync(userId);
            var achievementsCount = await userRepository.GetCompletedAchievementCountAsync(userId);
            var submissionDates = await submissionRepository.GetUserSubmissionDatesAsync(userId);

            return Ok(new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                Xp = user.Xp,
                Gems = user.Gems,
                Level = user.CalculateLevel(),
                SubmissionsCount = submissionsCount,
                SuccessfulSubmissions = successfulSubmissions,
                AchievementsCount = achievementsCount,
                CurrentStreak = CalculateStreak(submissionDates),
                CreatedAt = user.CreatedAt,
                LastActiveAt = user.LastActiveAt,
                RevealsRemaining = user.RevealsRemaining
            });
        }

        private int CalculateStreak(IEnumerable<DateTime> submissionDates)
        {
            var dates = submissionDates
                .Select(d => d.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            if (!dates.Any()) return 0;

            var streak = 0;
            var expectedDate = dates.First();
            foreach (var date in dates)
            {
                if (date == expectedDate)
                {
                    streak++;
                    expectedDate = expectedDate.AddDays(-1);
                }
                else if (date < expectedDate)
                {
                    break;
                }
            }
            return streak;
        }
    }

    public class ClerkWebhookPayload
    {
        public string Type { get; set; } = string.Empty;
        public ClerkUserData Data { get; set; } = new();
    }

    public class ClerkUserData
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public List<ClerkEmail> EmailAddresses { get; set; } = new();
    }

    public class ClerkEmail
    {
        public string EmailAddress { get; set; } = string.Empty;
    }
}
