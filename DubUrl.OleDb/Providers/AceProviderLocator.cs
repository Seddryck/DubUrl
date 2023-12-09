using DubUrl.OleDb;
using DubUrl.Locating.Options;
using DubUrl.Locating.RegexUtils;
using DubUrl.Mapping.Database;
using DubUrl.OleDb.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Rewriting.Tokening;

namespace DubUrl.OleDb.Providers;


[Provider<AceProviderRegex, OleDbMapper, MsExcelDatabase>]
public abstract class AceProviderLocator : BaseProviderLocator
{
    internal class AceProviderRegex : BaseProviderRegex
    {
        public AceProviderRegex()
            : base(new BaseRegex[]
            {
                new WordMatch("Microsoft.ACE.OLEDB."),
                new VersionCapture<VersionOption>(),
            })
        { }
    }
    private Dictionary<string, decimal> Candidates { get; } = new();

    public AceProviderLocator()
        : base(GetRegexPattern<AceProviderLocator>()) { }

    internal AceProviderLocator(ProviderLister providerLister)
        : base(GetRegexPattern<AceProviderLocator>(), providerLister) { }

    public AceProviderLocator(string value)
        : base(GetRegexPattern<AceProviderLocator>(), new BaseTokenMapper[] 
            { 
                new ExtendedPropertiesMapper(new[] { value })
                , new OleDbRewriter.DataSourceMapper() 
            }) { }


    protected override void AddCandidate(string provider, string[] matches)
    {
        var version = decimal.Parse(matches[0], System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
        Candidates.Add(provider, version);
    }
    protected override List<string> RankCandidates()
        => Candidates.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();

    
}

[ProviderSpecialization<AceProviderLocator>("xls")]
public class AceXlsProviderLocator : AceProviderLocator
{
    public AceXlsProviderLocator() : base("Excel 8.0") { }
}

[ProviderSpecialization<AceProviderLocator>("xlsx")]
public class AceXlsxProviderLocator : AceProviderLocator
{
    public AceXlsxProviderLocator() : base("Excel 12.0 Xml") { }
}

[ProviderSpecialization<AceProviderLocator>("xlsm")]
public class AceXlsmProviderLocator : AceProviderLocator
{
    public AceXlsmProviderLocator() : base("Excel 12.0 Macro") { }
}

[ProviderSpecialization<AceProviderLocator>("xlsb")]
public class AceXlsbProviderLocator : AceProviderLocator
{
    public AceXlsbProviderLocator() : base("Excel 12.0") { }
}
