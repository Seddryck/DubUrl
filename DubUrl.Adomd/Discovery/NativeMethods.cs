using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Discovery
{
    public class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        internal delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn,
            IntPtr lParam);

#pragma warning disable CA1838 // Avoid 'StringBuilder' parameters for P/Invokes
        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern unsafe int SendMessageTimeout(
            IntPtr hWnd,
            uint uMsg,
            uint wParam,
            StringBuilder? lParam,
            uint fuFlags,
            uint uTimeout,
            void* lpdwResult);
#pragma warning restore CA1838 // Avoid 'StringBuilder' parameters for P/Invokes

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam,
            StringBuilder lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int GetWindowTextLength(IntPtr hWnd);

#pragma warning disable CA1838 // Avoid 'StringBuilder' parameters for P/Invokes
        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern long GetWindowText(IntPtr hwnd, StringBuilder lpString, long cch);
#pragma warning restore CA1838 // Avoid 'StringBuilder' parameters for P/Invokes
    }
}
