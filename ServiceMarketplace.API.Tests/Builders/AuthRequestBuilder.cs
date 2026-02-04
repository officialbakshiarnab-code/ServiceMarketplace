using ServiceMarketplace.API.Models.Auth;
using ServiceMarketplace.Application.Constants;

namespace ServiceMarketplace.API.Tests.Builders;

/// <summary>
/// Builder for creating test authentication requests.
/// Provides fluent API for building registration and login requests.
/// </summary>
public sealed class AuthRequestBuilder
{
    private string _email = "test@example.com";
    private string _password = "Test@123456";
    private string _role = RoleConstants.User;

    public AuthRequestBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public AuthRequestBuilder WithPassword(string password)
    {
        _password = password;
        return this;
    }

    public AuthRequestBuilder WithRole(string role)
    {
        _role = role;
        return this;
    }

    public AuthRequestBuilder AsServiceProvider()
    {
        _role = RoleConstants.ServiceProvider;
        return this;
    }

    public AuthRequestBuilder AsUser()
    {
        _role = RoleConstants.User;
        return this;
    }

    public RegisterRequest BuildRegisterRequest()
    {
        return new RegisterRequest
        {
            Email = _email,
            Password = _password,
            Role = _role
        };
    }

    public LoginRequest BuildLoginRequest()
    {
        return new LoginRequest
        {
            Email = _email,
            Password = _password
        };
    }

    public static AuthRequestBuilder CreateDefault() => new();
    
    public static AuthRequestBuilder CreateUser(string email = "user@example.com")
    {
        return new AuthRequestBuilder()
            .WithEmail(email)
            .AsUser();
    }

    public static AuthRequestBuilder CreateServiceProvider(string email = "provider@example.com")
    {
        return new AuthRequestBuilder()
            .WithEmail(email)
            .AsServiceProvider();
    }
}
