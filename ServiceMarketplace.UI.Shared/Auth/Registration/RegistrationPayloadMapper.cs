namespace ServiceMarketplace.UI.Shared.Auth.Registration;

public interface IRegistrationPayloadMapper
{
    RegisterRequest BuildRequest(RegistrationConversationState state);
    RegistrationIntentState BuildIntent(RegistrationConversationState state);
}

public sealed class RegistrationPayloadMapper : IRegistrationPayloadMapper
{
    public RegisterRequest BuildRequest(RegistrationConversationState state)
    {
        return new RegisterRequest
        {
            Email = state.Email,
            Password = state.Password,
            FirstName = state.FirstName,
            LastName = state.LastName,
            DateOfBirth = state.DateOfBirth,
            Role = state.WantsProvider ? "ServiceProvider" : "User",
            PhoneNumber = state.PhoneNumber,
            CommercialOnboarding = BuildCommercialOnboarding(state)
        };
    }

    public RegistrationIntentState BuildIntent(RegistrationConversationState state)
    {
        return new RegistrationIntentState
        {
            WantsServices = true,
            WantsProducts = true,
            WantsProvider = state.WantsProvider,
            WantsSeller = state.WantsSeller
        };
    }

    private static RegistrationCommercialOnboardingRequest? BuildCommercialOnboarding(RegistrationConversationState state)
    {
        if (!state.WantsProvider && !state.WantsSeller)
            return null;

        return new RegistrationCommercialOnboardingRequest
        {
            WantsProvider = state.WantsProvider,
            WantsSeller = state.WantsSeller,
            Business = ShouldIncludeBusiness(state)
                ? new RegistrationBusinessDetailsRequest
                {
                    LegalName = NullIfWhiteSpace(state.BusinessLegalName),
                    TradingName = NullIfWhiteSpace(state.BusinessTradingName),
                    BusinessType = NullIfWhiteSpace(state.BusinessType),
                    Gstin = NullIfWhiteSpace(state.BusinessGstin),
                    WebsiteOrDomain = NullIfWhiteSpace(state.BusinessWebsiteOrDomain),
                    RegisteredAddress = NullIfWhiteSpace(state.BusinessRegisteredAddress),
                    OperatingAddress = NullIfWhiteSpace(state.BusinessOperatingAddress),
                    RequestedSeatLimit = state.RequestedSeatLimit
                }
                : null,
            Provider = state.WantsProvider
                ? new RegistrationProviderDetailsRequest
                {
                    ProviderType = state.ProviderType,
                    Skills = state.ProviderSkills,
                    Profession = state.ProviderProfession,
                    YearsOfExperience = state.ProviderYearsOfExperience,
                    PrimaryCategory = state.ProviderPrimaryCategory,
                    ServiceAreaCity = state.ProviderServiceAreaCity,
                    ServiceAreaState = state.ProviderServiceAreaState,
                    ServiceAreaZone = NullIfWhiteSpace(state.ProviderServiceAreaZone),
                    PricingType = state.ProviderPricingType,
                    Rate = state.ProviderRate,
                    Availability = NullIfWhiteSpace(state.ProviderAvailability),
                    Languages = NullIfWhiteSpace(state.ProviderLanguages)
                }
                : null,
            Seller = state.WantsSeller
                ? new RegistrationSellerDetailsRequest
                {
                    StoreName = state.SellerStoreName,
                    BusinessName = NullIfWhiteSpace(state.SellerBusinessName),
                    Gstin = NullIfWhiteSpace(state.SellerGstin),
                    ProductCategories = NullIfWhiteSpace(state.SellerProductCategories),
                    ProductConditionFocus = state.SellerProductConditionFocus,
                    PickupAddress = state.SellerPickupAddress,
                    City = state.SellerCity,
                    State = state.SellerState,
                    Description = NullIfWhiteSpace(state.SellerDescription)
                }
                : null
        };
    }

    private static bool ShouldIncludeBusiness(RegistrationConversationState state)
    {
        return state.WantsSeller ||
            (state.WantsProvider && string.Equals(state.ProviderType, "Business", StringComparison.OrdinalIgnoreCase));
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
