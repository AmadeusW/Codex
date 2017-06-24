using System.Diagnostics.CodeAnalysis;

namespace Codex.Storage.Utilities
{
    /// <summary>
    /// Bit manipulation utilities
    /// </summary>
    public static class NumberUtils
    {
        public static int Ceiling(int numerator, int denominator)
        {
            return 1 + ((numerator - 1) / denominator);
        }

        public static int GetByteWidth(int maxValue)
        {
            return Ceiling(FindHighestBitSet(maxValue) + 1, 8);
        }

        public static int FindHighestBitSet(int n)
        {
            unchecked
            {
                var highestBitSetValue = HighestBitSet((uint)n);
                switch (highestBitSetValue)
                {
                    case 0:
                        return -1;
                    case 1:
                        return 0;
                    case 1 << 1:
                        return 1;
                    case 1 << 2:
                        return 2;
                    case 1 << 3:
                        return 3;
                    case 1 << 4:
                        return 4;
                    case 1 << 5:
                        return 5;
                    case 1 << 6:
                        return 6;
                    case 1 << 7:
                        return 7;
                    case 1 << 8:
                        return 8;
                    case 1 << 9:
                        return 9;
                    case 1 << 10:
                        return 10;
                    case 1 << 11:
                        return 11;
                    case 1 << 12:
                        return 12;
                    case 1 << 13:
                        return 13;
                    case 1 << 14:
                        return 14;
                    case 1 << 15:
                        return 15;
                    case 1 << 16:
                        return 16;
                    case 1 << 17:
                        return 17;
                    case 1 << 18:
                        return 18;
                    case 1 << 19:
                        return 19;
                    case 1 << 20:
                        return 20;
                    case 1 << 21:
                        return 21;
                    case 1 << 22:
                        return 22;
                    case 1 << 23:
                        return 23;
                    case 1 << 24:
                        return 24;
                    case 1 << 25:
                        return 25;
                    case 1 << 26:
                        return 26;
                    case 1 << 27:
                        return 27;
                    case 1 << 28:
                        return 28;
                    case 1 << 29:
                        return 29;
                    case 1 << 30:
                        return 30;
                    default:
                        return 31;
                }
            }
        }

        /// <summary>
        /// Returns an integer with only the highest bit set
        /// </summary>
        public static uint HighestBitSet(uint n)
        {
            unchecked
            {
                n |= (n >> 1);
                n |= (n >> 2);
                n |= (n >> 4);
                n |= (n >> 8);
                n |= (n >> 16);
                return (n - (n >> 1));
            }
        }

        /// <summary>
        /// Gets whether the bit specified by the given offset is set
        /// </summary>
        public static bool IsBitSet(uint bits, byte bitOffset)
        {
            return (bits & (1 << bitOffset)) != 0;
        }

        /// <summary>
        /// Gets whether the bit specified by the given offset is set
        /// </summary>
        public static void SetBit(ref uint bits, byte bitOffset, bool value)
        {
            if (value)
            {
                bits |= unchecked((uint)(1 << bitOffset));
            }
            else
            {
                bits &= unchecked((uint)(~(1 << bitOffset)));
            }
        }
    }
}