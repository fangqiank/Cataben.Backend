namespace Cataben.Application.Exceptions
{
    public class ExecutionTimeoutException : Exception
    {
        public ExecutionTimeoutException() : base("Code execution timed out.") { }

        public ExecutionTimeoutException(string message) : base(message) { }

        public ExecutionTimeoutException(string message, Exception innerException) : base(message, innerException) { }
    }
}
