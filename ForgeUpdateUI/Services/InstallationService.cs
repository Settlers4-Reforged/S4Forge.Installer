using Sentry;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForgeUpdateUI.Services {
    public class InstallationService {
        private const string ForcedDirArg = "--s4-install=";

        LoggerService logger;
        public InstallationService(LoggerService loggerService) {
            this.logger = loggerService;
        }

        public (string, Exception?) FindS4Path() {
            string? forcedDir = (from arg in Program.CommandLineArgs
                                 where arg.StartsWith(ForcedDirArg)
                                 select arg).FirstOrDefault();
            if (forcedDir != null) {
                forcedDir = forcedDir.Substring(ForcedDirArg.Length).Replace("\\", "/");

                Exception? ex = null;
                if (!Directory.Exists(forcedDir)) {
                    this.logger.LogError(null, "The forced S4 path '{0}' does not exist", forcedDir);
                    ex = new DirectoryNotFoundException($"The forced S4 path '{forcedDir}' does not exist");
                }

                return (forcedDir, ex);
            }


            const string REG_KEY = "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Ubisoft\\Launcher\\Installs\\11785";
            try {
#pragma warning disable CA1416 // Validate platform compatibility
                string? s4Path = Microsoft.Win32.Registry.GetValue(REG_KEY, "InstallDir", null) as string;
#pragma warning restore CA1416 // Validate platform compatibility
                if (s4Path == null || !Directory.Exists(s4Path)) {
                    Exception? ex = new DirectoryNotFoundException($"Failed to find S4 path in registry at {REG_KEY}");
                    this.logger.LogError(ex, "Failed to find S4 path in registry at {0}", REG_KEY);
                    return (string.Empty, ex);
                }
                return (s4Path, null);
            } catch (Exception ex) {
                this.logger.LogError(ex, "Failed to read S4 path from registry");
                SentrySdk.CaptureException(ex);
                return (string.Empty, ex);
            }
        }
    }
}
