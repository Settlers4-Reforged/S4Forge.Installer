using ForgeUpdater;
using ForgeUpdater.Manifests;

using ForgeUpdateUI;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using WatsonWebserver;
using WatsonWebserver.Core;

namespace ForgeUpdaterTests {
    public class UITests {
        WebserverBase? server = null;
        string baseTestPath = Path.Combine(Environment.CurrentDirectory, "Test", "UI-Install");

        [MemberNotNull(nameof(server))]
        public void SetupServer(string manifestPath, Manifest manifest) {
            WebserverSettings settings = new WebserverSettings("127.0.0.1", 9000);
            server = new WatsonWebserver.Lite.WebserverLite(settings, async (ctx) => { ctx.Response.StatusCode = 404; });
            server.Routes.PreAuthentication.Static.Add(WatsonWebserver.Core.HttpMethod.GET, manifestPath, async (ctx) => {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(JsonSerializer.Serialize(manifest));
            });
        }

        [TearDown]
        public void TearDown() {
            server?.Stop();
            server?.Dispose();

            // Cleanup
            Directory.Delete(baseTestPath, true);
        }

        [Test]
        public async Task FreshSingleStoreTest() {
            // Prepare
            Manifest uxEngineManifest = new Manifest() {
                Id = "UXEngine",
                Name = "UX-Engine",
                Version = new ManifestVersion("0.10.1"),
                Type = "Engine",
                Embedded = true,
                ClearResidualFiles = false,
                Assets = new ManifestDownload() {
                    AssetURI = Path.Join(Environment.CurrentDirectory, "Fixture/Zips/UX-Engine.0.10.1.zip")
                }
            };
            SetupServer("/ux-engine.json", uxEngineManifest);
            string installationPath = Path.Combine(baseTestPath, "Installation");

            try {
                Directory.Delete(baseTestPath, true);
            } catch { }

            Installation installation = new Installation() {
                Name = "Test",
                InstallationPath = installationPath,
                ManifestFeeds = new[] {
                    new Installation.ManifestFeed() {
                        ManifestUri = "http://127.0.0.1:9000/ux-engine.json"
                    }
                },
                InstallIntoFolders = false,
                KeepResidualFiles = true
            };


            Directory.CreateDirectory(installationPath);
            File.WriteAllText(Path.Combine(baseTestPath, "Installation.json"), JsonSerializer.Serialize(installation, new JsonSerializerOptions() {
                WriteIndented = true
            }));

            server.Start();

            Task<int> updater = Program.Main(["--headless", "--auto-close", "--store=" + Path.Combine(baseTestPath, "Installation.json")]);
            int exitCode = await updater;
            Assert.That(exitCode, Is.EqualTo(0));

            Assert.That(Directory.Exists(installationPath), Is.True);
            Assert.That(File.Exists(Path.Combine(installationPath, "UX-Engine.dll")), Is.True);
        }


        [Test]
        public async Task FreshMultiStoreTest() {
            // Prepare
            Manifest uxEngineManifest = new Manifest() {
                Id = "UXEngine",
                Name = "UX-Engine",
                Version = new ManifestVersion("0.10.1"),
                Type = "Engine",
                Embedded = true,
                ClearResidualFiles = false,
                Assets = new ManifestDownload() {
                    AssetURI = Path.Join(Environment.CurrentDirectory, "Fixture/Zips/UX-Engine.0.10.1.zip")
                }
            };
            SetupServer("/ux-engine.json", uxEngineManifest);

            string baseInstallationPath = Path.Combine(baseTestPath, "Installation");

            try {
                Directory.Delete(baseTestPath, true);
            } catch { }

            Installation[] installations = [
                new Installation() {
                    Name = "Modules1",
                    InstallationPath = Path.Join(baseInstallationPath, "Modules1"),
                    ManifestFeeds = [
                        new Installation.ManifestFeed() {
                            ManifestUri = "http://127.0.0.1:9000/ux-engine.json"
                        }
                    ],
                    InstallIntoFolders = false,
                    KeepResidualFiles = true
                },
                new Installation() {
                    Name = "Modules2",
                    InstallationPath = Path.Join(baseInstallationPath, "Modules2"),
                    ManifestFeeds = [
                        new Installation.ManifestFeed() {
                            ManifestUri = "http://127.0.0.1:9000/ux-engine.json"
                        }
                    ],
                    InstallIntoFolders = false,
                    KeepResidualFiles = true
                }
            ];


            Directory.CreateDirectory(baseInstallationPath);
            File.WriteAllText(Path.Combine(baseTestPath, "Installation.json"), JsonSerializer.Serialize(installations, new JsonSerializerOptions() {
                WriteIndented = true
            }));

            server.Start();

            Task<int> updater = Program.Main(["--headless", "--auto-close", "--store=" + Path.Combine(baseTestPath, "Installation.json")]);
            int exitCode = await updater;
            Assert.That(exitCode, Is.EqualTo(0));

            Assert.That(Directory.Exists(baseInstallationPath), Is.True);
            Assert.That(Directory.Exists(Path.Join(baseInstallationPath, "Modules1")), Is.True);
            Assert.That(Directory.Exists(Path.Join(baseInstallationPath, "Modules2")), Is.True);
            Assert.That(File.Exists(Path.Combine(Path.Join(baseInstallationPath, "Modules1"), "UX-Engine.dll")), Is.True);
            Assert.That(File.Exists(Path.Combine(Path.Join(baseInstallationPath, "Modules2"), "UX-Engine.dll")), Is.True);
        }
    }
}
