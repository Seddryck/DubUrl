using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Discovery
{
    internal class TcpTable : IEnumerable<TcpRow>
    {
        public IEnumerable<TcpRow> TcpRows { get; }

        public TcpTable(IEnumerable<TcpRow> tcpRows)
            => TcpRows = tcpRows;

        public IEnumerator<TcpRow> GetEnumerator()
            => TcpRows.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => TcpRows.GetEnumerator();
    }
}
