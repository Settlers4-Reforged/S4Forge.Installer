using ForgeUpdater;
using ForgeUpdater.Manifests;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using WatsonWebserver;
using WatsonWebserver.Core;

namespace ForgeUpdaterTests {
    public class UITests {
        WebserverBase server;

        [SetUp]
        public void Setup() {
            WebserverSettings settings = new WebserverSettings("127.0.0.1", 9000);
            server = new WatsonWebserver.Lite.WebserverLite(settings, async (ctx) => { ctx.Response.StatusCode = 404; });
            server.Routes.PreAuthentication.Static.Add(WatsonWebserver.Core.HttpMethod.GET, "/ux-engine.json", async (ctx) => {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                Manifest uxEngine = new Manifest() {
                    Id = "UXEngine",
                    Name = "UX-Engine",
                    Version = new ManifestVersion("0.10.1"),
                    Type = "Engine",
                    Embedded = true,
                    ClearResidualFiles = false,
                    Assets = new ManifestDownload() {
                        AssetURI = "Fixture/Zips/UX-Engine.0.10.1.zip"
                    }
                };
                await ctx.Response.Send(JsonSerializer.Serialize(uxEngine));
            });
        }

        [TearDown]
        public void TearDown() {
            server.Stop();
            server.Dispose();
        }

        [Test]
        public void FreshSingleStoreTest() {
            // Prepare

            string baseTestPath = Path.Combine(Environment.CurrentDirectory, "Test/UI-Install");
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

            Process? updater = Process.Start(new ProcessStartInfo() {
                FileName = "ForgeUpdateUI.exe",
                Arguments = "--auto-close --store=" + Path.Combine(baseTestPath, "Installation.json"),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            Assert.That(updater, Is.Not.Null);

            updater.WaitForExit();

            Assert.That(updater.HasExited, Is.True);
            Assert.That(updater.ExitCode, Is.EqualTo(0));

            Assert.That(Directory.Exists(installationPath), Is.True);
            Assert.That(File.Exists(Path.Combine(installationPath, "UX-Engine.dll")), Is.True);
        }
    }
}
