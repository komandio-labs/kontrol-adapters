namespace Kontrol.Sdk.Settings;

/// <summary>
/// Dictates how the Kontrol host UI arranges the setting card in the responsive grid.
/// </summary>
public enum LayoutSpan
{
    /// <summary>Occupies half the width of the category container (paired 2-per-row).</summary>
    Half = 0,

    /// <summary>Occupies the entire width of the category container (100% full-width row).</summary>
    Full = 1
}
