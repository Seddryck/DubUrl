using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying.Dialects.Formatters;

namespace DubUrl.Querying.Dialects.Renderers
{
    internal class AnsiRenderer : IRenderer
    {
        protected NullFormatter Null { get; }
        protected BaseValueFormatter Value { get; }
        protected IIdentifierFormatter Identity { get; }

        protected AnsiRenderer(BaseValueFormatter value, NullFormatter @null, IIdentifierFormatter identity)
            => (Value, Null, Identity) = (value, @null, identity);

        public AnsiRenderer()
            : this(new ValueFormatter(), new NullFormatter(), new QuotedIdentifierFormatter()) { }

        public string Render(object? obj, string format)
        {
            if (string.IsNullOrWhiteSpace(format))
                return obj?.ToString() ?? "<empty>";

            if (format.ToLowerInvariant() == "identity")
                return Identity.Format(obj ?? throw new ArgumentNullException(nameof(obj)));

            if (format.ToLowerInvariant() == "value")
            {
                if (obj is null || obj==DBNull.Value)
                    return Null.Format();
                if (Value.Values.TryGetValue(obj.GetType(), out var formatter))
                    return formatter.Format(obj);
                else
                    throw new ArgumentException(nameof(format));
            }
            else
                throw new ArgumentException($"The format '{format}' is not a supported format. Only 'identifty' and 'value' are supported.");
        }
    }
}
