using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters
{
    internal class ReservedKeywordsReader : IDisposable
    {
        protected string ResourcePath { get; }
        protected Assembly ResourceAssembly { get; }

        public ReservedKeywordsReader(string dialectId)
            : this(Assembly.GetExecutingAssembly(), dialectId) { }

        public ReservedKeywordsReader(Assembly resouceAssembly, string dialectId)
            => (ResourceAssembly, ResourcePath) = (resouceAssembly, $"{GetType().Namespace}.ReservedKeywords.{dialectId}.");

        public virtual IEnumerable<string> ReadAll()
        {
            var resources = ResourceAssembly.GetManifestResourceNames().Where(x => x.StartsWith(ResourcePath));

            foreach (var resource in resources)
            {
                using var stream = ResourceAssembly.GetManifestResourceStream(resource)
                                    ?? throw new ArgumentException();
                var keywords = ReadAll(stream);
                foreach (var keyword in keywords)
                    yield return keyword;
            }
        }

        protected internal IEnumerable<string> ReadAll(Stream stream)
        {
            using var reader = new StreamReader(stream);
            while (reader.Peek() >= 0)
            {
                var reservedKeyword = reader.ReadLine();
                if (!string.IsNullOrWhiteSpace(reservedKeyword) && !reservedKeyword.TrimStart().StartsWith("//"))
                    yield return reservedKeyword.Trim();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                DisposeManagedState();
            }

            disposed = true;
        }
        protected virtual void DisposeManagedState() { }
    }
}
