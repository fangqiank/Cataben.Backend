namespace Cataben.Domain.Enums
{
    public enum SubmissionStatus
    {
        Pending = 0,
        Compiling = 1,
        Executing = 2,
        Testing = 3,
        Completed = 4,
        Failed = 5,
        Timeout = 6,
        Cancelled = 7,
        UnderReview = 8,
        Rejected = 9,
        PartialPass = 10,
        SystemError = 99
    }
}
