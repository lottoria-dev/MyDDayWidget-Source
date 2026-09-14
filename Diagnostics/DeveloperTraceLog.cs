#if TRACE_DIAGNOSTICS
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace DDay3.Diagnostics
{
    /// <summary>
    /// 개발자 빌드 전용 비동기 회전 로그입니다.
    /// Release 구성에서는 이 파일 자체가 컴파일 대상에서 제외됩니다.
    /// 일정 제목, 날짜, 글꼴 이름 같은 사용자 데이터는 기록하지 않습니다.
    /// </summary>
    internal sealed class DeveloperTraceLog : IDiagnosticLog
    {
        private const long MaxFileBytes = 4L * 1024L * 1024L;
        private const int BackupCount = 3;
        private const int QueueCapacity = 4096;
        private readonly BlockingCollection<string> queue;
        private readonly Thread worker;
        private readonly string logFile;
        private int droppedCount;
        private bool disposed;

        public bool IsEnabled { get { return true; } }
        public string LogDirectory { get; private set; }

        internal DeveloperTraceLog()
        {
            LogDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MyDDayWidget", "Logs");
            Directory.CreateDirectory(LogDirectory);
            logFile = Path.Combine(LogDirectory, "DDay3-developer.log");
            queue = new BlockingCollection<string>(QueueCapacity);
            worker = new Thread(WriteLoop)
            {
                IsBackground = true,
                Name = "DDay3 diagnostic writer"
            };
            worker.Start();
            Info("log.session", "started; privacy=user-content-excluded");
        }

        public void Info(string eventName, string detail = null)
        {
            Enqueue("INFO", eventName, detail, null);
        }

        public void Warn(string eventName, string detail = null)
        {
            Enqueue("WARN", eventName, detail, null);
        }

        public void Error(string eventName, Exception exception = null, string detail = null)
        {
            Enqueue("ERROR", eventName, detail, exception);
        }

        public void Flush()
        {
            DateTime limit = DateTime.UtcNow.AddMilliseconds(1200);
            while (queue.Count > 0 && DateTime.UtcNow < limit)
            {
                Thread.Sleep(20);
            }
        }

        private void Enqueue(string level, string eventName, string detail, Exception exception)
        {
            if (disposed) return;
            string line = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz") +
                " [" + level + "] " + Safe(eventName);
            if (!string.IsNullOrWhiteSpace(detail)) line += " | " + Safe(detail);
            if (exception != null)
            {
                line += " | " + exception.GetType().FullName + ": " + Safe(exception.Message) +
                    Environment.NewLine + Safe(exception.StackTrace);
            }
            if (!queue.TryAdd(line)) Interlocked.Increment(ref droppedCount);
        }

        private void WriteLoop()
        {
            try
            {
                foreach (string line in queue.GetConsumingEnumerable())
                {
                    RotateIfRequired();
                    int dropped = Interlocked.Exchange(ref droppedCount, 0);
                    using (FileStream stream = new FileStream(logFile, FileMode.Append, FileAccess.Write,
                        FileShare.ReadWrite, 16384, FileOptions.SequentialScan))
                    using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        if (dropped > 0)
                        {
                            writer.WriteLine(DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz") +
                                " [WARN] log.queue.drop | count=" + dropped);
                        }
                        writer.WriteLine(line);
                    }
                }
            }
            catch
            {
                // 진단 모듈의 실패가 프로그램 실행을 방해해서는 안 됩니다.
            }
        }

        private void RotateIfRequired()
        {
            FileInfo file = new FileInfo(logFile);
            if (!file.Exists || file.Length < MaxFileBytes) return;
            for (int i = BackupCount; i >= 1; i--)
            {
                string source = i == 1 ? logFile : logFile + "." + (i - 1);
                string destination = logFile + "." + i;
                if (!File.Exists(source)) continue;
                if (File.Exists(destination)) File.Delete(destination);
                File.Move(source, destination);
            }
        }

        private static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        public void Dispose()
        {
            if (disposed) return;
            Info("log.session", "stopping");
            disposed = true;
            queue.CompleteAdding();
            worker.Join(1800);
            queue.Dispose();
        }
    }
}
#endif
