using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    internal class InlineQuery : IQuery
    {
        private string Text { get; }

        public InlineQuery(string text) => Text = text;

        public string Read(IDialect dialect) => Text;
        public bool Exists(IDialect dialect, bool includeDefault = false) => true;
    }
}
