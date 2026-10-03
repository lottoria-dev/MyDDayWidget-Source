using System;
using System.Windows.Threading;
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
        internal static IcsCalendarService IcsCalendars { get; private set; }
        private static IcsSubscriptionScheduler subscriptions;
#if GOOGLE_OAUTH
        internal static GoogleCalendarService GoogleCalendar { get; private set; }
#endif

        internal static void Initialize()
        {
            Log = DiagnosticLogFactory.Create();
            Configuration = new ConfigurationService(Log);
            Startup = new StartupService(Log);
            Holidays = new KoreanHolidayService(Configuration.ConfigDirectory);
            SolarTerms = new KoreanHolidayService(Configuration.ConfigDirectory, kind: SpecialDateKind.SolarTerm);
            HolidayKeys = new HolidayKeyStore(Configuration.ConfigDirectory);
            IcsCalendars = new IcsCalendarService(Configuration.ConfigDirectory);
#if GOOGLE_OAUTH
            GoogleCalendar = new GoogleCalendarService(Configuration.ConfigDirectory);
#endif
        }

        internal static void StartCalendarSubscriptions(Dispatcher dispatcher)
        { if (subscriptions == null) subscriptions = new IcsSubscriptionScheduler(IcsCalendars, dispatcher); }

        internal static void Dispose()
        {
            if (subscriptions != null) { subscriptions.Dispose(); subscriptions = null; }
            if (IcsCalendars != null) IcsCalendars.Dispose();
#if GOOGLE_OAUTH
            if (GoogleCalendar != null) GoogleCalendar.Dispose();
#endif
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
