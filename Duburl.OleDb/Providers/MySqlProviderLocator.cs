﻿using DubUrl.OleDb;
using DubUrl.Locating.RegexUtils;
using DubUrl.Mapping.Database;
using DubUrl.Mapping.Tokening;
using DubUrl.OleDb.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DubUrl.Mapping;

namespace DubUrl.OleDb.Providers
{
    [Provider<MySqlProviderRegex, OleDbMapper, MySqlDatabase>()]
    internal class MySqlProviderLocator : BaseProviderLocator
    {
        internal class MySqlProviderRegex : BaseProviderRegex
        {
            public MySqlProviderRegex()
                : base(new BaseRegex[]
                {
                    new WordMatch("MySQL Provider"),
                })
            { }
        }
        private readonly List<string> Candidates = new();

        public MySqlProviderLocator()
            : base(GetRegexPattern<MySqlProviderLocator>(), new BaseTokenMapper[]
                { new BaseMapper.OptionsMapper()
                    , new OleDbMapper.InitialCatalogMapper()
                    , new OleDbMapper.ServerMapper()
                }
            )
        { }

        internal MySqlProviderLocator(ProviderLister providerLister)
            : base(GetRegexPattern<MySqlProviderLocator>(), providerLister) { }

        internal MySqlProviderLocator(string value)
            : base(GetRegexPattern<MySqlProviderLocator>(), new BaseTokenMapper[]
                { new BaseMapper.OptionsMapper()
                    , new OleDbMapper.InitialCatalogMapper()
                    , new OleDbMapper.ServerMapper()
                }
            )
        { }

        protected override void AddCandidate(string provider, string[] matches)
            => Candidates.Add(provider);
        protected override List<string> RankCandidates()
            => Candidates;
    }
}