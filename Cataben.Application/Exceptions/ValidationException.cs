namespace Cataben.Application.Exceptions
{
    public class ValidationException: Exception
    {
        public Dictionary<string, string[]> Errors { get; } = new();

        public ValidationException() : base("One or more validation errors occurred.") { }

        public ValidationException(string message) : base(message) { }

        public ValidationException(string message, Exception innerException) : base(message, innerException) { }

        public ValidationException(Dictionary<string, string[]> errors) : base("One or more validation errors occurred.")
        {
            Errors = errors;
        }
    }
}
