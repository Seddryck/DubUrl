using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Discovery;

internal class PowerBiDiscoverer : IPowerBiDiscoverer
{
    public PowerBiProcess[] GetPowerBiProcesses(bool includePBIRS = false) 
    {
        var results = new List<PowerBiProcess>();
        var tcps = TcpDiscoverer.GetExtendedTcpDictionary();
        var msmdsrvProcesses = Process.GetProcessesByName("msmdsrv");
        foreach (var proc in msmdsrvProcesses)
        {
            var pbiType = PowerBiType.None;
            var parentTitle = string.Empty;

            var parent = proc.GetParent();

            if (parent != null)
            {
                // exit here if the parent == "services" then this is a SSAS instance
                if (parent.ProcessName.Equals("services", StringComparison.OrdinalIgnoreCase))
                    continue;

                // exit here if the parent == "RSHostingService" then this is a SSAS instance
                if (parent.ProcessName.Equals("RSHostingService", StringComparison.OrdinalIgnoreCase))
                {
                    // only show PBI Report Server if we are running as admin
                    // otherwise we won't have any access to the models
                    if (includePBIRS && IsAdministrator())
                        pbiType = PowerBiType.PowerBIReportServer;
                    else
                        continue;
                }

                // if the process was launched from Visual Studio change the icon
                if (parent.ProcessName.Equals("devenv", StringComparison.OrdinalIgnoreCase))
                    pbiType = PowerBiType.Devenv;

                // get the window title so that we can parse out the file name
                parentTitle = parent.MainWindowTitle;

                if (parentTitle.Length == 0)
                {
                    // for minimized windows we need to use some Win32 api calls to get the title
                    parentTitle = WindowTitle.GetWindowTitle(parent.Id);
                }
            }
            // try and get the tcp port from the Win32 TcpTable API
            //try
            //{
                if (tcps.TryGetValue(proc.Id, out var tcpRow))
                {
                    var port = tcpRow.LocalEndPoint.Port;
                    results.Add(new PowerBiProcess(parentTitle, port, pbiType));
                    //Log.Debug("{class} {method} PowerBI found on port: {port}", nameof(PowerBIHelper), nameof(GetLocalInstances), _port);
                }
                //else
                //Log.Debug("{class} {method} PowerBI port not found for process: {processName} PID: {pid}", nameof(PowerBIHelper), nameof(GetLocalInstances), proc.ProcessName, proc.Id);

            //}
            //catch (Exception ex)
            //{
            //    Log.Error("{class} {Method} {Error} {StackTrace}", nameof(PowerBIHelper), nameof(GetLocalInstances), ex.Message, ex.StackTrace);
            //}
        }
        return results.ToArray();
    }

    protected static bool IsAdministrator()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        { 
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        return false;
    }
}
