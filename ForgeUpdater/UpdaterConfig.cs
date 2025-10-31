using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForgeUpdater {
    public static class UpdaterConfig {
        public static string WorkingDirectory { get; set; } = Environment.CurrentDirectory;
        public static string BaseDownloadPath { get; set; } = $"{WorkingDirectory}/.downloads";

        /// <summary>
        /// Whether or not the default setting for a manifest should be that the updater should clear residual files after an update or not.
        /// E.g. if the update should remove files that are no longer needed (Which are also not ignored in the manifest).
        /// </summary>
        public static bool DefaultUpdateShouldClearResidualFiles { get; set; } = true;
    }
}
