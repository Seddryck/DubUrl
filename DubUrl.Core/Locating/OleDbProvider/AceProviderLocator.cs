using DubUrl.Mapping.Tokening;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.OleDbProvider
{
    internal class AceProviderLocator : BaseProviderLocator
    {
        private const string REGEX_PATTERN = "^\\bMicrosoft\\b\\.\\bACE\\b\\.\\bOLEDB\\b\\.([0-9]*\\.[0-9]*)$";
        private readonly Dictionary<string, decimal> Candidates = new();

        public AceProviderLocator()
            : base(REGEX_PATTERN) { }

        public AceProviderLocator(string value)
            : base(REGEX_PATTERN, new ExtendedPropertiesMapper(new[] { value })) { }

        internal AceProviderLocator(ProviderLister providerLister)
            : base(REGEX_PATTERN, providerLister) { }

        protected override void AddCandidate(string provider, string[] matches)
        {
            var version = decimal.Parse(matches[0], System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
            Candidates.Add(provider, version);
        }
        protected override List<string> RankCandidates()
            => Candidates.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
    }

    internal class AceXlsProviderLocator : AceProviderLocator
    {
        public AceXlsProviderLocator() : base("Excel 8.0") { }
    }

    internal class AceXlsxProviderLocator : AceProviderLocator
    {
        public AceXlsxProviderLocator() : base("Excel 12.0 Xml") { }
    }

    internal class AceXlsmProviderLocator : AceProviderLocator
    {
        public AceXlsmProviderLocator() : base("Excel 12.0 Macro") { }
    }

    internal class AceXlsbProviderLocator : AceProviderLocator
    {
        public AceXlsbProviderLocator() : base("Excel 12.0") { }
    }
}
