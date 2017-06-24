using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Utilities
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Murmur3
    {
        // 128 bit output, 64 bit platform version

        public static ulong READ_SIZE = 16;
        private static ulong C1 = 0x87c37b91114253d5L;
        private static ulong C2 = 0x4cf5ad432745937fL;

        private ulong processedCount;
        private uint seed; // if want to start with a seed, create a constructor
        ulong high;
        ulong low;

        public MurmurHash ComputeHash(byte[] bb, int start = 0, int length = -1)
        {
            Reset();
            ProcessBytes(bb, start, length < 0 ? bb.Length : length);
            ProcessFinal();
            return Hash;
        }

        private void ProcessBytes(byte[] bb, int start, int length)
        {
            Reset();

            int pos = start;
            ulong remaining = (ulong)length;

            // read 128 bits, 16 bytes, 2 longs in eacy cycle
            while (remaining >= READ_SIZE)
            {
                ulong k1 = bb.GetUInt64(pos);
                pos += 8;

                ulong k2 = bb.GetUInt64(pos);
                pos += 8;

                processedCount += READ_SIZE;
                remaining -= READ_SIZE;

                MixBody(k1, k2);
            }

            // if the input MOD 16 != 0
            if (remaining > 0)
            {
                ProcessBytesRemaining(bb, remaining, pos);
            }
        }

        private void Reset()
        {
            high = seed;
            low = 0;
            this.processedCount = 0L;
        }

        private void ProcessFinal()
        {
            high ^= processedCount;
            low ^= processedCount;

            high += low;
            low += high;

            high = MixFinal(high);
            low = MixFinal(low);

            high += low;
            low += high;
        }

        private void ProcessBytesRemaining(byte[] bb, ulong remaining, int pos)
        {
            ulong k1 = 0;
            ulong k2 = 0;
            processedCount += remaining;

            // little endian (x86) processing
            switch (remaining)
            {
                case 15:
                    k2 ^= (ulong)bb[pos + 14] << 48; // fall through
                    goto case 14;
                case 14:
                    k2 ^= (ulong)bb[pos + 13] << 40; // fall through
                    goto case 13;
                case 13:
                    k2 ^= (ulong)bb[pos + 12] << 32; // fall through
                    goto case 12;
                case 12:
                    k2 ^= (ulong)bb[pos + 11] << 24; // fall through
                    goto case 11;
                case 11:
                    k2 ^= (ulong)bb[pos + 10] << 16; // fall through
                    goto case 10;
                case 10:
                    k2 ^= (ulong)bb[pos + 9] << 8; // fall through
                    goto case 9;
                case 9:
                    k2 ^= (ulong)bb[pos + 8]; // fall through
                    goto case 8;
                case 8:
                    k1 ^= bb.GetUInt64(pos);
                    break;
                case 7:
                    k1 ^= (ulong)bb[pos + 6] << 48; // fall through
                    goto case 6;
                case 6:
                    k1 ^= (ulong)bb[pos + 5] << 40; // fall through
                    goto case 5;
                case 5:
                    k1 ^= (ulong)bb[pos + 4] << 32; // fall through
                    goto case 4;
                case 4:
                    k1 ^= (ulong)bb[pos + 3] << 24; // fall through
                    goto case 3;
                case 3:
                    k1 ^= (ulong)bb[pos + 2] << 16; // fall through
                    goto case 2;
                case 2:
                    k1 ^= (ulong)bb[pos + 1] << 8; // fall through
                    goto case 1;
                case 1:
                    k1 ^= (ulong)bb[pos]; // fall through
                    break;
                default:
                    throw new Exception("Something went wrong with remaining bytes calculation.");
            }

            high ^= MixKey1(k1);
            low ^= MixKey2(k2);
        }

        #region Mix Methods

        private void MixBody(ulong k1, ulong k2)
        {
            high ^= MixKey1(k1);

            high = high.RotateLeft(27);
            high += low;
            high = high * 5 + 0x52dce729;

            low ^= MixKey2(k2);

            low = low.RotateLeft(31);
            low += high;
            low = low * 5 + 0x38495ab5;
        }

        private static ulong MixKey1(ulong k1)
        {
            k1 *= C1;
            k1 = k1.RotateLeft(31);
            k1 *= C2;
            return k1;
        }

        private static ulong MixKey2(ulong k2)
        {
            k2 *= C2;
            k2 = k2.RotateLeft(33);
            k2 *= C1;
            return k2;
        }

        private static ulong MixFinal(ulong k)
        {
            // avalanche bits

            k ^= k >> 33;
            k *= 0xff51afd7ed558ccdL;
            k ^= k >> 33;
            k *= 0xc4ceb9fe1a85ec53L;
            k ^= k >> 33;
            return k;
        }

        #endregion

        public MurmurHash Hash
        {
            get
            {
                return new MurmurHash() { High = high, Low = low };
            }
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct MurmurHash
    {
        public const int BYTE_LENGTH = 16;

        [FieldOffset(0)]
        public ulong High;

        [FieldOffset(8)]
        public ulong Low;

        [FieldOffset(0)]
        private uint int_0;

        [FieldOffset(4)]
        private uint int_1;

        [FieldOffset(8)]
        private uint int_2;

        [FieldOffset(12)]
        private uint int_3;

        [FieldOffset(0)]
        private Guid guid;

        [FieldOffset(0)]
        private fixed byte bytes[16];

        public MurmurHash(uint int0, uint int1, uint int2, uint int3)
            : this()
        {
            int_0 = int0;
            int_1 = int1;
            int_2 = int2;
            int_3 = int3;
        }

        public Guid AsGuid()
        {
            return guid;
        }

        public override string ToString()
        {
            return guid.ToString();
        }

        public uint GetInt(int i)
        {
            if (unchecked((uint)i >= (uint)4))
            {
                throw new ArgumentOutOfRangeException();
            }

            fixed (byte* bytes = this.bytes)
            {
                return ((uint*)bytes)[i];
            }
        }

        public byte GetByte(int i)
        {
            if (unchecked((uint)i >= (uint)BYTE_LENGTH))
            {
                throw new ArgumentOutOfRangeException();
            }

            fixed (byte* bytes = this.bytes)
            {
                return bytes[i];
            }
        }
    }
}
