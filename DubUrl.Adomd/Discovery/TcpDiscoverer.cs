using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Discovery
{
    internal class TcpDiscoverer
    {
        public static TcpTable GetExtendedTcpTable(bool sorted)
        {
            var tcpRows = new List<TcpRow>();

            var tcpTable = IntPtr.Zero;
            var tcpTableLength = 0;

            if (UnmanagedTcpDiscoverer.GetExtendedTcpTable(tcpTable, ref tcpTableLength, sorted, UnmanagedTcpDiscoverer.AfInet, UnmanagedTcpDiscoverer.TcpTableType.OwnerPidAll, 0) != 0)
            {
                try
                {
                    tcpTable = Marshal.AllocHGlobal(tcpTableLength);
                    if (UnmanagedTcpDiscoverer.GetExtendedTcpTable(tcpTable, ref tcpTableLength, true, UnmanagedTcpDiscoverer.AfInet, UnmanagedTcpDiscoverer.TcpTableType.OwnerPidAll, 0) == 0)
                    {
                        var table = (UnmanagedTcpDiscoverer.TcpTable)(Marshal.PtrToStructure(tcpTable, typeof(UnmanagedTcpDiscoverer.TcpTable)) ?? throw new NullReferenceException());

                        var rowPtr = (IntPtr)((long)tcpTable + Marshal.SizeOf(table.length));
                        for (int i = 0; i < table.length; ++i)
                        {
                            tcpRows.Add(new TcpRow((UnmanagedTcpDiscoverer.TcpRow)(Marshal.PtrToStructure(rowPtr, typeof(UnmanagedTcpDiscoverer.TcpRow)) ?? throw new NullReferenceException())));
                            rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(typeof(UnmanagedTcpDiscoverer.TcpRow)));
                        }
                    }
                }
                finally
                {
                    if (tcpTable != IntPtr.Zero)
                        Marshal.FreeHGlobal(tcpTable);
                }
            }

            return new TcpTable(tcpRows);
        }

        public static Dictionary<int, TcpRow> GetExtendedTcpDictionary()
        {
            var tcpRows = new Dictionary<int, TcpRow>();

            IntPtr tcpTable = IntPtr.Zero;
            int tcpTableLength = 0;

            if (UnmanagedTcpDiscoverer.GetExtendedTcpTable(tcpTable, ref tcpTableLength, false, UnmanagedTcpDiscoverer.AfInet, UnmanagedTcpDiscoverer.TcpTableType.OwnerPidAll, 0) != 0)
            {
                try
                {
                    tcpTable = Marshal.AllocHGlobal(tcpTableLength);
                    if (UnmanagedTcpDiscoverer.GetExtendedTcpTable(tcpTable, ref tcpTableLength, true, UnmanagedTcpDiscoverer.AfInet, UnmanagedTcpDiscoverer.TcpTableType.OwnerPidAll, 0) == 0)
                    {
                        UnmanagedTcpDiscoverer.TcpTable table = (UnmanagedTcpDiscoverer.TcpTable)(Marshal.PtrToStructure(tcpTable, typeof(UnmanagedTcpDiscoverer.TcpTable)) ?? throw new NullReferenceException());

                        var rowPtr = (IntPtr)((long)tcpTable + Marshal.SizeOf(table.length));
                        for (int i = 0; i < table.length; ++i)
                        {
                            TcpRow row = new TcpRow((UnmanagedTcpDiscoverer.TcpRow)(Marshal.PtrToStructure(rowPtr, typeof(UnmanagedTcpDiscoverer.TcpRow)) ?? throw new NullReferenceException()));
                            // HACK: only add first row that is in a Listening state
                            if (row.State == TcpState.Listen)
                            {
                                if (!tcpRows.ContainsKey(row.ProcessId))
                                    tcpRows.Add(row.ProcessId, row);
                            }
                            rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(typeof(UnmanagedTcpDiscoverer.TcpRow)));
                        }
                    }
                }
                finally
                {
                    if (tcpTable != IntPtr.Zero)
                        Marshal.FreeHGlobal(tcpTable);
                }
            }

            return tcpRows;
        }
    }
}
