using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public class BrandAttribute : Attribute
    {
        public const string DefaultMainColor = "#333333";
        public const string DefaultSecondaryColor = "#ffffff";

        public virtual string Slug { get; protected set; } = string.Empty;
        public virtual string MainColor { get; protected set; } = DefaultMainColor;
        public virtual string SecondaryColor { get; protected set; } = DefaultSecondaryColor;

        public BrandAttribute(string slug, string color)
            => (Slug , MainColor) = (slug, color);
        
        public BrandAttribute(string slug, string mainColor, string secondaryColor)
            => (Slug, MainColor, SecondaryColor) = (slug, mainColor, secondaryColor);
    }
}
