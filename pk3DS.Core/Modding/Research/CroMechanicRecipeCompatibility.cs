namespace pk3DS.Core.Modding.Research;

/// <summary>
/// Result of validating one external recipe document against a recipe-engine version.
/// </summary>
public sealed record CroMechanicRecipeCompatibilityReport(
    CroRecipeVersion EngineVersion,
    CroRecipeVersion? MinimumEngineVersion);

/// <summary>
/// Semantic compatibility checks for external CRO mechanic recipe documents.
/// Parsing remains format-only; callers can validate engine compatibility separately.
/// </summary>
public static class CroMechanicRecipeCompatibility
{
    /// <summary>
    /// Version of the CRO recipe engine capabilities implemented by this build.
    /// Increment this only when recipe execution capabilities change.
    /// </summary>
    public static CroRecipeVersion CurrentEngineVersion { get; } =
        new(
            Major: 1,
            Minor: 0,
            Patch: 0);

    /// <summary>
    /// Validates a recipe document against the current recipe-engine version.
    /// </summary>
    public static bool TryCheck(
        CroMechanicRecipeDocument document,
        out CroMechanicRecipeCompatibilityReport report,
        out string error) =>
        TryCheck(
            document,
            CurrentEngineVersion,
            out report,
            out error);

    /// <summary>
    /// Validates a recipe document against an explicit recipe-engine version.
    /// The explicit overload keeps compatibility semantics deterministic and testable.
    /// </summary>
    public static bool TryCheck(
        CroMechanicRecipeDocument document,
        CroRecipeVersion engineVersion,
        out CroMechanicRecipeCompatibilityReport report,
        out string error)
    {
        report =
            null;

        error =
            string.Empty;

        if (document is null)
        {
            error =
                "recipe document is null.";
            return false;
        }

        CroRecipeVersion? minimumEngineVersion =
            document.MinimumEngineVersion;

        if (minimumEngineVersion.HasValue &&
            engineVersion.CompareTo(
                minimumEngineVersion.Value) < 0)
        {
            string identity =
                string.IsNullOrEmpty(
                    document.RecipeId)
                    ? document.Name ?? string.Empty
                    : document.RecipeId;

            error =
                $"recipe '{identity}' requires recipe engine {minimumEngineVersion.Value} or newer; current engine is {engineVersion}.";
            return false;
        }

        report =
            new CroMechanicRecipeCompatibilityReport(
                EngineVersion: engineVersion,
                MinimumEngineVersion: minimumEngineVersion);

        return true;
    }
}