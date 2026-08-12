namespace Cataben.Application.Exceptions
{
    /// <summary>Thrown when a user tries to redeem a reward but does not have enough gems.
    /// Mapped to HTTP 400 by <see cref="Cataben.API.Middleware.ExceptionMiddleware"/>.</summary>
    public class InsufficientGemsException : Exception
    {
        public InsufficientGemsException() : base("宝石不足，无法兑换该奖励。") { }

        public InsufficientGemsException(string message) : base(message) { }

        public InsufficientGemsException(string message, Exception innerException) : base(message, innerException) { }
    }
}
