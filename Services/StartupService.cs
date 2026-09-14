using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;
using DDay3.Diagnostics;

namespace DDay3.Services
{
    internal sealed class StartupResult
    {
        internal bool Success { get; set; }
        internal string Message { get; set; }
    }

    internal sealed class StartupService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "MyDDayWidget";
        private readonly IDiagnosticLog log;

        internal StartupService(IDiagnosticLog log)
        {
            this.log = log;
        }

        internal StartupResult Sync(bool enabled)
        {
            return SetEnabled(enabled, false);
        }

        internal StartupResult SetEnabled(bool enabled, bool userInitiated)
        {
            try
            {
                string executable = Assembly.GetEntryAssembly().Location;
                if (!executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return new StartupResult
                    {
                        Success = false,
                        Message = "Visual Studio 호스트에서는 자동 실행을 등록하지 않습니다. 빌드된 EXE에서 확인하세요."
                    };
                }

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey, true))
                {
                    if (key == null) throw new InvalidOperationException("시작프로그램 레지스트리를 열 수 없습니다.");
                    if (enabled) key.SetValue(ValueName, "\"" + executable + "\" --startup", RegistryValueKind.String);
                    else key.DeleteValue(ValueName, false);
                }
                log.Info("startup.change", "enabled=" + enabled + ", userInitiated=" + userInitiated);
                return new StartupResult
                {
                    Success = true,
                    Message = enabled ? "Windows 시작 시 자동 실행됩니다." : "자동 실행을 해제했습니다."
                };
            }
            catch (Exception ex)
            {
                log.Error("startup.change.failed", ex, "enabled=" + enabled);
                return new StartupResult { Success = false, Message = ex.Message };
            }
        }

        internal static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}
