using ForgeUpdater.Manifests;
using ForgeUpdater.Updater;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForgeUpdaterTests {
    public class UpdatePipelineTests {

        [Test]
        public void InstallFromLocalTest() {
            const string testPath = "Test/Install";
            const string zipPath = "Fixture/S4Forge.1.0.0.zip";

            // Prepare
            try {
                Directory.Delete(testPath, true);
            } catch { }

            Directory.CreateDirectory(testPath);


            Manifest manifest = new Manifest() {
                Id = "test",
                Name = "S4Forge",
                Version = new ManifestVersion("1.0.0"),
                Type = "test",
                ClearResidualFiles = false,
            };

            // Run
            int progressCount = 0;
            foreach (var progress in UpdatePipeline<Manifest>.InstallFromLocal(manifest, zipPath, testPath)) {
                progressCount++;
            }

            // Assert
            Assert.That(Directory.Exists(testPath), Is.True);
            Assert.That(File.Exists(Path.Combine(testPath, "S4Forge.dll")), Is.True);
            Assert.That(File.Exists(Path.Combine(testPath, "S4Forge.pdb")), Is.True);
            Assert.That(progressCount, Is.EqualTo(2));

            try {
                Directory.Delete(testPath, true);
            } catch { }
        }

        [Test]
        public void InstallFromLocalTestWithResiduals() {
            const string testPath = "Test/Install-Residuals";
            const string zipPath = "Fixture/S4Forge.1.0.0.zip";

            // Prepare
            try {
                Directory.Delete(testPath, true);
            } catch { }

            Directory.CreateDirectory(testPath);
            File.WriteAllText(Path.Combine(testPath, "S4Forge.log"), "Test");
            File.WriteAllText(Path.Combine(testPath, "S4Forge-important.txt"), "Test");

            Manifest manifest = new Manifest() {
                Id = "test",
                Name = "S4Forge",
                Version = new ManifestVersion("1.0.0"),
                Type = "test",
                IgnoredEntries = new[] { "s4forge-important.txt" },
                ClearResidualFiles = true,
            };

            // Run
            foreach (var progress in UpdatePipeline<Manifest>.InstallFromLocal(manifest, zipPath, testPath)) { }

            // Assert
            Assert.That(Directory.Exists(testPath), Is.True);
            Assert.That(File.Exists(Path.Combine(testPath, "S4Forge.dll")), Is.True);
            Assert.That(File.Exists(Path.Combine(testPath, "S4Forge.pdb")), Is.True);

            Assert.That(File.Exists(Path.Combine(testPath, "S4Forge.log")), Is.False);
            Assert.That(File.Exists(Path.Combine(testPath, "S4Forge-important.txt")), Is.True);

            try {
                Directory.Delete(testPath, true);
            } catch { }
        }
    }
}
