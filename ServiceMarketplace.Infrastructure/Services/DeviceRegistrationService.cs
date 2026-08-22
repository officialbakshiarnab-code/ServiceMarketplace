using Microsoft.EntityFrameworkCore;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.Application.Exceptions;
using ServiceMarketplace.Application.Interfaces;
using ServiceMarketplace.Domain.Entities;
using ServiceMarketplace.Domain.Enums;
using ServiceMarketplace.Infrastructure.Data;

namespace ServiceMarketplace.Infrastructure.Services;

public sealed class DeviceRegistrationService(AppDbContext context) : IDeviceRegistrationService
{
    public async Task<DeviceRegistrationDto> RegisterAsync(string userId, RegisterDeviceDto request)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var token = NormalizeToken(request.DeviceToken);
        var platform = request.Platform == 0 ? DevicePlatform.Unknown : request.Platform;

        var existing = await context.DeviceRegistrations.FirstOrDefaultAsync(d =>
            d.UserId == userId &&
            d.Platform == platform &&
            d.DeviceToken == token);

        if (existing == null)
        {
            existing = new DeviceRegistration
            {
                UserId = userId,
                Platform = platform,
                DeviceToken = token,
                DeviceName = NormalizeOptional(request.DeviceName, 120),
                IsActive = true,
                LastSeenAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            context.DeviceRegistrations.Add(existing);
        }
        else
        {
            existing.DeviceName = NormalizeOptional(request.DeviceName, 120) ?? existing.DeviceName;
            existing.IsActive = true;
            existing.LastSeenAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        return ToDto(existing);
    }

    public async Task<List<DeviceRegistrationDto>> GetMineAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var registrations = await context.DeviceRegistrations
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LastSeenAt)
            .Take(50)
            .ToListAsync();

        return registrations.Select(ToDto).ToList();
    }

    public async Task DeactivateAsync(Guid registrationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authentication is required.");

        var registration = await context.DeviceRegistrations.FirstOrDefaultAsync(d =>
            d.Id == registrationId &&
            d.UserId == userId)
            ?? throw new NotFoundException("Device registration not found.");

        registration.IsActive = false;
        registration.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    private static string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new BadRequestException("Device token is required.");

        token = token.Trim();
        if (token.Length > 512)
            throw new BadRequestException("Device token is too long.");

        return token;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static DeviceRegistrationDto ToDto(DeviceRegistration registration)
    {
        return new DeviceRegistrationDto
        {
            Id = registration.Id,
            Platform = registration.Platform,
            DeviceName = string.IsNullOrWhiteSpace(registration.DeviceName)
                ? registration.Platform.ToString()
                : registration.DeviceName,
            IsActive = registration.IsActive,
            LastSeenAt = registration.LastSeenAt,
            CreatedAt = registration.CreatedAt
        };
    }
}
