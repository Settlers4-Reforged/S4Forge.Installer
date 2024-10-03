using ForgeUpdater.Manifests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ForgeUpdaterTests {
    public class ManifestTests {

        [Test]
        public void ParseFromJsonTest() {

        }

        [Test]
        public void SerializeToJsonTest() {
            Manifest manifest = new Manifest() {
                Id = "S4Forge",
                Name = "S4Forge",
                Version = new("1.0.0"),
                Type = "test",
                ClearResidualFiles = false,
                Assets = new() {
                    AssetURI = "https://example.com/assets/X.1.0.0.zip/",
                    DeltaPatchesURI = new() {
                        {"0.1.0", ("S4Forge.0.1.0.zip", "https://example.com/assets/X.1.0.0.to.0.1.0.delta/")}
                    }
                }
            };

            string json = JsonSerializer.Serialize(manifest);
            Manifest? deserialized = JsonSerializer.Deserialize<Manifest>(json);

            Assert.That(deserialized, Is.EqualTo(manifest));
        }
    }
}
