using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Discovery
{
    internal class WindowTitle
    {
        const int WM_GETTEXT = 0x000D;
        const int WM_GETTEXTLENGTH = 0x000E;

        private static IEnumerable<IntPtr> EnumerateProcessWindowHandles(int processId)
        {
            var handles = new List<IntPtr>();

            foreach (ProcessThread thread in Process.GetProcessById(processId).Threads)
                NativeMethods.EnumThreadWindows(thread.Id,
                    (hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);

            return handles;
        }


        public static string GetWindowTitle(int procId)
        {
            foreach (var handle in EnumerateProcessWindowHandles(procId))
            {
                if (NativeMethods.IsWindowVisible(handle))
                {
                    //NativeMethods.SendMessage(handle, WM_GETTEXT, message.Capacity, message);
                    //if (message.Length > 0) return message.ToString();
                    var title = GetCaptionOfWindow(handle);
                    if (title.Length > 0) return title;
                }

            }
            return "-";
        }

        public static string GetWindowTitleTimeout(int procId, uint timeout)
        {
            string? title = string.Empty;
            foreach (var handle in EnumerateProcessWindowHandles(procId))
            {
                try
                {
                    // if there is an issue with the window handle we just
                    // ignore it and skip to the next one in the collection
                    title = GetWindowTextTimeout(handle, timeout);
                }
#pragma warning disable CA1031
                catch (Exception)
#pragma warning restore CA1031
                {
                    title = string.Empty;
                }
                if (title?.Length > 0) 
                    return title;
            }
            return title;
        }


        private static unsafe string? GetWindowTextTimeout(IntPtr hWnd, uint timeout)
        {
            int length;
            if (NativeMethods.SendMessageTimeout(hWnd, WM_GETTEXTLENGTH, 0, null, 2, timeout, &length) == 0)
            {
                return null;
            }
            if (length == 0)
            {
                return null;
            }

            var sb = new StringBuilder(length + 1);  // leave room for null-terminator
            if (NativeMethods.SendMessageTimeout(hWnd, WM_GETTEXT, (uint)sb.Capacity, sb, 2, timeout, null) == 0)
            {
                return null;
            }

            return sb.ToString();
        }

        private static string GetCaptionOfWindow(IntPtr hwnd)
        {
            string caption = string.Empty;
            try
            {
                int max_length = NativeMethods.GetWindowTextLength(hwnd);
                var windowText = new StringBuilder(string.Empty, max_length + 5);
                NativeMethods.GetWindowText(hwnd, windowText, max_length + 2);

                if (!string.IsNullOrWhiteSpace(windowText.ToString()))
                    caption = windowText.ToString();
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                caption = ex.Message;
            }
            return caption;
        }
    }
}