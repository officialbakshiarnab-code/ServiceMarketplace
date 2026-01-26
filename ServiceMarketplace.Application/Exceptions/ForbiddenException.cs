namespace ServiceMarketplace.Application.Exceptions;

// Purpose: Signals an authorization failure for the current user.
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
