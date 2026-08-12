namespace Cataben.Application.Repositories
{
    /// <summary>One day's submission count for a user. Projected server-side in
    /// <c>SubmissionRepository.GetUserSubmissionCountsByDayAsync</c>.</summary>
    public record SubmissionDayCount(DateTime Date, int Count);
}
