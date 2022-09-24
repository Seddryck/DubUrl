﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.RegexUtils
{
    internal static class StringBuilderExtensions
    {
        private static char[] EscapedChars = new[] { '[', ']', '(', ')', '{', '}', '*', '+', '?', '|', '^', '$', '.', '\\', '-' };

        public static StringBuilder AppendEscaped(this StringBuilder stringBuilder, string text)
        {
            foreach (var c in text.ToArray())
            {
                if (EscapedChars.Contains(c))
                    stringBuilder.Append('\\');
                stringBuilder.Append(c);
            }
            return stringBuilder;
        }
    }
}