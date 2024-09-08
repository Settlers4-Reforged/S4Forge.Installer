namespace ForgeUpdater.Manifests;

public enum CompatibilityLevel {
    Unknown,
    Missing,
    /// <summary>
    /// Relationship has a version under the specified minimum.
    /// </summary>
    IncompatibleUnder,
    /// <summary>
    /// Relationship has a version over the specified maximum.
    /// </summary>
    IncompatibleOver,
    /// <summary>
    /// Anything >= Compatible is compatible. (e.g. Verified)
    /// </summary>
    Compatible,
    Verified,
}
