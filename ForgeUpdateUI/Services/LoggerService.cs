using ForgeUpdater;

using Sentry;

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
    public class LoggerService : IUpdaterLogger {
        private BehaviorSubject<string> logs = new BehaviorSubject<string>("[D] Starting Forge Update UI\n");

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
                string logLevel = value.Substring(1, 1);

                switch (logLevel) {
                    case "I":
                        SentrySdk.Logger.LogInfo(value);
                        break;
                    case "W":
                        SentrySdk.Logger.LogWarning(value);
                        break;
                    case "E":
                        SentrySdk.Logger.LogError(value);
                        break;
                    case "D":
                        SentrySdk.Logger.LogDebug(value);
                        break;
                }

                Console.WriteLine(value);
                lock (logWriter) {
                    logWriter.Write(value);
                }
            });

            System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(logWriter));

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
            if (err != null)
                SentrySdk.CaptureException(err);

            logs.OnNext(log);
        }

        public void Dispose() {
            logs.OnCompleted();

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
