using ForgeUpdater;
using ForgeUpdater.Manifests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForgeUpdaterTests {
    public class StoreTests {

        [Test]
        public void UpdateTest() {
            ManifestStore<Manifest> baseStore = new ManifestStore<Manifest>();
            baseStore.AddRange(new[] {
                new Manifest() {
                    Id = "test",
                    Name = "Test",
                    Version = new ManifestVersion("1.0.0"),
                    Type = "test"
                }
            });

            ManifestStore<Manifest> updateStore = new ManifestStore<Manifest>();
            updateStore.AddRange(new[] {
                new Manifest() {
                    Id = "test",
                    Name = "Test",
                    Version = new ManifestVersion("1.1.0"),
                    Type = "test"
                },
                new Manifest() {
                    Id = "test-alternative",
                    Name = "Test Alternative",
                    Version = new ManifestVersion("1.1.0"),
                    Type = "test"
                }
            });

            var output = baseStore.CheckForUpdatesWith(updateStore).ToList();

            Assert.That(output, Has.Count.EqualTo(1));
            Assert.That(output, Has.Exactly(1).Matches<(Manifest input, Manifest update)>((i) =>
                i.input.Id == "test" && i.update.Id == "test" && i.update.Version == new ManifestVersion("1.1.0")));
        }
    }
}
