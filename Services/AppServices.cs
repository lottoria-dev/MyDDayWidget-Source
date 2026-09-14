using System;
using DDay3.Diagnostics;

namespace DDay3.Services
{
    internal static class AppServices
    {
        internal static IDiagnosticLog Log { get; private set; }
        internal static ConfigurationService Configuration { get; private set; }
        internal static StartupService Startup { get; private set; }
        internal static KoreanHolidayService Holidays { get; private set; }
        internal static KoreanHolidayService SolarTerms { get; private set; }
        internal static HolidayKeyStore HolidayKeys { get; private set; }

        internal static void Initialize()
        {
            Log = DiagnosticLogFactory.Create();
            Configuration = new ConfigurationService(Log);
            Startup = new StartupService(Log);
            Holidays = new KoreanHolidayService(Configuration.ConfigDirectory);
            SolarTerms = new KoreanHolidayService(Configuration.ConfigDirectory, kind: SpecialDateKind.SolarTerm);
            HolidayKeys = new HolidayKeyStore(Configuration.ConfigDirectory);
        }

        internal static void Dispose()
        {
            if (Holidays != null) Holidays.Dispose();
            if (SolarTerms != null) SolarTerms.Dispose();
            if (Log != null)
            {
                Log.Dispose();
                Log = null;
            }
        }
    }
}
