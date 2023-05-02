using System;
using System.Runtime.InteropServices;

namespace CopyFilesToSharePoint.Common
{
    public class ServiceHelper
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);
        private const int STD_OUTPUT_HANDLE = -11;
        public static bool IsRunningAsService => GetStdHandle(STD_OUTPUT_HANDLE) == IntPtr.Zero;
    }
}
