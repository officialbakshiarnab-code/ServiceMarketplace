namespace ServiceMarketplace.Application.Exceptions;

// Purpose: Signals a client-side validation or request error.
public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
}
