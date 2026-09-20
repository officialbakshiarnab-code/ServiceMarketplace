using System.ComponentModel.DataAnnotations;

namespace ServiceMarketplace.UI.Shared.Auth.Registration;

public interface IRegistrationConversationService
{
    RegistrationQuestion GetCurrentQuestion(RegistrationConversationState state);
    IReadOnlyList<RegistrationQuestion> GetQuestions(RegistrationConversationState state);
    RegistrationStepResult ApplyAnswer(RegistrationConversationState state, RegistrationAnswer answer);
    RegistrationStepResult GoBack(RegistrationConversationState state);
    void GoToQuestion(RegistrationConversationState state, string questionKey);
    IReadOnlyList<RegistrationProgressItem> GetProgress(RegistrationConversationState state);
    IReadOnlyList<RegistrationReviewItem> GetReview(RegistrationConversationState state);
    RegistrationStepResult ResolveConflict(RegistrationConversationState state, string value);
}

public sealed class RegistrationConversationService : IRegistrationConversationService
{
    private static readonly IReadOnlyList<RegistrationInputMode> KeyboardVoice = [RegistrationInputMode.Keyboard, RegistrationInputMode.Voice];
    private static readonly IReadOnlyList<RegistrationInputMode> KeyboardOnly = [RegistrationInputMode.Keyboard];
    private static readonly IReadOnlyList<RegistrationInputMode> SelectionVoice = [RegistrationInputMode.Selection, RegistrationInputMode.Voice];
    private static readonly IReadOnlyList<RegistrationInputMode> SelectionOnly = [RegistrationInputMode.Selection];

    public RegistrationQuestion GetCurrentQuestion(RegistrationConversationState state)
    {
        var questions = GetQuestions(state);
        var current = questions.FirstOrDefault(q => q.Key == state.CurrentQuestionKey);
        if (current != null)
            return current;

        state.CurrentQuestionKey = questions[0].Key;
        return questions[0];
    }

    public IReadOnlyList<RegistrationQuestion> GetQuestions(RegistrationConversationState state)
    {
        var questions = new List<RegistrationQuestion>
        {
            new(RegistrationQuestionKeys.Intent, "What do you want to do with ServiceMarketplace?", RegistrationQuestionType.MultipleChoice, RegistrationStage.Intent, true, SelectionOnly, GetIntentOptions(), "Request Services and Buy Products are included with every account."),
            new(RegistrationQuestionKeys.FirstName, "What is your first name?", RegistrationQuestionType.Text, RegistrationStage.Account, true, KeyboardVoice),
            new(RegistrationQuestionKeys.LastName, "What is your last name?", RegistrationQuestionType.Text, RegistrationStage.Account, true, KeyboardVoice),
            new(RegistrationQuestionKeys.DateOfBirth, "What is your date of birth?", RegistrationQuestionType.Date, RegistrationStage.Account, true, KeyboardOnly, HelpText: "Service providers must be at least 18 years old."),
            new(RegistrationQuestionKeys.PhoneNumber, "What is your mobile number?", RegistrationQuestionType.Phone, RegistrationStage.Contact, true, KeyboardVoice, HelpText: "Phone stays private unless contact sharing is approved."),
            new(RegistrationQuestionKeys.Email, "What email should be used for login?", RegistrationQuestionType.Email, RegistrationStage.Contact, true, KeyboardVoice),
            new(RegistrationQuestionKeys.Password, "Create a password.", RegistrationQuestionType.Password, RegistrationStage.Security, true, KeyboardOnly, HelpText: "For your privacy, passwords must be typed."),
            new(RegistrationQuestionKeys.ConfirmPassword, "Confirm your password.", RegistrationQuestionType.Password, RegistrationStage.Security, true, KeyboardOnly)
        };

        if (state.WantsProvider)
            questions.AddRange(GetProviderQuestions(state));

        if (state.WantsSeller)
            questions.AddRange(GetSellerQuestions());

        if (ShouldAskBusinessQuestions(state))
            questions.AddRange(GetBusinessQuestions());

        if (state.WantsProvider)
            questions.Add(new(RegistrationQuestionKeys.GovernmentId, "Do you want to upload identity evidence now?", RegistrationQuestionType.Upload, RegistrationStage.Verification, false, [RegistrationInputMode.Upload], HelpText: "Your document will be submitted for review. You can skip this and continue application review after login."));

        questions.Add(new(RegistrationQuestionKeys.Review, "Review and create your account.", RegistrationQuestionType.Review, RegistrationStage.Review, true, KeyboardOnly));
        return questions;
    }

    public RegistrationStepResult ApplyAnswer(RegistrationConversationState state, RegistrationAnswer answer)
    {
        var current = GetCurrentQuestion(state);
        if (answer.Key != current.Key)
            answer = answer with { Key = current.Key };

        if (current.Key == RegistrationQuestionKeys.Intent && !string.IsNullOrWhiteSpace(answer.Value))
        {
            var parsedIntent = RegistrationAnswerInterpreter.ParseIntent(answer.Value);
            var intentValues = state.Answers.ContainsKey(RegistrationQuestionKeys.Intent)
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    state.WantsProvider ? "ProvideServices" : string.Empty,
                    state.WantsSeller ? "SellProducts" : string.Empty
                }
                : new HashSet<string>(parsedIntent, StringComparer.OrdinalIgnoreCase);

            intentValues.Remove(string.Empty);
            if (RegistrationAnswerInterpreter.RemovesProvider(answer.Value))
                intentValues.Remove("ProvideServices");
            else if (RegistrationAnswerInterpreter.MentionsProvider(answer.Value))
                intentValues.Add("ProvideServices");
            if (RegistrationAnswerInterpreter.RemovesSeller(answer.Value))
                intentValues.Remove("SellProducts");
            else if (RegistrationAnswerInterpreter.MentionsSeller(answer.Value))
                intentValues.Add("SellProducts");

            parsedIntent = intentValues.ToList();
            answer = answer with { Values = parsedIntent };
            state.WantsProvider = parsedIntent.Contains("ProvideServices");
            state.WantsSeller = parsedIntent.Contains("SellProducts");
        }

        // Explicit option values (including catalog GUIDs) must not be interpreted as
        // unrelated free-text answers such as phone numbers.
        var isExplicitSelection = current.Options?.Any(option =>
            string.Equals(option.Value, answer.Value, StringComparison.OrdinalIgnoreCase)) == true;
        var detectedConflicts = isExplicitSelection
            ? [] : RegistrationAnswerInterpreter.DetectConflicts(answer.Value);
        if (detectedConflicts.Count > 0)
        {
            state.Conflicts.RemoveAll(c => detectedConflicts.Any(d => d.FieldKey == c.FieldKey));
            state.Conflicts.AddRange(detectedConflicts);
            state.CurrentQuestionKey = detectedConflicts[0].FieldKey;
            return new RegistrationStepResult(true);
        }

        var candidates = !isExplicitSelection && !string.IsNullOrWhiteSpace(answer.Value) &&
            current.Type is not RegistrationQuestionType.Password
                ? RegistrationAnswerInterpreter.Process(answer.Value, state)
                : [];

        if (candidates.Count > 0 && !candidates.Any(c => string.Equals(c.Field, current.Key, StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var candidate in candidates.Where(c => c.Operation != RegistrationAnswerOperation.Remove))
            {
                var candidateQuestion = GetQuestions(state).FirstOrDefault(q => q.Key == candidate.Field);
                if (candidateQuestion == null)
                    continue;

                var candidateAnswer = new RegistrationAnswer(candidate.Field, Value: candidate.Value, Values: candidate.Values);
                var candidateValidation = ApplyCurrentAnswer(state, candidateQuestion, candidateAnswer);
                if (candidateValidation.Succeeded)
                {
                    state.Answers[candidate.Field] = candidateAnswer;
                    MarkField(state, candidate.Field, candidate.Operation == RegistrationAnswerOperation.Correct
                        ? RegistrationFieldStatus.Confirmed
                        : RegistrationFieldStatus.Candidate, candidate.Confidence);
                }
            }

            state.CurrentQuestionKey = GetQuestions(state)
                .FirstOrDefault(q => q.Key != RegistrationQuestionKeys.Review && !state.Answers.ContainsKey(q.Key))?.Key
                ?? RegistrationQuestionKeys.Review;
            return new RegistrationStepResult(true);
        }

        var validation = ApplyCurrentAnswer(state, current, answer);
        if (!validation.Succeeded)
            return validation;

        state.Answers[current.Key] = answer;
        MarkField(state, current.Key, RegistrationFieldStatus.Confirmed, 1m);

        foreach (var candidate in candidates.Where(c => !string.Equals(c.Field, current.Key, StringComparison.OrdinalIgnoreCase)))
        {
            var candidateQuestion = GetQuestions(state).FirstOrDefault(q => q.Key == candidate.Field);
            if (candidateQuestion == null || candidate.Operation == RegistrationAnswerOperation.Remove)
                continue;

            var candidateAnswer = new RegistrationAnswer(candidate.Field, Value: candidate.Value, Values: candidate.Values);
            var candidateValidation = ApplyCurrentAnswer(state, candidateQuestion, candidateAnswer);
            if (candidateValidation.Succeeded)
            {
                state.Answers[candidate.Field] = candidateAnswer;
                MarkField(state, candidate.Field, candidate.Operation == RegistrationAnswerOperation.Correct
                    ? RegistrationFieldStatus.Confirmed
                    : RegistrationFieldStatus.Candidate, candidate.Confidence);
            }
        }
        var previousKey = current.Key;
        var questions = GetQuestions(state);
        var currentIndex = questions.ToList().FindIndex(q => q.Key == previousKey);
        var next = GetNextQuestion(state, questions, currentIndex, previousKey);

        if (next.Key != previousKey)
            state.QuestionHistory.Add(previousKey);

        state.CurrentQuestionKey = next.Key;
        return new RegistrationStepResult(true);
    }

    public RegistrationStepResult ResolveConflict(RegistrationConversationState state, string value)
    {
        var conflict = state.Conflicts.FirstOrDefault();
        if (conflict == null)
            return new RegistrationStepResult(false, "No active conflict.");
        if (!conflict.CandidateValues.Contains(value, StringComparer.Ordinal))
            return new RegistrationStepResult(false, "Choose one of the detected values.");

        var question = GetQuestions(state).FirstOrDefault(q => q.Key == conflict.FieldKey);
        if (question == null)
            return new RegistrationStepResult(false, "The conflicted field is no longer active.");

        var answerValue = value;
        if (conflict.FieldKey == RegistrationQuestionKeys.FirstName && value.Contains('|'))
        {
            var parts = value.Split('|', 2);
            state.FirstName = RequireText(parts[0], "First name", 100);
            state.LastName = RequireText(parts[1], "Last name", 100);
            state.Answers[RegistrationQuestionKeys.FirstName] = new RegistrationAnswer(RegistrationQuestionKeys.FirstName, state.FirstName);
            state.Answers[RegistrationQuestionKeys.LastName] = new RegistrationAnswer(RegistrationQuestionKeys.LastName, state.LastName);
            state.Conflicts.Remove(conflict);
            state.CurrentQuestionKey = GetQuestions(state).FirstOrDefault(q => q.Key == RegistrationQuestionKeys.DateOfBirth)?.Key ?? RegistrationQuestionKeys.Review;
            return new RegistrationStepResult(true);
        }

        var validation = ApplyCurrentAnswer(state, question, new RegistrationAnswer(conflict.FieldKey, Value: answerValue));
        if (!validation.Succeeded)
            return validation;

        state.Answers[conflict.FieldKey] = new RegistrationAnswer(conflict.FieldKey, Value: value);
        state.Conflicts.Remove(conflict);
        var questions = GetQuestions(state);
        var index = questions.ToList().FindIndex(q => q.Key == conflict.FieldKey);
        state.CurrentQuestionKey = GetNextQuestion(state, questions, index, conflict.FieldKey).Key;
        return new RegistrationStepResult(true);
    }

    public RegistrationStepResult GoBack(RegistrationConversationState state)
    {
        while (state.QuestionHistory.Count > 0)
        {
            var previous = state.QuestionHistory[^1];
            state.QuestionHistory.RemoveAt(state.QuestionHistory.Count - 1);
            if (GetQuestions(state).Any(q => q.Key == previous))
            {
                state.CurrentQuestionKey = previous;
                return new RegistrationStepResult(true);
            }
        }

        return new RegistrationStepResult(false, "Already at the first question.");
    }

    public void GoToQuestion(RegistrationConversationState state, string questionKey)
    {
        if (GetQuestions(state).Any(q => q.Key == questionKey))
        {
            state.QuestionHistory.Add(state.CurrentQuestionKey ?? RegistrationQuestionKeys.Intent);
            state.CurrentQuestionKey = questionKey;
        }
    }

    public IReadOnlyList<RegistrationProgressItem> GetProgress(RegistrationConversationState state)
    {
        var current = GetCurrentQuestion(state);
        var orderedStages = GetQuestions(state)
            .Select(q => q.Stage)
            .Distinct()
            .ToList();
        var currentIndex = orderedStages.IndexOf(current.Stage);

        return orderedStages
            .Select((stage, index) => new RegistrationProgressItem(
                stage,
                GetStageLabel(stage),
                stage == current.Stage,
                index < currentIndex))
            .ToList();
    }

    public IReadOnlyList<RegistrationReviewItem> GetReview(RegistrationConversationState state)
    {
        var items = new List<RegistrationReviewItem>
        {
            new("Account", "Name", $"{state.FirstName} {state.LastName}".Trim(), RegistrationQuestionKeys.FirstName),
            new("Account", "Date of birth", state.DateOfBirth.ToString("yyyy-MM-dd"), RegistrationQuestionKeys.DateOfBirth),
            new("Contact", "Phone", MaskPhone(state.PhoneNumber), RegistrationQuestionKeys.PhoneNumber),
            new("Contact", "Email", state.Email, RegistrationQuestionKeys.Email),
            new("Marketplace", "Included access", "Request Services + Buy Products", RegistrationQuestionKeys.Intent),
            new("Marketplace", "Commercial review", GetCommercialReviewText(state), RegistrationQuestionKeys.Intent)
        };

        if (state.WantsProvider)
        {
            items.Add(new("Provider", "Provider type", state.ProviderType, RegistrationQuestionKeys.ProviderType));
            items.Add(new("Provider", "Category", state.ProviderPrimaryCategory, RegistrationQuestionKeys.ProviderCategory));
            items.Add(new("Provider", "Profession", state.ProviderProfession, RegistrationQuestionKeys.ProviderProfession));
            items.Add(new("Provider", "Skills", state.ProviderSkills, RegistrationQuestionKeys.ProviderSkills));
            items.Add(new("Provider", "Area", JoinNonEmpty(state.ProviderServiceAreaZone, state.ProviderServiceAreaCity, state.ProviderServiceAreaState), RegistrationQuestionKeys.ProviderZone));
            items.Add(new("Provider", "Rate", state.ProviderRate > 0 ? state.ProviderRate.ToString("0.##") : "Not set", RegistrationQuestionKeys.ProviderRate));
        }

        if (state.WantsSeller)
        {
            items.Add(new("Seller", "Store", state.SellerStoreName, RegistrationQuestionKeys.SellerStoreName));
            items.Add(new("Seller", "Product focus", state.SellerProductConditionFocus, RegistrationQuestionKeys.SellerConditionFocus));
            items.Add(new("Seller", "Pickup area", JoinNonEmpty(state.SellerPickupAddress, state.SellerCity, state.SellerState), RegistrationQuestionKeys.SellerPickupAddress));
        }

        if (ShouldAskBusinessQuestions(state))
        {
            items.Add(new("Business", "Trading name", EmptyAsNotProvided(state.BusinessTradingName), RegistrationQuestionKeys.BusinessTradingName));
            items.Add(new("Business", "Requested seats", state.RequestedSeatLimit.ToString(), RegistrationQuestionKeys.RequestedSeats));
            items.Add(new("Business", "GSTIN", EmptyAsNotProvided(state.BusinessGstin), RegistrationQuestionKeys.BusinessGstin));
        }

        if (state.WantsProvider)
            items.Add(new("Verification", "Identity document", state.GovernmentIdImage == null ? "Not submitted now" : state.GovernmentIdImage.Name, RegistrationQuestionKeys.GovernmentId));

        return items;
    }

    private static RegistrationStepResult ApplyCurrentAnswer(RegistrationConversationState state, RegistrationQuestion question, RegistrationAnswer answer)
    {
        try
        {
            switch (question.Key)
            {
                case RegistrationQuestionKeys.Intent:
                    var intentValues = answer.Values ?? RegistrationAnswerInterpreter.ParseIntent(answer.Value);
                    state.WantsProvider = intentValues.Contains("ProvideServices");
                    state.WantsSeller = intentValues.Contains("SellProducts");
                    if (!state.WantsProvider)
                        ClearProvider(state);
                    if (!state.WantsSeller)
                        ClearSeller(state);
                    if (!ShouldAskBusinessQuestions(state))
                        ClearBusiness(state);
                    break;
                case RegistrationQuestionKeys.FirstName:
                    ApplyNameAnswer(state, answer.Value);
                    break;
                case RegistrationQuestionKeys.LastName:
                    state.LastName = RequireText(RegistrationAnswerInterpreter.ParseLastName(answer.Value), "Last name", 100);
                    break;
                case RegistrationQuestionKeys.DateOfBirth:
                    state.DateOfBirth = answer.DateValue ?? ParseDateAnswer(answer.Value);
                    if (state.DateOfBirth.Date > DateTime.Today)
                        throw new ArgumentException("Date of birth cannot be in the future.");
                    if (state.WantsProvider && CalculateAge(state.DateOfBirth) < 18)
                        throw new ArgumentException("Service providers must be at least 18 years old.");
                    break;
                case RegistrationQuestionKeys.PhoneNumber:
                    var phone = RequireText(RegistrationAnswerInterpreter.ParsePhone(answer.Value), "Phone number", 20);
                    var digits = phone.Count(char.IsDigit);
                    if (digits is < 10 or > 15)
                        throw new ArgumentException("Phone number must contain 10 to 15 digits.");
                    state.PhoneNumber = phone;
                    break;
                case RegistrationQuestionKeys.Email:
                    var email = RequireText(RegistrationAnswerInterpreter.ParseEmail(answer.Value), "Email", 150);
                    if (!new EmailAddressAttribute().IsValid(email))
                        throw new ArgumentException("Enter a valid email address.");
                    state.Email = email;
                    break;
                case RegistrationQuestionKeys.Password:
                    state.Password = RequireText(answer.Value, "Password", 200);
                    if (state.Password.Length < 6)
                        throw new ArgumentException("Password must be at least 6 characters.");
                    break;
                case RegistrationQuestionKeys.ConfirmPassword:
                    state.ConfirmPassword = RequireText(answer.Value, "Confirm password", 200);
                    if (!string.Equals(state.Password, state.ConfirmPassword, StringComparison.Ordinal))
                        throw new ArgumentException("Passwords do not match.");
                    break;
                case RegistrationQuestionKeys.ProviderType:
                    state.ProviderType = RequireChoice(answer.Value, "Provider type");
                    if (!ShouldAskBusinessQuestions(state))
                        ClearBusiness(state);
                    break;
                case RegistrationQuestionKeys.ProviderCategory:
                    ApplyProviderCategory(state, RequireChoice(answer.Value, "Provider category"));
                    break;
                case RegistrationQuestionKeys.ProviderProfession:
                    state.ProviderProfession = RequireText(answer.Value, "Profession", 150);
                    break;
                case RegistrationQuestionKeys.ProviderSkills:
                    state.ProviderSkills = RequireText(answer.Value, "Provider skills", 500);
                    break;
                case RegistrationQuestionKeys.ProviderYears:
                    state.ProviderYearsOfExperience = RegistrationAnswerInterpreter.ParseInteger(answer.Value, answer.IntValue);
                    if (state.ProviderYearsOfExperience is < 0 or > 80)
                        throw new ArgumentException("Years of experience must be between 0 and 80.");
                    break;
                case RegistrationQuestionKeys.ProviderZone:
                    ApplyProviderLocationAnswer(state, answer.Value);
                    break;
                case RegistrationQuestionKeys.ProviderCity:
                    state.ProviderServiceAreaCity = RequireText(answer.Value, "Provider service city", 100);
                    break;
                case RegistrationQuestionKeys.ProviderState:
                    state.ProviderServiceAreaState = RequireText(answer.Value, "Provider service state", 100);
                    break;
                case RegistrationQuestionKeys.ProviderPricing:
                    state.ProviderPricingType = RequireChoice(answer.Value, "Pricing type");
                    break;
                case RegistrationQuestionKeys.ProviderRate:
                    state.ProviderRate = RegistrationAnswerInterpreter.ParseDecimal(answer.Value, answer.DecimalValue) ?? 0;
                    if (state.ProviderRate <= 0)
                        throw new ArgumentException("Provider rate must be greater than zero.");
                    break;
                case RegistrationQuestionKeys.ProviderAvailability:
                    state.ProviderAvailability = OptionalText(answer.Value, 250);
                    break;
                case RegistrationQuestionKeys.ProviderLanguages:
                    state.ProviderLanguages = OptionalText(answer.Value, 150);
                    break;
                case RegistrationQuestionKeys.SellerStoreName:
                    state.SellerStoreName = RequireText(answer.Value, "Store name", 150);
                    break;
                case RegistrationQuestionKeys.SellerBusinessName:
                    state.SellerBusinessName = OptionalText(answer.Value, 150);
                    break;
                case RegistrationQuestionKeys.SellerGstin:
                    state.SellerGstin = OptionalText(answer.Value, 30);
                    break;
                case RegistrationQuestionKeys.SellerProductCategories:
                    state.SellerProductCategories = OptionalText(answer.Value, 250);
                    break;
                case RegistrationQuestionKeys.SellerConditionFocus:
                    state.SellerProductConditionFocus = RequireChoice(answer.Value, "Product focus");
                    break;
                case RegistrationQuestionKeys.SellerPickupAddress:
                    ApplySellerPickupAnswer(state, answer.Value);
                    break;
                case RegistrationQuestionKeys.SellerCity:
                    state.SellerCity = RequireText(answer.Value, "Seller city", 100);
                    break;
                case RegistrationQuestionKeys.SellerState:
                    state.SellerState = RequireText(answer.Value, "Seller state", 100);
                    break;
                case RegistrationQuestionKeys.SellerDescription:
                    state.SellerDescription = OptionalText(answer.Value, 1000);
                    break;
                case RegistrationQuestionKeys.BusinessLegalName:
                    state.BusinessLegalName = OptionalText(answer.Value, 150);
                    break;
                case RegistrationQuestionKeys.BusinessTradingName:
                    state.BusinessTradingName = OptionalText(answer.Value, 150);
                    break;
                case RegistrationQuestionKeys.BusinessType:
                    state.BusinessType = RequireChoice(answer.Value, "Business type");
                    break;
                case RegistrationQuestionKeys.BusinessGstin:
                    state.BusinessGstin = OptionalText(answer.Value, 30);
                    break;
                case RegistrationQuestionKeys.BusinessWebsite:
                    state.BusinessWebsiteOrDomain = OptionalText(answer.Value, 150);
                    break;
                case RegistrationQuestionKeys.RequestedSeats:
                    state.RequestedSeatLimit = RegistrationAnswerInterpreter.ParseInteger(answer.Value, answer.IntValue) ?? 5;
                    if (state.RequestedSeatLimit is not (5 or 10 or 20))
                        throw new ArgumentException("Requested seats must be 5, 10, or 20.");
                    break;
                case RegistrationQuestionKeys.BusinessRegisteredAddress:
                    state.BusinessRegisteredAddress = OptionalText(answer.Value, 500);
                    break;
                case RegistrationQuestionKeys.BusinessOperatingAddress:
                    state.BusinessOperatingAddress = OptionalText(answer.Value, 500);
                    break;
                case RegistrationQuestionKeys.GovernmentId:
                    state.GovernmentIdImage = answer.File;
                    break;
            }

            return new RegistrationStepResult(true);
        }
        catch (ArgumentException ex)
        {
            return new RegistrationStepResult(false, ex.Message);
        }
    }

    private static DateTime ParseDateAnswer(string? value)
    {
        var date = RegistrationAnswerInterpreter.ParseDate(value);
        if (date.HasValue)
            return date.Value;
        throw new ArgumentException("Date of birth is required.");
    }

    private static void MarkField(RegistrationConversationState state, string key, RegistrationFieldStatus status, decimal confidence)
    {
        if (state.FieldMetadata.TryGetValue(key, out var existing) &&
            existing.Status == RegistrationFieldStatus.Confirmed &&
            status == RegistrationFieldStatus.Candidate)
        {
            state.FieldMetadata[key] = existing with { Status = RegistrationFieldStatus.Conflict, ConflictValue = existing.Confidence.ToString() };
            return;
        }

        state.FieldMetadata[key] = new(status, confidence);
    }

    private static RegistrationQuestion GetNextQuestion(
        RegistrationConversationState state,
        IReadOnlyList<RegistrationQuestion> questions,
        int currentIndex,
        string previousKey)
    {
        if (currentIndex < 0)
            return questions[^1];

        for (var index = currentIndex + 1; index < questions.Count; index++)
        {
            var candidate = questions[index];
            if (candidate.Key == RegistrationQuestionKeys.Review || !state.Answers.ContainsKey(candidate.Key))
                return candidate;
        }

        return questions[^1];
    }

    private static void ApplyNameAnswer(RegistrationConversationState state, string? value)
    {
        var name = RegistrationAnswerInterpreter.ParseName(value);
        state.FirstName = RequireText(name.FirstName, "First name", 100);

        if (!string.IsNullOrWhiteSpace(name.LastName))
        {
            state.LastName = RequireText(name.LastName, "Last name", 100);
            state.Answers[RegistrationQuestionKeys.LastName] = new RegistrationAnswer(
                RegistrationQuestionKeys.LastName,
                Value: state.LastName);
        }
    }

    private static void ApplyProviderLocationAnswer(RegistrationConversationState state, string? value)
    {
        var location = RegistrationAnswerInterpreter.ParseLocation(value);
        state.ProviderServiceAreaZone = OptionalText(location.Primary, 100);

        if (!string.IsNullOrWhiteSpace(location.City))
        {
            state.ProviderServiceAreaCity = RequireText(location.City, "Provider service city", 100);
            state.Answers[RegistrationQuestionKeys.ProviderCity] = new RegistrationAnswer(
                RegistrationQuestionKeys.ProviderCity,
                Value: state.ProviderServiceAreaCity);
        }

        if (!string.IsNullOrWhiteSpace(location.State))
        {
            state.ProviderServiceAreaState = RequireText(location.State, "Provider service state", 100);
            state.Answers[RegistrationQuestionKeys.ProviderState] = new RegistrationAnswer(
                RegistrationQuestionKeys.ProviderState,
                Value: state.ProviderServiceAreaState);
        }
    }

    private static void ApplySellerPickupAnswer(RegistrationConversationState state, string? value)
    {
        var location = RegistrationAnswerInterpreter.ParseLocation(value);
        state.SellerPickupAddress = RequireText(location.Primary, "Pickup address", 500);

        if (!string.IsNullOrWhiteSpace(location.City))
        {
            state.SellerCity = RequireText(location.City, "Seller city", 100);
            state.Answers[RegistrationQuestionKeys.SellerCity] = new RegistrationAnswer(
                RegistrationQuestionKeys.SellerCity,
                Value: state.SellerCity);
        }

        if (!string.IsNullOrWhiteSpace(location.State))
        {
            state.SellerState = RequireText(location.State, "Seller state", 100);
            state.Answers[RegistrationQuestionKeys.SellerState] = new RegistrationAnswer(
                RegistrationQuestionKeys.SellerState,
                Value: state.SellerState);
        }
    }

    private static IReadOnlyList<RegistrationQuestion> GetProviderQuestions(RegistrationConversationState state)
    {
        var categoryOptions = state.ServiceCategories.Count > 0
            ? state.ServiceCategories
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .Select(c => new RegistrationOption(c.Id.ToString(), c.Name, c.Description))
                .ToList()
            : null;

        var categoryType = categoryOptions is { Count: > 0 }
            ? RegistrationQuestionType.SingleChoice
            : RegistrationQuestionType.Text;
        var categoryModes = categoryOptions is { Count: > 0 }
            ? SelectionVoice
            : KeyboardVoice;
        var skillPrompt = ProfessionQuestionCatalog.GetSkillPrompt(state.ProviderPrimaryCategorySlug, state.ProviderPrimaryCategory);
        var ratePrompt = ProfessionQuestionCatalog.GetRatePrompt(state.ProviderPrimaryCategorySlug, state.ProviderPrimaryCategory);

        return
        [
            new(RegistrationQuestionKeys.ProviderType, "Are you registering as an individual or business provider?", RegistrationQuestionType.SingleChoice, RegistrationStage.Professional, true, SelectionOnly, GetProviderTypeOptions()),
            new(RegistrationQuestionKeys.ProviderCategory, "What service category best fits your work?", categoryType, RegistrationStage.Professional, true, categoryModes, categoryOptions, "This reuses the marketplace service categories."),
            new(RegistrationQuestionKeys.ProviderProfession, "What is your profession or service title?", RegistrationQuestionType.Text, RegistrationStage.Professional, true, KeyboardVoice, HelpText: "Example: electrician, makeup artist, tutor, driver."),
            new(RegistrationQuestionKeys.ProviderSkills, skillPrompt, RegistrationQuestionType.TextArea, RegistrationStage.Professional, true, KeyboardVoice),
            new(RegistrationQuestionKeys.ProviderYears, "How many years of experience do you have?", RegistrationQuestionType.Number, RegistrationStage.Professional, false, KeyboardVoice),
            new(RegistrationQuestionKeys.ProviderZone, "Which local zone or area do you serve most?", RegistrationQuestionType.Text, RegistrationStage.Professional, false, KeyboardVoice),
            new(RegistrationQuestionKeys.ProviderCity, "Which city do you serve?", RegistrationQuestionType.Text, RegistrationStage.Professional, true, KeyboardVoice),
            new(RegistrationQuestionKeys.ProviderState, "Which state is that in?", RegistrationQuestionType.Text, RegistrationStage.Professional, true, KeyboardVoice),
            new(RegistrationQuestionKeys.ProviderPricing, "How do you usually price this service?", RegistrationQuestionType.SingleChoice, RegistrationStage.Professional, true, SelectionOnly, GetPricingOptions()),
            new(RegistrationQuestionKeys.ProviderRate, ratePrompt, RegistrationQuestionType.Currency, RegistrationStage.Professional, true, KeyboardVoice),
            new(RegistrationQuestionKeys.ProviderAvailability, "When are you generally available?", RegistrationQuestionType.Text, RegistrationStage.Professional, false, KeyboardVoice),
            new(RegistrationQuestionKeys.ProviderLanguages, "Which languages can you support?", RegistrationQuestionType.Text, RegistrationStage.Professional, false, KeyboardVoice)
        ];
    }

    private static IReadOnlyList<RegistrationQuestion> GetSellerQuestions()
    {
        return
        [
            new(RegistrationQuestionKeys.SellerStoreName, "What is your store or shop name?", RegistrationQuestionType.Text, RegistrationStage.Seller, true, KeyboardVoice),
            new(RegistrationQuestionKeys.SellerBusinessName, "What business name should be shown for seller review?", RegistrationQuestionType.Text, RegistrationStage.Seller, false, KeyboardVoice),
            new(RegistrationQuestionKeys.SellerGstin, "What is your GSTIN? Optional.", RegistrationQuestionType.Text, RegistrationStage.Seller, false, KeyboardVoice),
            new(RegistrationQuestionKeys.SellerProductCategories, "What product categories do you sell?", RegistrationQuestionType.Text, RegistrationStage.Seller, false, KeyboardVoice),
            new(RegistrationQuestionKeys.SellerConditionFocus, "Do you sell new products, used products, or both?", RegistrationQuestionType.SingleChoice, RegistrationStage.Seller, true, SelectionOnly, GetConditionOptions()),
            new(RegistrationQuestionKeys.SellerPickupAddress, "What pickup, shop, or godown address should admins review?", RegistrationQuestionType.TextArea, RegistrationStage.Seller, true, KeyboardVoice),
            new(RegistrationQuestionKeys.SellerCity, "Which city is the seller pickup location in?", RegistrationQuestionType.Text, RegistrationStage.Seller, true, KeyboardVoice),
            new(RegistrationQuestionKeys.SellerState, "Which state is the seller pickup location in?", RegistrationQuestionType.Text, RegistrationStage.Seller, true, KeyboardVoice),
            new(RegistrationQuestionKeys.SellerDescription, "Briefly describe your store. Optional.", RegistrationQuestionType.TextArea, RegistrationStage.Seller, false, KeyboardVoice)
        ];
    }

    private static IReadOnlyList<RegistrationQuestion> GetBusinessQuestions()
    {
        return
        [
            new(RegistrationQuestionKeys.BusinessLegalName, "What is the legal business name? Optional.", RegistrationQuestionType.Text, RegistrationStage.Business, false, KeyboardVoice),
            new(RegistrationQuestionKeys.BusinessTradingName, "What trading name should admins see? Optional.", RegistrationQuestionType.Text, RegistrationStage.Business, false, KeyboardVoice),
            new(RegistrationQuestionKeys.BusinessType, "What type of business is it?", RegistrationQuestionType.SingleChoice, RegistrationStage.Business, true, SelectionOnly, GetBusinessTypeOptions()),
            new(RegistrationQuestionKeys.BusinessGstin, "Business GSTIN, if available.", RegistrationQuestionType.Text, RegistrationStage.Business, false, KeyboardVoice),
            new(RegistrationQuestionKeys.BusinessWebsite, "Website or domain, if available.", RegistrationQuestionType.Text, RegistrationStage.Business, false, KeyboardVoice),
            new(RegistrationQuestionKeys.RequestedSeats, "How many team seats may be needed later?", RegistrationQuestionType.SingleChoice, RegistrationStage.Business, true, SelectionOnly, GetSeatOptions(), "This is informational only; no team accounts are created."),
            new(RegistrationQuestionKeys.BusinessRegisteredAddress, "Registered business address, if different.", RegistrationQuestionType.TextArea, RegistrationStage.Business, false, KeyboardVoice),
            new(RegistrationQuestionKeys.BusinessOperatingAddress, "Operating address, if different.", RegistrationQuestionType.TextArea, RegistrationStage.Business, false, KeyboardVoice)
        ];
    }

    private static void ApplyProviderCategory(RegistrationConversationState state, string value)
    {
        var selected = state.ServiceCategories.FirstOrDefault(c => c.Id.ToString().Equals(value, StringComparison.OrdinalIgnoreCase));
        var previousSlug = state.ProviderPrimaryCategorySlug;

        if (selected == null)
        {
            state.ProviderPrimaryCategory = RequireText(value, "Provider service category", 150);
            state.ProviderPrimaryCategorySlug = string.Empty;
        }
        else
        {
            state.ProviderPrimaryCategory = selected.Name;
            state.ProviderPrimaryCategorySlug = selected.Slug;
        }

        if (!string.Equals(previousSlug, state.ProviderPrimaryCategorySlug, StringComparison.OrdinalIgnoreCase))
        {
            state.ProviderSkills = string.Empty;
            state.Answers.Remove(RegistrationQuestionKeys.ProviderSkills);
        }
    }

    private static bool ShouldAskBusinessQuestions(RegistrationConversationState state)
    {
        return state.WantsSeller ||
            (state.WantsProvider && string.Equals(state.ProviderType, "Business", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<RegistrationOption> GetIntentOptions()
    {
        return
        [
            new("RequestServices", "Request Services", "Included: post service requests and compare provider bids.", true),
            new("BuyProducts", "Buy Products", "Included: browse local product listings and delivery orders.", true),
            new("ProvideServices", "Provide Services", "Apply as a provider and bid after approval."),
            new("SellProducts", "Sell Products", "Apply as a seller and manage listings after approval.")
        ];
    }

    private static IReadOnlyList<RegistrationOption> GetProviderTypeOptions() =>
    [
        new("Individual", "Individual"),
        new("Business", "Business / Company")
    ];

    private static IReadOnlyList<RegistrationOption> GetPricingOptions() =>
    [
        new("VisitCharge", "Visit charge"),
        new("StartingPrice", "Starting price"),
        new("Hourly", "Hourly"),
        new("Fixed", "Fixed"),
        new("QuoteRequired", "Quote required")
    ];

    private static IReadOnlyList<RegistrationOption> GetConditionOptions() =>
    [
        new("New", "New products"),
        new("Used", "Used products"),
        new("Both", "Both")
    ];

    private static IReadOnlyList<RegistrationOption> GetBusinessTypeOptions() =>
    [
        new("SoleProprietorship", "Sole proprietorship"),
        new("Partnership", "Partnership"),
        new("LLP", "LLP"),
        new("PrivateLimited", "Private limited"),
        new("Other", "Other")
    ];

    private static IReadOnlyList<RegistrationOption> GetSeatOptions() =>
    [
        new("5", "5"),
        new("10", "10"),
        new("20", "20")
    ];

    private static string GetStageLabel(RegistrationStage stage)
    {
        return stage switch
        {
            RegistrationStage.Intent => "Intent",
            RegistrationStage.Account => "Account",
            RegistrationStage.Contact => "Contact",
            RegistrationStage.Security => "Security",
            RegistrationStage.Professional => "Professional",
            RegistrationStage.Seller => "Seller",
            RegistrationStage.Business => "Business",
            RegistrationStage.Verification => "Verification",
            RegistrationStage.Review => "Review",
            _ => stage.ToString()
        };
    }

    private static string RequireText(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{fieldName} is required.");

        value = value.Trim();
        if (value.Length > maxLength)
            throw new ArgumentException($"{fieldName} cannot exceed {maxLength} characters.");

        return value;
    }

    private static string RequireChoice(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{fieldName} is required.");

        return value.Trim();
    }

    private static string OptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static int CalculateAge(DateTime dateOfBirth)
    {
        var today = DateTime.Today;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age))
            age--;
        return age;
    }

    private static void ClearProvider(RegistrationConversationState state)
    {
        state.ProviderType = "Individual";
        state.ProviderProfession = string.Empty;
        state.ProviderSkills = string.Empty;
        state.ProviderYearsOfExperience = null;
        state.ProviderPrimaryCategory = string.Empty;
        state.ProviderPrimaryCategorySlug = string.Empty;
        state.ProviderServiceAreaCity = "Kolkata";
        state.ProviderServiceAreaState = "West Bengal";
        state.ProviderServiceAreaZone = string.Empty;
        state.ProviderPricingType = "StartingPrice";
        state.ProviderRate = 0;
        state.ProviderAvailability = string.Empty;
        state.ProviderLanguages = string.Empty;
        state.GovernmentIdImage = null;
    }

    private static void ClearSeller(RegistrationConversationState state)
    {
        state.SellerStoreName = string.Empty;
        state.SellerBusinessName = string.Empty;
        state.SellerGstin = string.Empty;
        state.SellerProductCategories = string.Empty;
        state.SellerProductConditionFocus = "Both";
        state.SellerPickupAddress = string.Empty;
        state.SellerCity = "Kolkata";
        state.SellerState = "West Bengal";
        state.SellerDescription = string.Empty;
    }

    private static void ClearBusiness(RegistrationConversationState state)
    {
        state.BusinessLegalName = string.Empty;
        state.BusinessTradingName = string.Empty;
        state.BusinessType = "SoleProprietorship";
        state.BusinessGstin = string.Empty;
        state.BusinessWebsiteOrDomain = string.Empty;
        state.BusinessRegisteredAddress = string.Empty;
        state.BusinessOperatingAddress = string.Empty;
        state.RequestedSeatLimit = 5;
    }

    private static string GetCommercialReviewText(RegistrationConversationState state)
    {
        if (state.WantsProvider && state.WantsSeller)
            return "Provider and seller applications";
        if (state.WantsProvider)
            return "Provider application";
        if (state.WantsSeller)
            return "Seller application";
        return "None";
    }

    private static string MaskPhone(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
            return value;
        return $"******{digits[^4..]}";
    }

    private static string EmptyAsNotProvided(string value) =>
        string.IsNullOrWhiteSpace(value) ? "Not provided" : value;

    private static string JoinNonEmpty(params string[] values)
    {
        var joined = string.Join(", ", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));
        return string.IsNullOrWhiteSpace(joined) ? "Not provided" : joined;
    }
}
