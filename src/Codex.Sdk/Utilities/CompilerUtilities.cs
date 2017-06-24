using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Codex.ObjectModel;
using System;

namespace Codex.Utilities
{
    public static class CompilerUtilities
    {
        public static string GetString<TEnum>(this TEnum value) where TEnum : struct
        {
            return EnumMapper<TEnum>.GetString(value);
        }

        private static class EnumMapper<TEnum> where TEnum : struct
        {
            private static Dictionary<TEnum, string> Strings = new Dictionary<TEnum, string>();

            static EnumMapper()
            {
                foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
                {
                    Strings[value] = value.ToString();
                }
            }

            public static string GetString(TEnum value)
            {
                return Strings[value];
            }
        }
    }
}
