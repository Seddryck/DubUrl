using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Discovery
{
    internal class PowerBiProcess
    {
        public static readonly string[] PBIDesktopMainWindowTitleSuffixes = new string[]
        {
            // Different characters are used as a separator in the PBIDesktop window title depending on the current UI culture/localization
            // See https://github.com/sql-bi/Bravo/issues/476

            " \u002D Power BI Desktop", // Dash Punctuation - minus hyphen
            " \u2212 Power BI Desktop", // Math Symbol - minus sign
            " \u2011 Power BI Desktop", // Dash Punctuation - non-breaking hyphen
            " \u2013 Power BI Desktop", // Dash Punctuation - en dash
            " \u2014 Power BI Desktop", // Dash Punctuation - em dash
            " \u2015 Power BI Desktop", // Dash Punctuation - horizontal bar
        };

        public int Port { get; private set; }
        public string Name { get; private set; }
        public PowerBiType PowerBiType { get; private set; }

        public PowerBiProcess(string windowTitle, int port, PowerBiType pbiType)
        {
            Port = port;
            PowerBiType = pbiType;
            //try
            //{
                // Strip "Power BI Designer" or "Power BI Desktop" off the end of the string
                foreach (var suffix in PBIDesktopMainWindowTitleSuffixes)
                {
                    var index = windowTitle.LastIndexOf(suffix);
                    if (index >= 1)
                    {
                        Name = windowTitle.Substring(0, index).Trim();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(Name))
                {
                    //if (port != -1)
                    //{
                    //    Log.Warning(Constants.LogMessageTemplate, nameof(PowerBIInstance), "ctor", $"Unable to find ' - Power BI Desktop' in Power BI title '{windowTitle}'");
                    //}
                    Name = windowTitle;
                }
            //}
            //catch (Exception ex)
            //{
            //    Log.Error(ex, Constants.LogMessageTemplate, nameof(PowerBIInstance), "ctor", ex.Message);
            //    Name = windowTitle;
            //}
        }
        
    }
}