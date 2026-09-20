namespace ServiceMarketplace.UI.Shared.Auth.Registration;

public static class ProfessionQuestionCatalog
{
    public static string GetSkillPrompt(string? categorySlug, string? categoryName)
    {
        var normalized = Normalize(categorySlug, categoryName);
        if (ContainsAny(normalized, "beauty", "makeup", "hair", "mehendi"))
            return "Which beauty services do you offer?";
        if (ContainsAny(normalized, "electric", "plumb", "repair", "appliance", "carpenter", "paint"))
            return "Which home service tasks can you handle?";
        if (ContainsAny(normalized, "education", "tutor", "music", "language"))
            return "Which subjects, classes, or learning services do you offer?";
        if (ContainsAny(normalized, "photo", "video"))
            return "Which photography or media services do you offer?";
        if (ContainsAny(normalized, "driver", "transport"))
            return "Which driving or transport services do you offer?";

        return "What skills or services do you offer?";
    }

    public static string GetRatePrompt(string? categorySlug, string? categoryName)
    {
        var normalized = Normalize(categorySlug, categoryName);
        if (ContainsAny(normalized, "beauty", "makeup", "photo", "video"))
            return "What starting package amount should admins review?";
        if (ContainsAny(normalized, "electric", "plumb", "repair", "appliance", "carpenter", "paint"))
            return "What visit charge or starting amount should admins review?";
        if (ContainsAny(normalized, "education", "tutor", "music", "language"))
            return "What hourly or session amount should admins review?";

        return "What rate or starting amount should admins review?";
    }

    private static string Normalize(string? categorySlug, string? categoryName)
    {
        return $"{categorySlug} {categoryName}".Trim().ToLowerInvariant();
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
