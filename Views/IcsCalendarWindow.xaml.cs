using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using DDay3.Services;

namespace DDay3.Views
{
    public partial class IcsCalendarWindow : Window
    {
        private readonly IcsCalendarService service;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private List<IcsCalendarSnapshot> rows;
        private bool closed, busy;
        internal IcsCalendarWindow(IcsCalendarService service)
        { InitializeComponent(); this.service = service; Reload(); service.Changed += Service_OnChanged; }
        private void Reload(bool preserveChoices = false)
        {
            var updated = service.Calendars;
            if (preserveChoices && rows != null) foreach (var row in updated)
            {
                var old = rows.FirstOrDefault(c => c.Id == row.Id);
                if (old != null) { row.Enabled = old.Enabled; row.RefreshMinutes = old.RefreshMinutes; }
            }
            rows = updated; CalendarList.ItemsSource = rows; StatusText.Text = service.LoadMessage;
        }
        private void Service_OnChanged(object sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => Service_OnChanged(sender, e))); return; }
            if (!closed && !busy) Reload(true);
        }
        protected override void OnClosed(EventArgs e)
        { closed = true; service.Changed -= Service_OnChanged; lifetime.Cancel(); base.OnClosed(e); }
        private async Task ReadFiles(IcsCalendarSnapshot selected = null)
        {
            if (busy || (selected != null && !rows.Any(c => c.Id == selected.Id))) return;
            bool replace = selected != null;
            var dialog = new OpenFileDialog { Title = "ICS 일정 파일 선택", Filter = "일정 파일 (*.ics)|*.ics", Multiselect = !replace };
            if (dialog.ShowDialog(this) != true) return;
            busy = true; Actions.IsEnabled = CalendarList.IsEnabled = false; StatusText.Text = "일정 제목과 날짜를 읽는 중…";
            try
            {
                var imports = await IcsCalendarService.ReadFiles(dialog.FileNames, lifetime.Token);
                if (closed) return;
                string preview = string.Join("\n", imports.Select(c => c.Label));
                if (MessageBox.Show(this, preview + "\n\n가져올까요? 같은 이름의 캘린더는 새 파일로 갱신합니다. 등록한 D-Day는 유지됩니다.",
                    "가져오기 확인", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                service.Import(imports, replace ? selected.Id : null, rows); Reload();
                StatusText.Text = "가져왔습니다. 달력의 날짜를 클릭해 일정 제목을 D-Day로 등록하세요.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!closed) StatusText.Text = "파일을 가져오지 못했습니다. 기존 자료는 유지됩니다.\n" + ex.Message; }
            finally { if (!closed) { busy = false; Actions.IsEnabled = CalendarList.IsEnabled = true; } }
        }
        private async void Import_OnClick(object sender, RoutedEventArgs e) { await ReadFiles(); }
        private void Subscribe_OnClick(object sender, RoutedEventArgs e)
        {
            if (busy) return;
            try
            {
                service.SaveSelection(rows);
                if (new IcsSubscriptionWindow(service) { Owner = this }.ShowDialog() == true)
                { Reload(); StatusText.Text = "URL을 등록했습니다. 달력의 날짜를 눌러 일정을 D-Day로 등록하세요."; }
            }
            catch (Exception) { StatusText.Text = "캘린더 설정을 저장하지 못했습니다. 저장 폴더를 확인하세요."; }
        }
        private async void Refresh_OnClick(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.DataContext as IcsCalendarSnapshot;
            if (busy || row == null) return;
            if (!row.IsSubscription) { await ReadFiles(row); return; }
            busy = true; Actions.IsEnabled = CalendarList.IsEnabled = false; StatusText.Text = "URL에서 일정을 갱신하는 중…";
            try
            {
                service.SaveSelection(rows);
                string error = await service.RefreshSubscription(row.Id, lifetime.Token);
                if (!closed) { Reload(); StatusText.Text = error.Length == 0 ? "갱신했습니다. 등록한 D-Day는 유지됩니다." : error; }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (!closed) StatusText.Text = "갱신하지 못했습니다. 기존 자료는 유지됩니다."; }
            finally { if (!closed) { busy = false; Actions.IsEnabled = CalendarList.IsEnabled = true; } }
        }
        private void Remove_OnClick(object sender, RoutedEventArgs e)
        {
            var selected = (sender as FrameworkElement)?.DataContext as IcsCalendarSnapshot;
            if (busy || selected == null || !rows.Any(c => c.Id == selected.Id)) return;
            if (MessageBox.Show(this, selected.Name + "\n\n이 캘린더를 목록에서 삭제할까요? 등록한 D-Day는 유지됩니다.", "캘린더 삭제",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            try { service.Remove(selected.Id, rows); Reload(); }
            catch (Exception ex) { StatusText.Text = "저장하지 못했습니다.\n" + ex.Message; }
        }
        private void Save_OnClick(object sender, RoutedEventArgs e)
        {
            if (busy) return;
            try { service.SaveSelection(rows); DialogResult = true; }
            catch (Exception ex) { StatusText.Text = "저장하지 못했습니다.\n" + ex.Message; }
        }
    }
}
