using ForgeUpdater;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace ForgeUpdateUI.Services {
    public class LoggerService : IUpdaterLogger {
        private BehaviorSubject<string> logs = new BehaviorSubject<string>("");

        const string logFile = "updater.log";

        public LoggerService() {
            UpdaterLogger.Logger = this;

            string applicationPath = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('/', '\\');
            logs.Subscribe((value) => {
                File.WriteAllText($"{applicationPath}/{logFile}", value);
            });
        }

        public IObservable<string> Logs => logs;

        public void LogInfo(string message, params object[] args) {
            string log = logs.Value;
            log += ("[I] " + string.Format(message, args)) + "\n";
            logs.OnNext(log);
        }

        public void LogWarn(string message, params object[] args) {
            string log = logs.Value;
            log += ("[W] " + string.Format(message, args)) + "\n";
            logs.OnNext(log);
        }

        public void LogDebug(string message, params object[] args) {
            string log = logs.Value;
            log += ("[D] " + string.Format(message, args)) + "\n";
            logs.OnNext(log);
        }

        public void LogError(Exception? err, string message, params object[] args) {
            string log = logs.Value;
            log += ("[E] " + string.Format(message, args)) + "\n";
            if (err != null) {
                log += err.ToString() + "\n";
            }
            logs.OnNext(log);
        }
    }
}
