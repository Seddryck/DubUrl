using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Tokening
{
    public class ExtendedPropertiesMapper : BaseTokenMapper
    {

        protected internal const string EXTENDED_PROPERTIES_KEYWORD = "Extended Properties";
        protected string[] Values { get; }

        public ExtendedPropertiesMapper(string[] values)
            => Values = values;

        public override void Execute(UrlInfo urlInfo)
        {
            var internalStringBuilder = new StringBuilder();
            //internalStringBuilder.Append('\"');
            foreach (var value in Values)
                internalStringBuilder.Append(value).Append(';');

            foreach (var option in urlInfo.Options)
                internalStringBuilder.Append(option.Key).Append('=').Append(option.Value).Append(';');
            //internalStringBuilder.Append('\"');

            Specificator.Execute(EXTENDED_PROPERTIES_KEYWORD, internalStringBuilder.ToString());
        }
    }
}
