using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DDay3.Models;
using DDay3.Services;

namespace DDay3.Views
{
    public partial class GoogleDayWindow : Window
    {
        private readonly GoogleCalendarService service;
        private readonly DateTime date;
        private readonly HashSet<string> imported;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private List<GoogleCalendarEvent> events = new List<GoogleCalendarEvent>();
        private bool closed, busy;
        internal bool ManualEntryRequested { get; private set; }
        internal List<GoogleCalendarEvent> SelectedEvents { get; private set; } = new List<GoogleCalendarEvent>();
        internal GoogleDayWindow(GoogleCalendarService service, DateTime date, IEnumerable<DDayItem> items)
        {
            InitializeComponent(); this.service = service; this.date = date.Date;
            imported = new HashSet<string>(items.Select(i => i.CalendarEventKey));
            DateText.Text = date.ToString("yyyy-MM-dd (ddd)");
            Loaded += async delegate { await LoadEvents(false); };
        }
        protected override void OnClosed(EventArgs e) { closed = true; lifetime.Cancel(); base.OnClosed(e); }
        private async Task LoadEvents(bool force)
        {
            busy = true; ImportButton.IsEnabled = RefreshButton.IsEnabled = false;
            StatusText.Text = "선택한 캘린더에서 일정을 읽는 중…";
            try
            {
                DateTime end = date == DateTime.MaxValue.Date ? date : date.AddDays(1);
                var result = await service.Events(date, end, force, lifetime.Token);
                if (closed) return;
                events = result.Events.Where(e => e.OccursOn(date)).ToList();
                foreach (var item in events) item.AlreadyImported = imported.Contains(item.Key);
                if (events.Count == 1 && events[0].CanImport) events[0].IsSelected = true;
                EventList.ItemsSource = events;
                StatusText.Text = (events.Count == 0 && result.Warnings.Count == 0 && service.Enabled ? "이 날짜에 조회된 일정이 없습니다.\n" : "") + result.Status;
            }
            catch (Exception ex)
            {
                if (closed) return;
                events.Clear(); EventList.ItemsSource = null;
                StatusText.Text = GoogleCalendarService.ErrorMessage(ex);
            }
            finally
            {
                if (!closed) { busy = false; ImportButton.IsEnabled = events.Any(e => e.CanImport); RefreshButton.IsEnabled = true; }
            }
        }
        private async void Refresh_OnClick(object sender, RoutedEventArgs e) { if (!busy) await LoadEvents(true); }
        private void Import_OnClick(object sender, RoutedEventArgs e)
        {
            if (busy) return;
            SelectedEvents = events.Where(item => item.IsSelected && item.CanImport).ToList();
            if (SelectedEvents.Count == 0) { StatusText.Text = "등록할 일정을 하나 이상 선택하세요."; return; }
            DialogResult = true;
        }
        private void Manual_OnClick(object sender, RoutedEventArgs e) { ManualEntryRequested = true; DialogResult = true; }
        private void Browser_OnClick(object sender, RoutedEventArgs e)
        { StartupService.OpenUrl("https://calendar.google.com/calendar/u/0/r/day/" + date.Year + "/" + date.Month + "/" + date.Day); }
    }
}
