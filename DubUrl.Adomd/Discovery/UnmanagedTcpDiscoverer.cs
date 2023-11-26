using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Discovery;

internal class UnmanagedTcpDiscoverer
{
    #region Public Fields

    public const string DllName = "iphlpapi.dll";
    public const int AfInet = 2;

    #endregion

    #region Public Methods

    /// <summary>
    /// <see cref="http://msdn2.microsoft.com/en-us/library/aa365928.aspx"/>
    /// </summary>
    [DllImport(DllName, SetLastError = true)]
    public static extern uint GetExtendedTcpTable(IntPtr tcpTable, ref int tcpTableLength, bool sort, int ipVersion, TcpTableType tcpTableType, int reserved);

    #endregion

    #region Public Enums

    /// <summary>
    /// <see cref="http://msdn2.microsoft.com/en-us/library/aa366386.aspx"/>
    /// </summary>
    public enum TcpTableType
    {
        BasicListener,
        BasicConnections,
        BasicAll,
        OwnerPidListener,
        OwnerPidConnections,
        OwnerPidAll,
        OwnerModuleListener,
        OwnerModuleConnections,
        OwnerModuleAll,
    }

    #endregion

    #region Public Structs

    /// <summary>
    /// <see cref="http://msdn2.microsoft.com/en-us/library/aa366921.aspx"/>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TcpTable
    {
        public uint length;
        public TcpRow row;
    }

    /// <summary>
    /// <see cref="http://msdn2.microsoft.com/en-us/library/aa366913.aspx"/>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TcpRow
    {
        public TcpState state;
        public uint localAddr;
        public byte localPort1;
        public byte localPort2;
        public byte localPort3;
        public byte localPort4;
        public uint remoteAddr;
        public byte remotePort1;
        public byte remotePort2;
        public byte remotePort3;
        public byte remotePort4;
        public int owningPid;
    }

    #endregion
}
