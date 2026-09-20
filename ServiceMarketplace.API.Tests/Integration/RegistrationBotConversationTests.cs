using FluentAssertions;
using ServiceMarketplace.Application.DTOs;
using ServiceMarketplace.UI.Shared.Auth.Registration;
using Xunit;

namespace ServiceMarketplace.API.Tests.Integration;

public sealed class RegistrationBotConversationTests
{
    private readonly RegistrationConversationService _conversation = new();
    private readonly RegistrationPayloadMapper _mapper = new();

    [Fact]
    public void CustomerOnlyFlow_MapsToUserRegistrationWithoutCommercialPayload()
    {
        var state = new RegistrationConversationState();

        Answer(state, RegistrationQuestionKeys.Intent, values: []);
        Answer(state, RegistrationQuestionKeys.FirstName, value: "Demo");
        Answer(state, RegistrationQuestionKeys.LastName, value: "Customer");
        Answer(state, RegistrationQuestionKeys.DateOfBirth, date: DateTime.Today.AddYears(-22));
        Answer(state, RegistrationQuestionKeys.PhoneNumber, value: "9876543210");
        Answer(state, RegistrationQuestionKeys.Email, value: "customer@test.com");
        Answer(state, RegistrationQuestionKeys.Password, value: "Demo@12345");
        Answer(state, RegistrationQuestionKeys.ConfirmPassword, value: "Demo@12345");

        _conversation.GetCurrentQuestion(state).Key.Should().Be(RegistrationQuestionKeys.Review);
        var request = _mapper.BuildRequest(state);

        request.Role.Should().Be("User");
        request.CommercialOnboarding.Should().BeNull();
        request.PhoneNumber.Should().Be("9876543210");
    }

    [Fact]
    public void ProviderSellerFlow_MapsToExistingCommercialOnboardingPayload()
    {
        var categoryId = Guid.NewGuid();
        var state = new RegistrationConversationState
        {
            ServiceCategories =
            [
                new ServiceCategoryDto
                {
                    Id = categoryId,
                    Name = "Home Appliance Repair",
                    Slug = "home-appliance-repair",
                    IsActive = true
                }
            ]
        };

        Answer(state, RegistrationQuestionKeys.Intent, values: ["ProvideServices", "SellProducts"]);
        Answer(state, RegistrationQuestionKeys.FirstName, value: "Demo");
        Answer(state, RegistrationQuestionKeys.LastName, value: "Commercial");
        Answer(state, RegistrationQuestionKeys.DateOfBirth, date: DateTime.Today.AddYears(-30));
        Answer(state, RegistrationQuestionKeys.PhoneNumber, value: "9876543211");
        Answer(state, RegistrationQuestionKeys.Email, value: "commercial@test.com");
        Answer(state, RegistrationQuestionKeys.Password, value: "Demo@12345");
        Answer(state, RegistrationQuestionKeys.ConfirmPassword, value: "Demo@12345");
        Answer(state, RegistrationQuestionKeys.ProviderType, value: "Business");
        Answer(state, RegistrationQuestionKeys.ProviderCategory, value: categoryId.ToString());
        Answer(state, RegistrationQuestionKeys.ProviderProfession, value: "Appliance technician");
        Answer(state, RegistrationQuestionKeys.ProviderSkills, value: "AC repair, refrigerator repair");
        Answer(state, RegistrationQuestionKeys.ProviderYears, integer: 8);
        Answer(state, RegistrationQuestionKeys.ProviderZone, value: "North Kolkata");
        Answer(state, RegistrationQuestionKeys.ProviderCity, value: "Kolkata");
        Answer(state, RegistrationQuestionKeys.ProviderState, value: "West Bengal");
        Answer(state, RegistrationQuestionKeys.ProviderPricing, value: "StartingPrice");
        Answer(state, RegistrationQuestionKeys.ProviderRate, money: 699);
        Answer(state, RegistrationQuestionKeys.ProviderAvailability, value: "Mon-Sat");
        Answer(state, RegistrationQuestionKeys.ProviderLanguages, value: "Bengali, Hindi, English");
        Answer(state, RegistrationQuestionKeys.SellerStoreName, value: "Garia Home Goods");
        Answer(state, RegistrationQuestionKeys.SellerBusinessName, value: "Garia Home Goods");
        Answer(state, RegistrationQuestionKeys.SellerGstin, value: "");
        Answer(state, RegistrationQuestionKeys.SellerProductCategories, value: "Home improvement");
        Answer(state, RegistrationQuestionKeys.SellerConditionFocus, value: "Both");
        Answer(state, RegistrationQuestionKeys.SellerPickupAddress, value: "45 Garia Station Road");
        Answer(state, RegistrationQuestionKeys.SellerCity, value: "Kolkata");
        Answer(state, RegistrationQuestionKeys.SellerState, value: "West Bengal");
        Answer(state, RegistrationQuestionKeys.SellerDescription, value: "Local shop.");
        Answer(state, RegistrationQuestionKeys.BusinessLegalName, value: "North Kolkata Repairs Private Limited");
        Answer(state, RegistrationQuestionKeys.BusinessTradingName, value: "North Kolkata Repairs");
        Answer(state, RegistrationQuestionKeys.BusinessType, value: "PrivateLimited");
        Answer(state, RegistrationQuestionKeys.BusinessGstin, value: "");
        Answer(state, RegistrationQuestionKeys.BusinessWebsite, value: "northkolkatarepairs.test");
        Answer(state, RegistrationQuestionKeys.RequestedSeats, value: "10", integer: 10);
        Answer(state, RegistrationQuestionKeys.BusinessRegisteredAddress, value: "Registered address");
        Answer(state, RegistrationQuestionKeys.BusinessOperatingAddress, value: "Operating address");

        var request = _mapper.BuildRequest(state);

        request.Role.Should().Be("ServiceProvider");
        request.CommercialOnboarding.Should().NotBeNull();
        request.CommercialOnboarding!.WantsProvider.Should().BeTrue();
        request.CommercialOnboarding.WantsSeller.Should().BeTrue();
        request.CommercialOnboarding.Provider!.PrimaryCategory.Should().Be("Home Appliance Repair");
        request.CommercialOnboarding.Provider.Profession.Should().Be("Appliance technician");
        request.CommercialOnboarding.Provider.Rate.Should().Be(699);
        request.CommercialOnboarding.Seller!.StoreName.Should().Be("Garia Home Goods");
        request.CommercialOnboarding.Business!.RequestedSeatLimit.Should().Be(10);
    }

    [Fact]
    public void ProviderCategoryChange_ClearsDependentSkillAnswer()
    {
        var firstCategoryId = Guid.NewGuid();
        var secondCategoryId = Guid.NewGuid();
        var state = new RegistrationConversationState
        {
            WantsProvider = true,
            ServiceCategories =
            [
                new ServiceCategoryDto { Id = firstCategoryId, Name = "Beauty", Slug = "beauty", IsActive = true },
                new ServiceCategoryDto { Id = secondCategoryId, Name = "Electrical", Slug = "electrical", IsActive = true }
            ],
            CurrentQuestionKey = RegistrationQuestionKeys.ProviderCategory
        };

        Answer(state, RegistrationQuestionKeys.ProviderCategory, value: firstCategoryId.ToString());
        Answer(state, RegistrationQuestionKeys.ProviderSkills, value: "Bridal makeup");

        _conversation.GoToQuestion(state, RegistrationQuestionKeys.ProviderCategory);
        Answer(state, RegistrationQuestionKeys.ProviderCategory, value: secondCategoryId.ToString());

        state.ProviderSkills.Should().BeEmpty();
        state.Answers.Should().NotContainKey(RegistrationQuestionKeys.ProviderSkills);
    }

    [Fact]
    public void PasswordQuestions_AreKeyboardOnlyAndVoiceUnavailable()
    {
        var state = new RegistrationConversationState { CurrentQuestionKey = RegistrationQuestionKeys.Password };

        var passwordQuestion = _conversation.GetCurrentQuestion(state);
        passwordQuestion.Type.Should().Be(RegistrationQuestionType.Password);
        passwordQuestion.InputModes.Should().ContainSingle().Which.Should().Be(RegistrationInputMode.Keyboard);

        var speech = new UnavailableSpeechInputService();
        speech.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void LanguageCatalog_SupportsTieredIndianLocales()
    {
        RegistrationLanguageCatalog.Supported.Should().HaveCount(12);
        RegistrationLanguageCatalog.Get("hi-IN").Tier.Should().Be(RegistrationLanguageSupportTier.Tier1);
        RegistrationLanguageCatalog.Get("bn-IN").Tier.Should().Be(RegistrationLanguageSupportTier.Tier2);
        RegistrationLanguageCatalog.Get("unknown").Code.Should().Be("en-IN");
        RegistrationTextCatalog.GetQuestionPrompt(
            new RegistrationQuestion(RegistrationQuestionKeys.FirstName, "fallback", RegistrationQuestionType.Text, RegistrationStage.Account, true, []),
            "hi-IN").Should().Be("आपका पहला नाम क्या है?");
    }

    [Fact]
    public void RegistrationLanguageCatalog_HasAllLocalesAndSafeFallback()
    {
        RegistrationLanguageCatalog.Supported.Select(x => x.Code).Should().OnlyHaveUniqueItems();
        RegistrationLanguageCatalog.Supported.Should().HaveCount(12);
        RegistrationLanguageCatalog.Supported.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.NativeDisplayName));
        RegistrationTextCatalog.Get("missing.registration.key", "bn-IN").Should().BeEmpty();
        RegistrationTextCatalog.GetQuestionPrompt(
            new RegistrationQuestion(RegistrationQuestionKeys.ProviderRate, "English provider rate", RegistrationQuestionType.Currency, RegistrationStage.Professional, true, []),
            "bn-IN").Should().Be("English provider rate");
    }

    [Fact]
    public void HindiPresentation_LocalizesReviewConflictAndOptionsWithoutChangingValues()
    {
        RegistrationTextCatalog.GetReviewText("Phone", "hi-IN").Should().NotBe("Phone");
        RegistrationTextCatalog.GetOptionLabel(new RegistrationOption("ProvideServices", "Provide Services"), "hi-IN").Should().NotBe("Provide Services");
        RegistrationTextCatalog.GetConflictPrompt(new RegistrationConflict(RegistrationQuestionKeys.PhoneNumber, ["9876543210", "9123456789"], "internal"), "hi-IN").Should().NotContain("internal");
        RegistrationTextCatalog.GetReviewText("9876543210", "hi-IN").Should().Be("9876543210");
    }

    [Fact]
    public void LanguageSwitchPresentation_DoesNotAlterRateDobOrPhoneParsing()
    {
        var state = new RegistrationConversationState { CurrentQuestionKey = RegistrationQuestionKeys.PhoneNumber, LanguageCode = "en-IN" };
        Answer(state, RegistrationQuestionKeys.PhoneNumber, value: "9876543210");
        state.LanguageCode = "hi-IN";
        Answer(state, RegistrationQuestionKeys.DateOfBirth, date: new DateTime(1995, 3, 12));
        state.LanguageCode = "bn-IN";
        state.WantsProvider = true;
        Answer(state, RegistrationQuestionKeys.ProviderRate, value: "1200.50");

        state.PhoneNumber.Should().Be("9876543210");
        state.DateOfBirth.Should().Be(new DateTime(1995, 3, 12));
        state.ProviderRate.Should().Be(1200.50m);
    }

    [Theory]
    [InlineData("I want to provide services and sell products", true, true)]
    [InlineData("Main service provide karna chahta hoon aur products bhi sell karna chahta hoon", true, true)]
    [InlineData("Mujhe provider nahi banna", false, false)]
    public void IntentSentence_ExtractsProviderAndSellerChoices(string input, bool provider, bool seller)
    {
        var state = new RegistrationConversationState { CurrentQuestionKey = RegistrationQuestionKeys.Intent };
        var result = _conversation.ApplyAnswer(state, new RegistrationAnswer(RegistrationQuestionKeys.Intent, Value: input));

        result.Succeeded.Should().BeTrue(result.Error);
        state.WantsProvider.Should().Be(provider);
        state.WantsSeller.Should().Be(seller);
    }

    [Fact]
    public void RemovingProviderIntent_RetainsSellerBranch()
    {
        var state = new RegistrationConversationState
        {
            CurrentQuestionKey = RegistrationQuestionKeys.Intent,
            WantsProvider = true,
            WantsSeller = true
        };
        state.Answers[RegistrationQuestionKeys.Intent] = new RegistrationAnswer(
            RegistrationQuestionKeys.Intent,
            Values: ["ProvideServices", "SellProducts"]);
        state.ProviderProfession = "Electrician";
        state.SellerStoreName = "Local Store";

        var result = _conversation.ApplyAnswer(state, new RegistrationAnswer(
            RegistrationQuestionKeys.Intent,
            Value: "Provider nahi chahiye."));

        result.Succeeded.Should().BeTrue(result.Error);
        state.WantsProvider.Should().BeFalse();
        state.WantsSeller.Should().BeTrue();
        state.ProviderProfession.Should().BeEmpty();
        state.SellerStoreName.Should().Be("Local Store");
    }

    [Fact]
    public void MultiplePhones_CreateConflictWithoutSelectingOne()
    {
        var state = new RegistrationConversationState { CurrentQuestionKey = RegistrationQuestionKeys.PhoneNumber };

        var result = _conversation.ApplyAnswer(state, new RegistrationAnswer(
            RegistrationQuestionKeys.PhoneNumber,
            Value: "My numbers are 9876543210 and 9123456789."));

        result.Succeeded.Should().BeTrue(result.Error);
        state.PhoneNumber.Should().BeEmpty();
        state.Conflicts.Should().ContainSingle().Which.FieldKey.Should().Be(RegistrationQuestionKeys.PhoneNumber);
        state.Conflicts[0].CandidateValues.Should().HaveCount(2);
    }

    [Fact]
    public void PhoneConflictResolution_StoresChosenNumber()
    {
        var state = new RegistrationConversationState { CurrentQuestionKey = RegistrationQuestionKeys.PhoneNumber };
        _conversation.ApplyAnswer(state, new RegistrationAnswer(
            RegistrationQuestionKeys.PhoneNumber,
            Value: "My numbers are 9876543210 and 9123456789."));

        var result = _conversation.ResolveConflict(state, "9123456789");

        result.Succeeded.Should().BeTrue(result.Error);
        state.PhoneNumber.Should().Be("9123456789");
        state.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public void MultipleDates_CreateDobConflict()
    {
        var state = new RegistrationConversationState { CurrentQuestionKey = RegistrationQuestionKeys.DateOfBirth };
        var result = _conversation.ApplyAnswer(state, new RegistrationAnswer(
            RegistrationQuestionKeys.DateOfBirth,
            Value: "My birthday is 12 March 1995 or 13 March 1995."));

        result.Succeeded.Should().BeTrue(result.Error);
        state.DateOfBirth.Should().NotBe(default);
        state.Conflicts.Should().ContainSingle().Which.FieldKey.Should().Be(RegistrationQuestionKeys.DateOfBirth);
    }

    [Fact]
    public void ThreePartName_CreatesMappingConflict()
    {
        var state = new RegistrationConversationState { CurrentQuestionKey = RegistrationQuestionKeys.FirstName };
        var result = _conversation.ApplyAnswer(state, new RegistrationAnswer(
            RegistrationQuestionKeys.FirstName,
            Value: "My name is Arnab Kumar Panda."));

        result.Succeeded.Should().BeTrue(result.Error);
        state.FirstName.Should().BeEmpty();
        state.Conflicts.Should().ContainSingle().Which.FieldKey.Should().Be(RegistrationQuestionKeys.FirstName);
    }

    [Theory]
    [InlineData("Mera naam Arnab Panda hai.")]
    [InlineData("मेरा नाम Arnab Panda है।")]
    public void HindiAndHinglishNameSentences_AreExtracted(string input)
    {
        var state = new RegistrationConversationState { CurrentQuestionKey = RegistrationQuestionKeys.FirstName };
        var result = _conversation.ApplyAnswer(state, new RegistrationAnswer(RegistrationQuestionKeys.FirstName, Value: input));

        result.Succeeded.Should().BeTrue(result.Error);
        state.FirstName.Should().Be("Arnab");
        state.LastName.Should().Be("Panda");
    }

    [Fact]
    public void HinglishContactProfessionalSentences_AreExtracted()
    {
        var state = new RegistrationConversationState { WantsProvider = true };

        Answer(state, RegistrationQuestionKeys.PhoneNumber, value: "Mera number 9876543210 hai.");
        Answer(state, RegistrationQuestionKeys.Email, value: "Mera email arnab@example.com hai.");
        Answer(state, RegistrationQuestionKeys.DateOfBirth, value: "Meri date of birth 12 March 1995 hai.");
        Answer(state, RegistrationQuestionKeys.ProviderYears, value: "Mera experience 7 saal hai.");
        Answer(state, RegistrationQuestionKeys.ProviderRate, value: "Mera rate 1200 rupees hai.");

        state.PhoneNumber.Should().Be("9876543210");
        state.Email.Should().Be("arnab@example.com");
        state.DateOfBirth.Should().Be(new DateTime(1995, 3, 12));
        state.ProviderYearsOfExperience.Should().Be(7);
        state.ProviderRate.Should().Be(1200);
    }

    [Fact]
    public void FirstNameSentence_ExtractsFullNameAndSkipsLastName()
    {
        var state = new RegistrationConversationState();

        Answer(state, RegistrationQuestionKeys.Intent, values: []);
        Answer(state, RegistrationQuestionKeys.FirstName, value: "My name is Arnab Panda but I like being called Arnab");

        state.FirstName.Should().Be("Arnab");
        state.LastName.Should().Be("Panda");
        state.Answers.Should().ContainKey(RegistrationQuestionKeys.LastName);
        _conversation.GetCurrentQuestion(state).Key.Should().Be(RegistrationQuestionKeys.DateOfBirth);
    }

    [Fact]
    public void FirstNameQuestion_ExtractsNameAndContactFieldsFromOneSentence()
    {
        var state = new RegistrationConversationState();

        Answer(state, RegistrationQuestionKeys.Intent, values: []);
        Answer(state, RegistrationQuestionKeys.FirstName,
            value: "My name is Arnab Panda and my email is arnab@example.com and my phone number is 9876543210.");

        state.FirstName.Should().Be("Arnab");
        state.LastName.Should().Be("Panda");
        state.Email.Should().Be("arnab@example.com");
        state.PhoneNumber.Should().Be("9876543210");
        _conversation.GetCurrentQuestion(state).Key.Should().Be(RegistrationQuestionKeys.DateOfBirth);
    }

    [Fact]
    public void DateOfBirthSentence_ExtractsDateAndPhoneWithoutInferringAge()
    {
        var state = new RegistrationConversationState { CurrentQuestionKey = RegistrationQuestionKeys.DateOfBirth };

        var result = _conversation.ApplyAnswer(state, new RegistrationAnswer(
            RegistrationQuestionKeys.DateOfBirth,
            Value: "My date of birth is 12 March 1995 and my number is 9876543210."));

        result.Succeeded.Should().BeTrue(result.Error);
        state.DateOfBirth.Should().Be(new DateTime(1995, 3, 12));
        state.PhoneNumber.Should().Be("9876543210");
    }

    [Fact]
    public void LastNameCorrection_ReplacesPreviousValue()
    {
        var state = new RegistrationConversationState { CurrentQuestionKey = RegistrationQuestionKeys.LastName };
        state.FirstName = "Arnab";
        state.LastName = "Panda";
        state.Answers[RegistrationQuestionKeys.FirstName] = new RegistrationAnswer(RegistrationQuestionKeys.FirstName, "Arnab");

        var result = _conversation.ApplyAnswer(state, new RegistrationAnswer(
            RegistrationQuestionKeys.LastName,
            Value: "No, my surname is Pandey."));

        result.Succeeded.Should().BeTrue(result.Error);
        state.LastName.Should().Be("Pandey");
        state.FieldMetadata[RegistrationQuestionKeys.LastName].Status.Should().Be(RegistrationFieldStatus.Confirmed);
    }

    [Fact]
    public void UnpromptedCorrection_IsAppliedAndBotKeepsNextMissingQuestion()
    {
        var state = new RegistrationConversationState { CurrentQuestionKey = RegistrationQuestionKeys.DateOfBirth };
        state.FirstName = "Arnab";
        state.LastName = "Panda";
        state.Answers[RegistrationQuestionKeys.FirstName] = new RegistrationAnswer(RegistrationQuestionKeys.FirstName, "Arnab");
        state.Answers[RegistrationQuestionKeys.LastName] = new RegistrationAnswer(RegistrationQuestionKeys.LastName, "Panda");
        state.Answers[RegistrationQuestionKeys.Intent] = new RegistrationAnswer(RegistrationQuestionKeys.Intent, Values: []);

        var result = _conversation.ApplyAnswer(state, new RegistrationAnswer(
            RegistrationQuestionKeys.DateOfBirth,
            Value: "No, my surname is Pandey."));

        result.Succeeded.Should().BeTrue(result.Error);
        state.LastName.Should().Be("Pandey");
        state.CurrentQuestionKey.Should().Be(RegistrationQuestionKeys.DateOfBirth);
    }

    [Fact]
    public void AgeSentence_DoesNotInventDateOfBirth()
    {
        var state = new RegistrationConversationState { CurrentQuestionKey = RegistrationQuestionKeys.DateOfBirth };

        var result = _conversation.ApplyAnswer(state, new RegistrationAnswer(
            RegistrationQuestionKeys.DateOfBirth,
            Value: "I am 31 years old."));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("Date of birth");
    }

    [Fact]
    public void SentenceAnswers_ExtractContactAndCommercialNumbers()
    {
        var state = new RegistrationConversationState
        {
            WantsProvider = true
        };

        Answer(state, RegistrationQuestionKeys.PhoneNumber, value: "My phone number is +91 98765 43210.");
        Answer(state, RegistrationQuestionKeys.Email, value: "Please use arnab.provider1@gmail.com for login.");
        Answer(state, RegistrationQuestionKeys.ProviderYears, value: "I have 7 years of experience.");
        Answer(state, RegistrationQuestionKeys.ProviderRate, value: "I charge around 1,200 rupees.");

        state.PhoneNumber.Should().Be("+919876543210");
        state.Email.Should().Be("arnab.provider1@gmail.com");
        state.ProviderYearsOfExperience.Should().Be(7);
        state.ProviderRate.Should().Be(1200);
    }

    [Fact]
    public void LocationSentence_SplitsAreaCityAndState()
    {
        var state = new RegistrationConversationState
        {
            WantsProvider = true,
            WantsSeller = true
        };

        Answer(state, RegistrationQuestionKeys.ProviderZone, value: "My area is Salt Lake, Kolkata, West Bengal");
        Answer(state, RegistrationQuestionKeys.SellerPickupAddress, value: "Pickup is Park Street, Kolkata, West Bengal");

        state.ProviderServiceAreaZone.Should().Be("Salt Lake");
        state.ProviderServiceAreaCity.Should().Be("Kolkata");
        state.ProviderServiceAreaState.Should().Be("West Bengal");
        state.SellerPickupAddress.Should().Be("Park Street");
        state.SellerCity.Should().Be("Kolkata");
        state.SellerState.Should().Be("West Bengal");
        state.Answers.Should().ContainKey(RegistrationQuestionKeys.ProviderCity);
        state.Answers.Should().ContainKey(RegistrationQuestionKeys.ProviderState);
        state.Answers.Should().ContainKey(RegistrationQuestionKeys.SellerCity);
        state.Answers.Should().ContainKey(RegistrationQuestionKeys.SellerState);
    }

    [Fact]
    public void CurrentLocationLabel_DoesNotBecomeOversizedCityOrState()
    {
        var state = new RegistrationConversationState
        {
            WantsProvider = true,
            CurrentQuestionKey = RegistrationQuestionKeys.ProviderZone
        };

        var result = _conversation.ApplyAnswer(state, new RegistrationAnswer(
            RegistrationQuestionKeys.ProviderZone,
            Value: "Current location: 22.743453, 88.350142. Accuracy about 105 meters. Map: https://www.openstreetmap.org/?mlat=22.743453&mlon=88.350142"));

        result.Succeeded.Should().BeTrue(result.Error);
        state.ProviderServiceAreaZone.Length.Should().BeLessThanOrEqualTo(100);
        state.ProviderServiceAreaCity.Should().Be("Kolkata");
        state.ProviderServiceAreaState.Should().Be("West Bengal");
        state.Answers.Should().NotContainKey(RegistrationQuestionKeys.ProviderCity);
        state.Answers.Should().NotContainKey(RegistrationQuestionKeys.ProviderState);
    }

    [Fact]
    public void ProviderUnderAge_IsRejectedBeforePayloadCreation()
    {
        var state = new RegistrationConversationState();

        Answer(state, RegistrationQuestionKeys.Intent, values: ["ProvideServices"]);
        Answer(state, RegistrationQuestionKeys.FirstName, value: "Young");
        Answer(state, RegistrationQuestionKeys.LastName, value: "Provider");
        var result = _conversation.ApplyAnswer(state, new RegistrationAnswer(
            RegistrationQuestionKeys.DateOfBirth,
            DateValue: DateTime.Today.AddYears(-17)));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("at least 18");
    }

    private void Answer(
        RegistrationConversationState state,
        string key,
        string? value = null,
        IReadOnlyList<string>? values = null,
        DateTime? date = null,
        int? integer = null,
        decimal? money = null)
    {
        state.CurrentQuestionKey = key;
        var result = _conversation.ApplyAnswer(state, new RegistrationAnswer(
            key,
            Value: value,
            Values: values,
            DateValue: date,
            IntValue: integer,
            DecimalValue: money));

        result.Succeeded.Should().BeTrue(result.Error);
    }
}
