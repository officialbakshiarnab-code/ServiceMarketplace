namespace ServiceMarketplace.Application.Exceptions;

// Purpose: Signals a missing resource or entity.
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
