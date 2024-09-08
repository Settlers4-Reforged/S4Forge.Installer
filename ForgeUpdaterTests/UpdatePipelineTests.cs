using ForgeUpdater;
using ForgeUpdater.Manifests;
using ForgeUpdater.Updater;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForgeUpdaterTests {
    public class UpdatePipelineTests {
        const string downloaderPath = "Test/.downloads";

        [SetUp]
        public void Setup() {
            UpdaterConfig.BaseDownloadPath = downloaderPath;
            UpdaterLogger.Logger = new ConsoleLogger();
        }

        class ConsoleLogger : IUpdaterLogger {
            public void LogDebug(string message, params object[] args) {
                Console.WriteLine($"[DEBUG] {string.Format(message, args)}");
            }

            public void LogError(Exception? err, string message, params object[] args) {
                Console.WriteLine($"[ERROR] {string.Format(message, args)}");
                if (err != null) {
                    Console.WriteLine(err);
                }
            }

            public void LogInfo(string message, params object[] args) {
                Console.WriteLine($"[INFO] {string.Format(message, args)}");
            }

            public void LogWarn(string message, params object[] args) {
                Console.WriteLine($"[WARN] {string.Format(message, args)}");
            }
        }

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
                Id = "S4Forge",
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
            const string testPath = "Test/Install-Residual";
            const string zipPath = "Fixture/S4Forge.1.0.0.zip";

            // Prepare
            try {
                Directory.Delete(testPath, true);
            } catch { }

            Directory.CreateDirectory(testPath);
            File.WriteAllText(Path.Combine(testPath, "S4Forge.log"), "Test");
            File.WriteAllText(Path.Combine(testPath, "S4Forge-important.txt"), "Test");

            Manifest manifest = new Manifest() {
                Id = "S4Forge",
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

        [Test]
        public void UpdateFromLocalTest() {
            const string testPath = "Test/Update";
            const string zipPath = "Fixture/S4Forge.1.0.0.zip";

            // Prepare
            try {
                Directory.Delete(testPath, true);
            } catch { }

            Directory.CreateDirectory(testPath);
            File.WriteAllText(Path.Combine(testPath, "S4Forge.dll"), "Test");
            File.WriteAllText(Path.Combine(testPath, "S4Forge.pdb"), "Test");



            Manifest sourceManifest = new Manifest() {
                Id = "S4Forge",
                Name = "S4Forge",
                Version = new ManifestVersion("0.0.1"),
                Type = "test",
                ClearResidualFiles = false,
            };

            Manifest targetManifest = new Manifest() {
                Id = "S4Forge",
                Name = "S4Forge",
                Version = new ManifestVersion("1.0.0"),
                Type = "test",
                IgnoredEntries = new[] { "S4Forge.pdb" },
                ClearResidualFiles = false,
            };

            // Run
            foreach (var progress in UpdatePipeline<Manifest>.UpdateFromLocal(sourceManifest, targetManifest, zipPath, testPath)) { }

            // Assert
            Assert.That(Directory.Exists(testPath), Is.True);
            Assert.That(File.Exists(Path.Combine(testPath, "S4Forge.dll")), Is.True);
            Assert.That(File.Exists(Path.Combine(testPath, "S4Forge.pdb")), Is.True);

            Assert.That(File.ReadAllText(Path.Combine(testPath, "S4Forge.dll")), Is.Not.EqualTo("Test"));
            Assert.That(File.ReadAllText(Path.Combine(testPath, "S4Forge.pdb")), Is.EqualTo("Test"));

            try {
                Directory.Delete(testPath, true);
            } catch { }
        }


        [Test]
        public async Task InstallFromRemoteTest() {
            const string testPath = "Test/Install-Remote";

            // Prepare
            try {
                Directory.Delete(testPath, true);
            } catch { }
            try {
                Directory.Delete(downloaderPath, true);
            } catch { }

            Directory.CreateDirectory(testPath);

            Manifest manifest = new Manifest() {
                Id = "S4Forge",
                Name = "S4Forge",
                Version = new ManifestVersion("1.0.0"),
                Url = "https://gitlab.settlers4-hd.com/s4-plugins/modapi/s4forgeupdater/-/raw/main/ForgeUpdaterTests/Fixture/",
                Type = "test",
            };

            // Run
            await foreach (var progress in UpdatePipeline<Manifest>.InstallFromRemote(manifest, testPath)) {
                Console.WriteLine(progress);
            }

            // Assert
            Assert.That(Directory.Exists(testPath), Is.True);
            Assert.That(File.Exists(Path.Combine(testPath, "S4Forge.dll")), Is.True);
            Assert.That(File.Exists(Path.Combine(testPath, "S4Forge.pdb")), Is.True);

            try {
                Directory.Delete(testPath, true);
            } catch { }

            try {
                Directory.Delete(downloaderPath, true);
            } catch { }
        }
    }
}
