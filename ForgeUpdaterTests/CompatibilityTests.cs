using ForgeUpdater.Manifests;

namespace ForgeUpdaterTests {
    public class CompatibilityTests {
        [Test]
        public void CheckCompatibilityTests() {
            var testSet = new (Compatibility, ManifestVersion, CompatibilityLevel)[] {
                (new Compatibility { Minimum = "1.0.0" }, "1.0.0", CompatibilityLevel.Compatible),
                (new Compatibility { Minimum = "1.0.0" }, "1.1.0", CompatibilityLevel.Compatible),
                (new Compatibility { Minimum = "1.0.0", Verified = "1.0.0"}, "1.0.0", CompatibilityLevel.Verified),
                (new Compatibility { Minimum = "1.0.0", Verified = "1.1.0"}, "1.1.0", CompatibilityLevel.Verified),
                (new Compatibility { Minimum = "1.0.0" }, "0.0.1", CompatibilityLevel.IncompatibleUnder),
                (new Compatibility { Minimum = "1.0.0", Maximum = "1.0.0"}, "1.0.1", CompatibilityLevel.IncompatibleOver),
            };

            foreach (var (compat, version, expected) in testSet) {
                var actual = compat.CheckCompatibility(version);
                Assert.That(actual, Is.EqualTo(expected));
            }
        }
    }
}
