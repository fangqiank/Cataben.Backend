namespace Cataben.Application.Exceptions
{
    public class NotFoundException: Exception
    {
        public NotFoundException() : base("The requested resource was not found.") { }

        public NotFoundException(string message) : base(message) { }

        public NotFoundException(string entity, object id)
            : base($"Entity '{entity}' with id '{id}' was not found.") { }
    }
}
