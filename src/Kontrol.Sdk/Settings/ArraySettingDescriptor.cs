using System.Collections;
using System.Text.RegularExpressions;

namespace Kontrol.Sdk.Settings;

/// <summary>
/// Dynamic tag/list setting descriptor with uniqueness constraints, item count bounds, and item regex validation.
/// </summary>
public sealed record ArraySettingDescriptor : AdapterSettingDescriptor
{
    public IReadOnlyList<string> DefaultValue { get; init; } = Array.Empty<string>();
    public int? MinItems { get; init; }
    public int? MaxItems { get; init; }
    public bool UniqueItems { get; init; } = true;
    public string? ItemPattern { get; init; }
    public string? ItemPatternErrorMessage { get; init; }
    public IReadOnlyList<SettingOption>? AllowedItemValues { get; init; }

    public ArraySettingDescriptor()
    {
        Type = SettingType.Array;
    }

    public override bool Validate(object? value, out string? errorMessage)
    {
        var items = new List<string>();
        if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var el in je.EnumerateArray())
            {
                items.Add(el.GetString() ?? el.ToString());
            }
        }
        else if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var obj in enumerable)
            {
                if (obj != null) items.Add(obj.ToString()!);
            }
        }
        else
        {
            errorMessage = "Value must be a collection/list of items.";
            return false;
        }

        if (MinItems.HasValue && items.Count < MinItems.Value)
        {
            errorMessage = $"Array item count ({items.Count}) is less than minimum required ({MinItems.Value}).";
            return false;
        }

        if (MaxItems.HasValue && items.Count > MaxItems.Value)
        {
            errorMessage = $"Array item count ({items.Count}) exceeds maximum allowed ({MaxItems.Value}).";
            return false;
        }

        if (UniqueItems && items.Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count)
        {
            errorMessage = "Array contains duplicate items, but unique items are required.";
            return false;
        }

        if (AllowedItemValues != null && AllowedItemValues.Count > 0)
        {
            foreach (var item in items)
            {
                if (!AllowedItemValues.Any(opt => string.Equals(opt.Value, item, StringComparison.OrdinalIgnoreCase)))
                {
                    errorMessage = $"Item '{item}' is not among the allowed choices.";
                    return false;
                }
            }
        }

        if (!string.IsNullOrEmpty(ItemPattern))
        {
            foreach (var item in items)
            {
                if (!Regex.IsMatch(item, ItemPattern))
                {
                    errorMessage = ItemPatternErrorMessage ?? $"Item '{item}' does not match pattern '{ItemPattern}'.";
                    return false;
                }
            }
        }

        errorMessage = null;
        return true;
    }

    public override object? Sanitize(object? value)
    {
        if (value is not IEnumerable enumerable || value is string)
            return DefaultValue;

        var items = new List<string>();
        foreach (var obj in enumerable)
        {
            if (obj != null) items.Add(obj.ToString()!);
        }

        return Validate(items, out _) ? items : DefaultValue;
    }
}
