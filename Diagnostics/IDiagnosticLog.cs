using System;

namespace DDay3.Diagnostics
{
    internal interface IDiagnosticLog : IDisposable
    {
        bool IsEnabled { get; }
        string LogDirectory { get; }
        void Info(string eventName, string detail = null);
        void Warn(string eventName, string detail = null);
        void Error(string eventName, Exception exception = null, string detail = null);
        void Flush();
    }
}
