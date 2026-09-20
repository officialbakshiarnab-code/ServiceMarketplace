using System.Globalization;
using System.Text.RegularExpressions;

namespace ServiceMarketplace.UI.Shared.Auth.Registration;

internal static partial class RegistrationAnswerInterpreter
{
    public static IReadOnlyList<RegistrationCandidateUpdate> Process(
        string? input,
        RegistrationConversationState state)
    {
        var raw = RequireRaw(input);
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var updates = new List<RegistrationCandidateUpdate>();
        var intents = ParseIntent(raw);
        if (intents.Count > 0)
            updates.Add(new(RegistrationQuestionKeys.Intent, null, Values: intents));
        var name = ParseName(raw);
        if (LooksLikeExplicitName(raw) &&
            !ProviderIntentRegex().IsMatch(raw) &&
            !SellerIntentRegex().IsMatch(raw) &&
            !NumberRegex().IsMatch(TrimAtAside(raw)) &&
            !string.IsNullOrWhiteSpace(name.FirstName))
        {
            updates.Add(new(RegistrationQuestionKeys.FirstName, name.FirstName));
            if (!string.IsNullOrWhiteSpace(name.LastName))
                updates.Add(new(RegistrationQuestionKeys.LastName, name.LastName));
        }

        var email = EmailRegex().Match(raw);
        if (email.Success)
            updates.Add(new(RegistrationQuestionKeys.Email, email.Value));

        var phone = PhoneCandidateRegex().Matches(raw)
            .Select(m => m.Value)
            .FirstOrDefault(v => v.Count(char.IsDigit) >= 10);
        if (phone != null)
            updates.Add(new(RegistrationQuestionKeys.PhoneNumber, ParsePhone(phone)));

        var dob = DateRegex().Match(raw);
        if (dob.Success && DateTime.TryParse(dob.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date))
            updates.Add(new(RegistrationQuestionKeys.DateOfBirth, date.ToString("O", CultureInfo.InvariantCulture)));

        var years = YearsRegex().Match(raw);
        if (state.WantsProvider && years.Success)
            updates.Add(new(RegistrationQuestionKeys.ProviderYears, years.Groups[1].Value));

        var money = MoneyRegex().Match(raw);
        if (money.Success)
            updates.Add(new(RegistrationQuestionKeys.ProviderRate, money.Groups[1].Value));

        if (state.WantsProvider && LocationRegex().IsMatch(raw))
            updates.Add(new(RegistrationQuestionKeys.ProviderZone, raw));
        if (state.WantsSeller && SellerLocationRegex().IsMatch(raw))
            updates.Add(new(RegistrationQuestionKeys.SellerPickupAddress, raw));

        var correction = CorrectionLastNameRegex().Match(raw);
        if (correction.Success)
            updates.Add(new(RegistrationQuestionKeys.LastName, correction.Groups[1].Value, RegistrationAnswerOperation.Correct));

        return updates
            .GroupBy(u => u.Field, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();
    }

    public static IReadOnlyList<RegistrationConflict> DetectConflicts(string? input)
    {
        var raw = RequireRaw(input);
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var conflicts = new List<RegistrationConflict>();
        var phones = PhoneCandidateRegex().Matches(raw)
            .Select(m => ParsePhone(m.Value))
            .Where(p => p.Count(char.IsDigit) is >= 10 and <= 15)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (phones.Count > 1 && PhoneKeywordRegex().IsMatch(raw))
            conflicts.Add(new(RegistrationQuestionKeys.PhoneNumber, phones, "More than one phone number was provided."));

        var emails = EmailRegex().Matches(raw).Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (emails.Count > 1)
            conflicts.Add(new(RegistrationQuestionKeys.Email, emails, "More than one email address was provided."));

        var dates = DateValueRegex().Matches(raw).Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (DateKeywordRegex().IsMatch(raw) && dates.Count > 1)
            conflicts.Add(new(RegistrationQuestionKeys.DateOfBirth, dates, "More than one date of birth was provided."));

        var nameParts = WordRegex().Matches(TrimAtAside(RemoveLeadingPhrase(raw)))
            .Select(m => m.Value).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (LooksLikeExplicitName(raw) && nameParts.Count >= 3 && nameParts.Count <= 4)
        {
            conflicts.Add(new(
                RegistrationQuestionKeys.FirstName,
                [$"{nameParts[0]}|{string.Join(" ", nameParts.Skip(1))}", $"{string.Join(" ", nameParts.Take(nameParts.Count - 1))}|{nameParts[^1]}"],
                "More than two name parts were provided. Choose how to map the name."));
        }

        return conflicts;
    }

    public static IReadOnlyList<string> ParseIntent(string? value)
    {
        var raw = RequireRaw(value);
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        if (raw.Equals("provider", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("seller", StringComparison.OrdinalIgnoreCase))
            return [];

        var normalized = raw.ToLowerInvariant();
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var provider = ProviderIntentRegex().IsMatch(raw);
        var seller = SellerIntentRegex().IsMatch(raw);
        var negated = normalized.Contains("nahi", StringComparison.Ordinal) ||
            normalized.Contains("nahin", StringComparison.Ordinal) ||
            normalized.Contains("don't", StringComparison.Ordinal) ||
            normalized.Contains("do not", StringComparison.Ordinal) ||
            normalized.Contains("remove", StringComparison.Ordinal);
        if (provider && !negated)
            values.Add("ProvideServices");
        if (seller && !negated)
            values.Add("SellProducts");
        return values.ToList();
    }

    public static bool MentionsProvider(string? value) => ProviderIntentRegex().IsMatch(value ?? string.Empty);
    public static bool MentionsSeller(string? value) => SellerIntentRegex().IsMatch(value ?? string.Empty);

    public static bool RemovesProvider(string? value) =>
        MentionsProvider(value) && ContainsNegation(value);

    public static bool RemovesSeller(string? value) =>
        MentionsSeller(value) && ContainsNegation(value);

    private static bool ContainsNegation(string? value)
    {
        var normalized = (value ?? string.Empty).ToLowerInvariant();
        return normalized.Contains("nahi", StringComparison.Ordinal) ||
            normalized.Contains("nahin", StringComparison.Ordinal) ||
            normalized.Contains("don't", StringComparison.Ordinal) ||
            normalized.Contains("do not", StringComparison.Ordinal) ||
            normalized.Contains("remove", StringComparison.Ordinal);
    }

    public static ParsedName ParseName(string? value)
    {
        var candidate = TrimAtAside(RemoveLeadingPhrase(RequireRaw(value)));
        var tokens = WordRegex().Matches(candidate)
            .Select(m => CleanNameToken(m.Value))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        if (tokens.Count == 0)
            return new ParsedName(candidate, string.Empty);

        if (tokens.Count == 1)
            return new ParsedName(tokens[0], string.Empty);

        return new ParsedName(tokens[0], string.Join(" ", tokens.Skip(1)));
    }

    public static string ParseLastName(string? value)
    {
        var candidate = TrimAtAside(RemoveLeadingPhrase(RequireRaw(value)));
        candidate = LastNamePrefixRegex().Replace(candidate, string.Empty).Trim();
        var tokens = WordRegex().Matches(candidate)
            .Select(m => CleanNameToken(m.Value))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        if (tokens.Count == 0)
            return candidate;

        return tokens.Count == 1 ? tokens[0] : tokens[^1];
    }

    public static string ParseEmail(string? value)
    {
        var raw = RequireRaw(value);
        var match = EmailRegex().Match(raw);
        return match.Success ? match.Value.Trim() : raw.Trim();
    }

    public static string ParsePhone(string? value)
    {
        var raw = RequireRaw(value);
        var phoneMatch = PhoneCandidateRegex().Matches(raw)
            .Select(m => m.Value)
            .OrderByDescending(v => v.Count(char.IsDigit))
            .FirstOrDefault(v => v.Count(char.IsDigit) >= 10);

        var candidate = phoneMatch ?? raw;
        var digits = new string(candidate.Where(char.IsDigit).ToArray());
        if (digits.Length is < 10 or > 15)
            return candidate.Trim();

        return candidate.TrimStart().StartsWith("+", StringComparison.Ordinal) ? $"+{digits}" : digits;
    }

    public static int? ParseInteger(string? value, int? typedValue = null)
    {
        if (typedValue.HasValue)
            return typedValue;

        var match = NumberRegex().Match(value ?? string.Empty);
        if (!match.Success)
            return null;

        return int.TryParse(match.Value.Replace(",", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    public static decimal? ParseDecimal(string? value, decimal? typedValue = null)
    {
        if (typedValue.HasValue)
            return typedValue;

        var match = DecimalRegex().Match(value ?? string.Empty);
        if (!match.Success)
            return null;

        return decimal.TryParse(match.Value.Replace(",", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    public static DateTime? ParseDate(string? value)
    {
        var match = DateRegex().Match(value ?? string.Empty);
        if (match.Success && DateTime.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
            return parsed;

        return DirectDateRegex().IsMatch(value ?? string.Empty) &&
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed)
            ? parsed
            : null;
    }

    public static ParsedLocation ParseLocation(string? value)
    {
        var raw = RemoveAddressPrefix(RequireRaw(value));

        // GPS labels are not manual addresses. Keep the readable coordinate label
        // bounded and leave city/state for explicit user input or reverse geocoding.
        if (raw.StartsWith("Current location:", StringComparison.OrdinalIgnoreCase))
        {
            var label = raw.Split(" Map:", 2, StringSplitOptions.None)[0];
            return new ParsedLocation(label, string.Empty, string.Empty);
        }

        var parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
            return new ParsedLocation(parts[0], parts[1], string.Join(", ", parts.Skip(2)));

        if (parts.Length == 2)
            return new ParsedLocation(parts[0], parts[1], string.Empty);

        return new ParsedLocation(raw.Trim(), string.Empty, string.Empty);
    }

    private static string RequireRaw(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : WhitespaceRegex().Replace(value.Trim(), " ");

    private static string RemoveLeadingPhrase(string value) =>
        NamePrefixRegex().Replace(value, string.Empty).Trim();

    private static string RemoveAddressPrefix(string value) =>
        AddressPrefixRegex().Replace(value, string.Empty).Trim();

    private static string TrimAtAside(string value)
    {
        var marker = AsideRegex().Match(value);
        if (marker.Success)
            value = value[..marker.Index];

        return value.Trim(' ', '.', ',', ';', ':');
    }

    private static bool LooksLikeExplicitName(string value) => NamePrefixRegex().IsMatch(value);

    private static string CleanNameToken(string value) =>
        value.Trim(' ', '.', ',', ';', ':', '"', '\'');

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"(?i)^\s*(my\s+(full\s+)?name\s+is|full\s+name\s+is|i\s+am|i'm|this\s+is|call\s+me|mera\s+naam|main|मेरा\s+नाम)\s+")]
    private static partial Regex NamePrefixRegex();

    [GeneratedRegex(@"(?i)^\s*(my\s+)?last\s+name\s+(is|:)\s+")]
    private static partial Regex LastNamePrefixRegex();

    [GeneratedRegex(@"(?i)(\.\s+|\s+but\s+|\s+and\s+(?:my|i\s+have|i\s+work|i\s+serve|my\s+phone|my\s+email)\b|\s+i\s+like\s+being\s+called\s+|\s+call\s+me\s+|\s+(?:hai|hoon|है|हूँ)\b)")]
    private static partial Regex AsideRegex();

    [GeneratedRegex(@"[A-Za-z][A-Za-z'.-]*")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\+?[\d][\d\s().-]{8,}\d")]
    private static partial Regex PhoneCandidateRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\d[\d,]*(\.\d+)?")]
    private static partial Regex DecimalRegex();

    [GeneratedRegex(@"(?i)^\s*((my|the)\s+)?(address|pickup|shop|godown|area|zone|location)\s+(is|:|at)\s+")]
    private static partial Regex AddressPrefixRegex();

    [GeneratedRegex(@"(?i)(?:date of birth|born on|dob|birthday|janam\s+tithi|जन्मतिथि)\s*(?:is|:)?\s*(\d{1,2}(?:st|nd|rd|th)?[ ./-][A-Za-z]+[ ./-]\d{4}|\d{1,2}[ ./-]\d{1,2}[ ./-]\d{4})")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"^\s*\d{1,4}[ ./-]\d{1,2}[ ./-]\d{1,4}\s*$")]
    private static partial Regex DirectDateRegex();

    [GeneratedRegex(@"(?i)\b\d{1,2}(?:st|nd|rd|th)?[ ./-][A-Za-z]+[ ./-]\d{4}\b|\b\d{1,2}[ ./-]\d{1,2}[ ./-]\d{4}\b")]
    private static partial Regex DateValueRegex();

    [GeneratedRegex(@"(?i)(date|birthday|born|dob|janam|जन्म)")]
    private static partial Regex DateKeywordRegex();

    [GeneratedRegex(@"(?i)(phone|number|mobile|नंबर|मोबाइल)")]
    private static partial Regex PhoneKeywordRegex();

    [GeneratedRegex(@"(?i)(\d+)\s+(?:years?|saal)\s+(?:(?:of\s+)?experience|hai|se\s+kaam|kaam)")]
    private static partial Regex YearsRegex();

    [GeneratedRegex(@"(?i)(?:charge|rate|price|cost)\D{0,20}(\d[\d,]*(?:\.\d+)?)")]
    private static partial Regex MoneyRegex();

    [GeneratedRegex(@"(?i)(?:work|serve|service|area|location|stay|live|kaam|mein)\D{0,30},")]
    private static partial Regex LocationRegex();

    [GeneratedRegex(@"(?i)(?:pickup|shop|store|godown)\D{0,30},")]
    private static partial Regex SellerLocationRegex();

    [GeneratedRegex(@"(?i)(?:no,?\s+)?(?:my\s+)?(?:last name|surname)\s+is\s+([A-Za-z'.-]+)")]
    private static partial Regex CorrectionLastNameRegex();

    [GeneratedRegex(@"(?i)(provide|provider|service provider|services? dena|service provide|सर्विस|सेवा)")]
    private static partial Regex ProviderIntentRegex();

    [GeneratedRegex(@"(?i)(sell|seller|products?|product bech|product bechna|बेचना|प्रोडक्ट)")]
    private static partial Regex SellerIntentRegex();

    [GeneratedRegex(@"(?i)((don't|do not|dont|nahi|nahin|remove|no).*?(provide|provider|service|सर्विस|सेवा)|(provide|provider|service|सर्विस|सेवा).*?(don't|do not|dont|nahi|nahin|remove|no))")]
    private static partial Regex RemoveProviderIntentRegex();

    [GeneratedRegex(@"(?i)((don't|do not|dont|nahi|nahin|remove|no).*?(sell|seller|product|बेचना|प्रोडक्ट)|(sell|seller|product|बेचना|प्रोडक्ट).*?(don't|do not|dont|nahi|nahin|remove|no))")]
    private static partial Regex RemoveSellerIntentRegex();
}

internal sealed record ParsedName(string FirstName, string LastName);

internal sealed record ParsedLocation(string Primary, string City, string State);
