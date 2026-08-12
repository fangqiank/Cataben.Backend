namespace Cataben.Application.Exceptions
{
    public class UnauthorizedException : Exception
    {
        public UnauthorizedException() : base("You are not authorized to perform this action.") { }

        public UnauthorizedException(string message) : base(message) { }
    }
}
