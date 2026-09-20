using Microsoft.AspNetCore.Components.Forms;
using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.UI.Shared.Auth.Registration;

public enum RegistrationStage
{
    Intent,
    Account,
    Contact,
    Security,
    Professional,
    Seller,
    Business,
    Verification,
    Review
}

public enum RegistrationQuestionType
{
    Text,
    TextArea,
    Date,
    Phone,
    Email,
    Password,
    SingleChoice,
    MultipleChoice,
    Number,
    Currency,
    Upload,
    Review
}

public enum RegistrationInputMode
{
    Keyboard,
    Selection,
    Voice,
    Upload
}

public enum RegistrationAnswerOperation
{
    Set,
    Correct,
    Confirm,
    Remove
}

public enum RegistrationFieldStatus
{
    Missing,
    Candidate,
    Confirmed,
    Invalid,
    Conflict
}

public sealed record RegistrationCandidateUpdate(
    string Field,
    string? Value,
    RegistrationAnswerOperation Operation = RegistrationAnswerOperation.Set,
    decimal Confidence = 1m,
    IReadOnlyList<string>? Values = null);

public sealed record RegistrationFieldMetadata(
    RegistrationFieldStatus Status,
    decimal Confidence,
    string? ConflictValue = null);

public sealed record RegistrationConflict(
    string FieldKey,
    IReadOnlyList<string> CandidateValues,
    string Reason);

public sealed record RegistrationOption(
    string Value,
    string Label,
    string? Description = null,
    bool IsLocked = false);

public sealed record RegistrationQuestion(
    string Key,
    string Prompt,
    RegistrationQuestionType Type,
    RegistrationStage Stage,
    bool IsRequired,
    IReadOnlyList<RegistrationInputMode> InputModes,
    IReadOnlyList<RegistrationOption>? Options = null,
    string? HelpText = null);

public sealed record RegistrationAnswer(
    string Key,
    string? Value = null,
    IReadOnlyList<string>? Values = null,
    DateTime? DateValue = null,
    decimal? DecimalValue = null,
    int? IntValue = null,
    IBrowserFile? File = null);

public sealed record RegistrationStepResult(
    bool Succeeded,
    string? Error = null);

public sealed record RegistrationProgressItem(
    RegistrationStage Stage,
    string Label,
    bool IsActive,
    bool IsComplete);

public sealed record RegistrationReviewItem(
    string Section,
    string Label,
    string Value,
    string EditQuestionKey);

public sealed class RegistrationConversationState
{
    public string LanguageCode { get; set; } = "en-IN";
    public string? CurrentQuestionKey { get; set; } = RegistrationQuestionKeys.Intent;
    public List<string> QuestionHistory { get; } = [];
    public Dictionary<string, RegistrationAnswer> Answers { get; } = [];
    public Dictionary<string, RegistrationFieldMetadata> FieldMetadata { get; } = [];
    public List<RegistrationConflict> Conflicts { get; } = [];
    public double? CurrentLocationLatitude { get; set; }
    public double? CurrentLocationLongitude { get; set; }
    public double? CurrentLocationAccuracyMeters { get; set; }
    public bool CurrentLocationUsed { get; set; }
    public List<ServiceCategoryDto> ServiceCategories { get; set; } = [];

    public bool WantsServices { get; set; } = true;
    public bool WantsProducts { get; set; } = true;
    public bool WantsProvider { get; set; }
    public bool WantsSeller { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; } = DateTime.Today.AddYears(-25);
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public IBrowserFile? GovernmentIdImage { get; set; }

    public string ProviderType { get; set; } = "Individual";
    public string ProviderProfession { get; set; } = string.Empty;
    public string ProviderSkills { get; set; } = string.Empty;
    public int? ProviderYearsOfExperience { get; set; }
    public string ProviderPrimaryCategory { get; set; } = string.Empty;
    public string ProviderPrimaryCategorySlug { get; set; } = string.Empty;
    public string ProviderServiceAreaCity { get; set; } = "Kolkata";
    public string ProviderServiceAreaState { get; set; } = "West Bengal";
    public string ProviderServiceAreaZone { get; set; } = string.Empty;
    public string ProviderPricingType { get; set; } = "StartingPrice";
    public decimal ProviderRate { get; set; }
    public string ProviderAvailability { get; set; } = string.Empty;
    public string ProviderLanguages { get; set; } = string.Empty;

    public string SellerStoreName { get; set; } = string.Empty;
    public string SellerBusinessName { get; set; } = string.Empty;
    public string SellerGstin { get; set; } = string.Empty;
    public string SellerProductCategories { get; set; } = string.Empty;
    public string SellerProductConditionFocus { get; set; } = "Both";
    public string SellerPickupAddress { get; set; } = string.Empty;
    public string SellerCity { get; set; } = "Kolkata";
    public string SellerState { get; set; } = "West Bengal";
    public string SellerDescription { get; set; } = string.Empty;

    public string BusinessLegalName { get; set; } = string.Empty;
    public string BusinessTradingName { get; set; } = string.Empty;
    public string BusinessType { get; set; } = "SoleProprietorship";
    public string BusinessGstin { get; set; } = string.Empty;
    public string BusinessWebsiteOrDomain { get; set; } = string.Empty;
    public string BusinessRegisteredAddress { get; set; } = string.Empty;
    public string BusinessOperatingAddress { get; set; } = string.Empty;
    public int RequestedSeatLimit { get; set; } = 5;
}

public static class RegistrationQuestionKeys
{
    public const string Intent = "intent";
    public const string FirstName = "first-name";
    public const string LastName = "last-name";
    public const string DateOfBirth = "date-of-birth";
    public const string PhoneNumber = "phone-number";
    public const string Email = "email";
    public const string Password = "password";
    public const string ConfirmPassword = "confirm-password";
    public const string ProviderType = "provider-type";
    public const string ProviderCategory = "provider-category";
    public const string ProviderProfession = "provider-profession";
    public const string ProviderSkills = "provider-skills";
    public const string ProviderYears = "provider-years";
    public const string ProviderZone = "provider-zone";
    public const string ProviderCity = "provider-city";
    public const string ProviderState = "provider-state";
    public const string ProviderPricing = "provider-pricing";
    public const string ProviderRate = "provider-rate";
    public const string ProviderAvailability = "provider-availability";
    public const string ProviderLanguages = "provider-languages";
    public const string SellerStoreName = "seller-store-name";
    public const string SellerBusinessName = "seller-business-name";
    public const string SellerGstin = "seller-gstin";
    public const string SellerProductCategories = "seller-product-categories";
    public const string SellerConditionFocus = "seller-condition-focus";
    public const string SellerPickupAddress = "seller-pickup-address";
    public const string SellerCity = "seller-city";
    public const string SellerState = "seller-state";
    public const string SellerDescription = "seller-description";
    public const string BusinessLegalName = "business-legal-name";
    public const string BusinessTradingName = "business-trading-name";
    public const string BusinessType = "business-type";
    public const string BusinessGstin = "business-gstin";
    public const string BusinessWebsite = "business-website";
    public const string RequestedSeats = "requested-seats";
    public const string BusinessRegisteredAddress = "business-registered-address";
    public const string BusinessOperatingAddress = "business-operating-address";
    public const string GovernmentId = "government-id";
    public const string Review = "review";
}
