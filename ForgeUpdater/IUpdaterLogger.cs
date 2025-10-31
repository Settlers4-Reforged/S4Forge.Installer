using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForgeUpdater {
    public interface IUpdaterLogger {
        void LogInfo(string message, params object[] args);
        void LogWarn(string message, params object[] args);
        void LogDebug(string message, params object[] args);
        void LogError(Exception? err, string message, params object[] args);
    }

    public static class UpdaterLogger {
        public static IUpdaterLogger? Logger { get; set; }

        public static void LogInfo(string message, params object[] args) {
            Logger?.LogInfo(message, args);
        }

        public static void LogWarn(string message, params object[] args) {
            Logger?.LogWarn(message, args);
        }

        public static void LogDebug(string message, params object[] args) {
            Logger?.LogDebug(message, args);
        }

        public static void LogError(Exception? err, string message, params object[] args) {
            Logger?.LogError(err, message, args);
        }
    }
}
