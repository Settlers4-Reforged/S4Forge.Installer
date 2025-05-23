using ForgeUpdater;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace ForgeUpdateUI.Services {
    public class LoggerService : IUpdaterLogger, IDisposable {
        private BehaviorSubject<string> logs = new BehaviorSubject<string>("");

        const string logFile = "updater.log";
        FileStream? logStream;
        StreamWriter? logWriter;

        public LoggerService() {
            UpdaterLogger.Logger = this;

            string applicationPath = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('/', '\\');
            string logPath = $"{applicationPath}/{logFile}";

            logStream = File.Open(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            logWriter = new StreamWriter(logStream) { AutoFlush = true };
            logs.Subscribe((value) => {
                lock (logWriter) {
                    logWriter.Write(value);
                }
            });

            Console.WriteLine("### Log file: {0}", logPath);
        }

        public IObservable<string> Logs => logs;

        public void LogInfo(string message, params object[] args) {
            string log = ("[I] " + string.Format(message, args)) + "\n";
            logs.OnNext(log);
        }

        public void LogWarn(string message, params object[] args) {
            string log = ("[W] " + string.Format(message, args)) + "\n";
            logs.OnNext(log);
        }

        public void LogDebug(string message, params object[] args) {
            string log = ("[D] " + string.Format(message, args)) + "\n";
            logs.OnNext(log);
        }

        public void LogError(Exception? err, string message, params object[] args) {
            string log = ("[E] " + string.Format(message, args)) + "\n";
            if (err != null) {
                log += err.ToString() + "\n";
            }
            logs.OnNext(log);
        }

        public void Dispose() {
            if (logStream != null) {
                logStream.Dispose();
                logStream = null;
            }
            if (logWriter != null) {
                logWriter.Dispose();
                logWriter = null;
            }
            logs?.Dispose();
            logs = null!;
        }
    }
}
