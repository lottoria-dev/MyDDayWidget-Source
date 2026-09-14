using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DDay3.Services
{
    internal sealed class HolidayKeyStore
    {
        private readonly string path;
        internal HolidayKeyStore(string directory) { path = Path.Combine(directory, "holiday-key.bin"); }
        internal string Read()
        {
            try
            {
                if (!File.Exists(path)) return string.Empty;
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser));
            }
            catch (CryptographicException) { return string.Empty; }
            catch (IOException) { return string.Empty; }
            catch (UnauthorizedAccessException) { return string.Empty; }
        }
        internal void Write(string key)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(HolidayData.NormalizeKey(key));
            byte[] protectedBytes;
            try { protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser); }
            finally { Array.Clear(bytes, 0, bytes.Length); }
            string temporary = path + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, protectedBytes);
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal void Delete() { if (File.Exists(path)) File.Delete(path); }
    }
}
