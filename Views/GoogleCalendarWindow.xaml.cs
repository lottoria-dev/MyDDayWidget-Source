using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DDay3.Models;
using DDay3.Services;
using Microsoft.Win32;

namespace DDay3.Views
{
    public partial class GoogleCalendarWindow : Window
    {
        private readonly GoogleCalendarService service;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private List<GoogleCalendarEntry> calendars;
        private bool closed, busy;
        internal GoogleCalendarWindow(GoogleCalendarService service)
        {
            InitializeComponent(); this.service = service;
            if (!GoogleOAuthClient.IsConfigured)
                LoginHelpText.Text = "소스 빌드에 공식 OAuth 정보가 포함되지 않았습니다. 공식 배포본을 사용하거나 아래 고급 연결에서 개인 OAuth JSON을 선택하세요. 기존 계정 연결은 계속 사용할 수 있습니다.";
            StatusText.Text = service.LoadMessage.Length > 0 ? service.LoadMessage : service.Connected
                ? "연결됨 · 저장된 캘린더 " + service.SelectedCount + "개" : "Google 계정이 연결되지 않았습니다.";
            SetBusy(false);
            Loaded += async delegate { if (service.Connected) await RefreshCalendars(); };
        }
        protected override void OnClosed(EventArgs e)
        { closed = true; lifetime.Cancel(); base.OnClosed(e); }
        private void SetBusy(bool value)
        {
            busy = value;
            ConnectButton.IsEnabled = !value && GoogleOAuthClient.IsConfigured;
            AdvancedConnectButton.IsEnabled = !value;
            RefreshButton.IsEnabled = !value && service.Connected;
            DisconnectButton.IsEnabled = !value && service.Connected;
            SaveButton.IsEnabled = !value && service.Connected && calendars != null;
            CalendarList.IsEnabled = !value;
        }
        private async void Connect_OnClick(object sender, RoutedEventArgs e)
        { if (!busy) await StartConnection(() => service.ConnectOfficial(lifetime.Token)); }
        private async void AdvancedConnect_OnClick(object sender, RoutedEventArgs e)
        {
            if (busy) return;
            var picker = new OpenFileDialog { Filter = "Google OAuth 클라이언트 (*.json)|*.json", Title = "데스크톱 앱용 OAuth JSON 선택" };
            if (picker.ShowDialog(this) != true) return;
            await StartConnection(() => service.Connect(picker.FileName, lifetime.Token));
        }
        private async Task StartConnection(Func<Task> connect)
        {
            SetBusy(true); StatusText.Text = "브라우저에서 로그인하고 읽기 권한을 허용하세요. 최대 3분간 기다립니다. 창을 닫으면 취소됩니다.";
            try
            {
                await connect();
                if (closed) return;
                calendars = null; CalendarList.ItemsSource = null;
                await RefreshCalendars();
            }
            catch (Exception ex) { if (!closed) StatusText.Text = GoogleCalendarService.ErrorMessage(ex); }
            finally { if (!closed) SetBusy(false); }
        }
        private async void Refresh_OnClick(object sender, RoutedEventArgs e) { if (!busy) await RefreshCalendars(); }
        private async Task RefreshCalendars()
        {
            SetBusy(true); StatusText.Text = "캘린더 목록을 읽는 중…";
            try
            {
                var result = await service.ListCalendars(lifetime.Token);
                if (closed) return;
                // Preserve checkbox edits across an explicit refresh in the same dialog.
                if (calendars != null)
                    foreach (var entry in result) { var previous = calendars.FirstOrDefault(c => c.Id == entry.Id); if (previous != null) entry.IsSelected = previous.IsSelected; }
                calendars = result; CalendarList.ItemsSource = calendars;
                StatusText.Text = calendars.Count == 0 ? "읽을 수 있는 캘린더가 없습니다. Google 웹에서 구독·공유 캘린더를 먼저 추가하세요."
                    : "사용할 캘린더를 선택한 뒤 ‘선택 저장’을 누르세요. 비공개 일정은 공유 권한에 따라 제목을 볼 수 없습니다.";
            }
            catch (Exception ex) { if (!closed) StatusText.Text = GoogleCalendarService.ErrorMessage(ex); }
            finally { if (!closed) SetBusy(false); }
        }
        private void Save_OnClick(object sender, RoutedEventArgs e)
        {
            if (busy || calendars == null) return;
            try { service.SelectCalendars(calendars); DialogResult = true; }
            catch (Exception ex) { StatusText.Text = GoogleCalendarService.ErrorMessage(ex); }
        }
        private async void Disconnect_OnClick(object sender, RoutedEventArgs e)
        {
            if (busy) return;
            SetBusy(true); StatusText.Text = "연결 정보를 지우는 중…";
            try
            {
                bool revoked = await service.Disconnect(lifetime.Token);
                if (closed) return;
                calendars = null; CalendarList.ItemsSource = null;
                StatusText.Text = revoked
                    ? "이 PC의 연결 정보와 조회 자료를 지우고 Google 권한을 철회했습니다. 이미 등록한 D-Day는 유지됩니다."
                    : "이 PC의 연결 정보와 조회 자료를 지웠습니다. Google 권한 철회를 완료하지 못했습니다. ‘Google 연결 권한 관리’에서 직접 해제하세요. 이미 등록한 D-Day는 유지됩니다.";
            }
            catch (Exception ex) { if (!closed) StatusText.Text = GoogleCalendarService.ErrorMessage(ex); }
            finally { if (!closed) SetBusy(false); }
        }
        private void Permissions_OnClick(object sender, RoutedEventArgs e)
        { StartupService.OpenUrl("https://myaccount.google.com/connections"); }
        private void Privacy_OnClick(object sender, RoutedEventArgs e)
        { StartupService.OpenUrl("https://mathtime.kr/dday-privacy.html"); }
    }
}
