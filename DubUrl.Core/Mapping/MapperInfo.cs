using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public record struct MapperInfo(
        Type MapperType
        , string DatabaseName
        , string[] Aliases
        , Type DialectType
        , int ListingPriority
        , string ProviderInvariantName
        , Type ParametrizerType
        , string Slug
        , string MainColor
        , string SecondaryColor
    ) { }
}
