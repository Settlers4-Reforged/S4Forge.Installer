using ForgeUpdater.Manifests;

namespace ForgeUpdaterTests {
    public class VersionTests {
        [Test]
        public void CorrectTests() {
            var testPairs = new[] {
                ("1", new ManifestVersion() { Major = 1, Minor = null, Patch = null }),
                ("1.*", new ManifestVersion() { Major = 1, Minor = null, Patch = null }),
                ("1.*.*", new ManifestVersion() { Major = 1, Minor = null, Patch = null }),
                ("1.2", new ManifestVersion() { Major = 1, Minor = 2, Patch = null }),
                ("1.02", new ManifestVersion() { Major = 1, Minor = 2, Patch = null }),
                ("1.020", new ManifestVersion() { Major = 1, Minor = 20, Patch = null }),
                ("1.2.*", new ManifestVersion() { Major = 1, Minor = 2, Patch = null }),
                ("1.2.3", new ManifestVersion() { Major = 1, Minor = 2, Patch = 3 }),
            };

            foreach (var (input, expected) in testPairs) {
                var actual = new ManifestVersion(input);
                Assert.That(actual, Is.EqualTo(expected));
            }
        }

        [Test]
        public void IncorrectTests() {
            var testPairs = new[] {
                "",
                "1.0.0.0",
                "test",
                "1.x",
                "1.0.",
                "1.1-beta"
            };

            foreach (var input in testPairs) {
                Assert.That(() => new ManifestVersion(input), Throws.ArgumentException, input);
            }
        }
    }
}