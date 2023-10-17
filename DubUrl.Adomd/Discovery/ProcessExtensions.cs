using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Discovery
{
    internal static class ProcessExtensions
    {
        public static Process GetParent(this Process process)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var query = new ManagementObjectSearcher(
                  "SELECT ParentProcessId " +
                  "FROM Win32_Process " +
                  "WHERE ProcessId=" + process.Id);
                return query
                  .Get()
                  .OfType<ManagementObject>()
                  .Select(p => Process.GetProcessById(
                                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                    ? (int)(uint)p["ParentProcessId"]
                                    : throw new NotSupportedException()))
                  .FirstOrDefault() ?? throw new NullReferenceException();
            }
            else
                throw new NotSupportedException();
        }
    }
}