using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying
{
    internal class FileQueryLogger : IQueryLogger
    {
        public string Path { get; }

        public FileQueryLogger()
            => Path = ".\\queries.txt";

        public FileQueryLogger(string path)
            => Path = path;

        public void Log(string query)
            => File.AppendText($"Path, {query}\r\n");
    }
}
