using ForgeUpdater;

namespace ForgeUpdaterTests {
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

}
