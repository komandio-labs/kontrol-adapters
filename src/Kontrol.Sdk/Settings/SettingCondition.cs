namespace Kontrol.Sdk.Settings;

/// <summary>
/// A declarative conditional rule for dynamic UI visibility and enabling.
/// Evaluates whether a setting should be displayed or active based on another setting's value.
/// </summary>
public sealed record SettingCondition(
    string DependsOnKey,
    object? ExpectedValue = null,
    IReadOnlyList<object>? OneOfValues = null
)
{
    /// <summary>
    /// Evaluates if this condition is met given the current dictionary of setting values.
    /// </summary>
    public bool Evaluate(IReadOnlyDictionary<string, object?> currentValues)
    {
        if (!currentValues.TryGetValue(DependsOnKey, out var actualVal))
            return false;

        if (OneOfValues != null && OneOfValues.Count > 0)
        {
            foreach (var opt in OneOfValues)
            {
                if (ValuesEqual(actualVal, opt)) return true;
            }
            return false;
        }

        return ValuesEqual(actualVal, ExpectedValue);
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;

        if (a.Equals(b)) return true;
        return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
