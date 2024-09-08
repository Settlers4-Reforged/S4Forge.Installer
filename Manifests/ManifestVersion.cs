using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeUpdater.Manifests;

[DebuggerDisplay("{this.ToString()}")]
[JsonConverter(typeof(ManifestVersionConverter))]
public record ManifestVersion : IComparable<ManifestVersion> {
    // A null value means that the version is a wildcard
    public int? Major { get; init; }
    public int? Minor { get; init; }
    public int? Patch { get; init; }

    public bool HasWildcard => Major == null || Minor == null || Patch == null;

    public ManifestVersion() {
        Major = null;
        Minor = null;
        Patch = null;
    }

    public ManifestVersion(string version) {
        string[] parts = version.Split('.');

        Major = null;
        Minor = null;
        Patch = null;

        try {
            Major = parts[0] == "*" ? null : int.Parse(parts[0]);
            if (parts.Length == 1 || Major == null)
                return;  // No '.' or ending with a wildcard: "*"

            Minor = parts[1] == "*" ? null : int.Parse(parts[1]);
            if (parts.Length == 2 || Minor == null)
                return; // We got only one '.' or ending with a wildcard: "1.*"

            // We got two '.' - could be "1.0.*" or "1.0.0"
            Patch = parts[2] == "*" ? null : int.Parse(parts[2]);

            if (parts.Length > 3)
                throw new ArgumentException($"Invalid version format, found {version}, expected something in Major.Minor.Patch format with optional *");
        } catch (FormatException e) {
            throw new ArgumentException($"Invalid version format, found {version}, expected something in Major.Minor.Patch format with optional *", e);
        }
    }

    public static implicit operator ManifestVersion(string version) => new ManifestVersion(version);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="other"></param>
    /// <returns>
    /// -1 if this version is less than the other version.
    /// 0 if this version is equal to the other version.
    /// 1 if this version is greater than the other version.
    /// </returns>
    public int CompareTo(ManifestVersion? other) {
        if (other == null) {
            return -1;
        }

        if (other.HasWildcard) {
            // Comparison between two wildcard versions does not make sense!
            throw new NotSupportedException("Comparison between two wildcard versions is not supported!");
        }

        (int?, int)[] parts = new[] {
            // other.Part are not null here, because we checked for wildcard versions above!
            (Major, other.Major!.Value),
            (Minor, other.Minor!.Value),
            (Patch, other.Patch!.Value)
        };

        foreach (var (part, otherPart) in parts) {
            if (part == null) {
                // This part of the version is a wildcard, if we didn't differ until here, then we should be "equal"
                return 0;
            }

            if (part != otherPart) {
                return part.Value.CompareTo(otherPart);
            }
        }
        return 0;
    }

    public override string ToString() {
        // All wildcard: *
        if (Major == null) {
            return "*";
        }

        // With Major: 1.
        StringBuilder sb = new StringBuilder();
        sb.Append(Major).Append('.');

        // Wildcard Minor: 1.*
        if (Minor == null) {
            return sb.Append('*').ToString();
        }

        // With Minor: 1.0.
        sb.Append(Minor).Append('.');

        // Wildcard Patch: 1.0.*
        if (Patch == null) {
            return sb.Append('*').ToString();
        }

        // With Patch: 1.0.0
        return sb.Append(Patch).ToString();
    }

    public class ManifestVersionConverter : JsonConverter<ManifestVersion> {
        public override ManifestVersion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            if (reader.TokenType != JsonTokenType.String) {
                throw new JsonException("Expected string token");
            }

            return new ManifestVersion(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, ManifestVersion value, JsonSerializerOptions options) {
            writer.WriteStringValue(value.ToString());
        }
    }
}
