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
    public partial class CalendarDayWindow : Window
    {
        private readonly IcsCalendarService service;
        private readonly DateTime date;
        private readonly HashSet<string> imported;
        private readonly HashSet<string> existingTitles;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private List<CalendarEvent> events = new List<CalendarEvent>();
        private bool closed, busy;
        internal bool ManualEntryRequested { get; private set; }
        internal List<CalendarEvent> SelectedEvents { get; private set; } = new List<CalendarEvent>();
        internal CalendarDayWindow(IcsCalendarService service, DateTime date, IEnumerable<DDayItem> items, string dateFormat)
        {
            InitializeComponent(); this.service = service; this.date = date.Date;
            imported = new HashSet<string>(items.Select(i => i.CalendarEventKey));
            existingTitles = new HashSet<string>(items.Select(i => i.Date.Date.Ticks + "\n" + i.Title));
            DateText.Text = DateDisplayFormat.Format(date, dateFormat);
            Loaded += async delegate { await LoadEvents(); };
        }
        protected override void OnClosed(EventArgs e) { closed = true; lifetime.Cancel(); base.OnClosed(e); }
        private async Task LoadEvents()
        {
            busy = true; ImportButton.IsEnabled = false; StatusText.Text = "일정 제목을 읽는 중…";
            try
            {
                DateTime end = date == DateTime.MaxValue.Date ? DateTime.MaxValue : date.AddDays(1);
                var entries = await service.Events(date, end, lifetime.Token);
                if (closed) return;
                events = entries.Where(e => e.OccursOn(date)).ToList();
                foreach (var item in events) item.AlreadyImported = imported.Contains(item.Key)
                    || existingTitles.Contains(item.Start.Date.Ticks + "\n" + item.Title);
                if (events.Count == 1 && events[0].CanImport) events[0].IsSelected = true;
                EventList.ItemsSource = events;
                StatusText.Text = events.Count == 0 ? "이 날짜에 등록할 일정이 없습니다." : "등록할 제목을 선택하세요.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!closed) StatusText.Text = "일정 파일을 읽지 못했습니다. " + ex.Message; }
            finally { if (!closed) { busy = false; ImportButton.IsEnabled = events.Any(e => e.CanImport); } }
        }
        private void Import_OnClick(object sender, RoutedEventArgs e)
        {
            if (busy) return;
            SelectedEvents = events.Where(item => item.IsSelected && item.CanImport).ToList();
            if (SelectedEvents.Count == 0) { StatusText.Text = "등록할 제목을 하나 이상 선택하세요."; return; }
            DialogResult = true;
        }
        private void Manual_OnClick(object sender, RoutedEventArgs e) { ManualEntryRequested = true; DialogResult = true; }
    }
}
