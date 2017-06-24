using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ObjectModel
{
    public struct EnumEx : IComparable<EnumEx>
    {
        public readonly string Name;
        public readonly int Value;
        public readonly string Id;

        public EnumEx(Enum enumValue)
        {
            Name = enumValue.ToString();
            Value = (int)(object)enumValue;
            Id = ComputeId(Name, Value);
        }

        public EnumEx(string name, int value)
        {
            Name = name;
            Value = value;
            Id = ComputeId(Name, Value);
        }

        private static string ComputeId(string name, int value)
        {
            return $"{Convert.ToString(value, 16).PadLeft(4, '0')}|{name}";
        }

        public int CompareTo(EnumEx other)
        {
            return Value.CompareTo(other.Value);
        }

        public override string ToString()
        {
            return Id;
        }
    }
}
