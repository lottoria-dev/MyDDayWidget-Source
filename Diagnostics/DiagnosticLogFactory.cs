namespace DDay3.Diagnostics
{
    internal static class DiagnosticLogFactory
    {
        internal static IDiagnosticLog Create()
        {
#if TRACE_DIAGNOSTICS
            return new DeveloperTraceLog();
#else
            return NullDiagnosticLog.Instance;
#endif
        }
    }
}
