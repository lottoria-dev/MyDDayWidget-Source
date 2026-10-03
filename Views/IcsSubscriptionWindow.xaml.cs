using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using DDay3.Services;

namespace DDay3.Views
{
    public partial class IcsSubscriptionWindow : Window
    {
        private readonly IcsCalendarService service;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private bool busy, closed;
        internal IcsSubscriptionWindow(IcsCalendarService service)
        { InitializeComponent(); this.service = service; Loaded += delegate { UrlInput.Focus(); }; }
        protected override void OnClosed(EventArgs e)
        { closed = true; lifetime.Cancel(); UrlInput.Clear(); base.OnClosed(e); }
        private async void Register_OnClick(object sender, RoutedEventArgs e)
        {
            if (busy) return;
            busy = true; RegisterButton.IsEnabled = UrlInput.IsEnabled = NameInput.IsEnabled = IntervalInput.IsEnabled = false;
            StatusText.Text = "캘린더 제목과 날짜를 확인하는 중…";
            try
            {
                int interval = int.Parse((string)((ComboBoxItem)IntervalInput.SelectedItem).Tag);
                await service.Subscribe(UrlInput.Password, NameInput.Text, interval, lifetime.Token);
                if (!closed) DialogResult = true;
            }
            catch (OperationCanceledException) { }
            catch (InvalidDataException ex) { if (!closed) StatusText.Text = ex.Message; }
            catch (Exception) { if (!closed) StatusText.Text = "등록하지 못했습니다. HTTPS iCal 주소·공개 범위·인터넷 연결을 확인하세요. 기존 자료는 유지됩니다."; }
            finally
            {
                if (!closed) { busy = false; RegisterButton.IsEnabled = UrlInput.IsEnabled = NameInput.IsEnabled = IntervalInput.IsEnabled = true; }
            }
        }
    }
}
