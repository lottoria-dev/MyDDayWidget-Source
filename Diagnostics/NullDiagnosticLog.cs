using System;

namespace DDay3.Diagnostics
{
    internal sealed class NullDiagnosticLog : IDiagnosticLog
    {
        internal static readonly NullDiagnosticLog Instance = new NullDiagnosticLog();
        public bool IsEnabled { get { return false; } }
        public string LogDirectory { get { return string.Empty; } }
        private NullDiagnosticLog() { }
        public void Info(string eventName, string detail = null) { }
        public void Warn(string eventName, string detail = null) { }
        public void Error(string eventName, Exception exception = null, string detail = null) { }
        public void Flush() { }
        public void Dispose() { }
    }
}
