using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Tokening
{
    public class OptionsMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            foreach (var option in urlInfo.Options)
                Specificator.Execute(option.Key, option.Value);
        }
    }

}
