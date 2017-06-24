using Codex.Storage.Utilities;
using Codex.Utilities;
using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using System.IO;
using Newtonsoft.Json;

namespace Codex.Storage.DataModel
{
    public class IntegerListModel : IIndexable<int>
    {
        public int ValueByteWidth { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int MinValue { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int DecompressedLength { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int CompressedLength { get; set; }

        [Binary]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public byte[] Data { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, PropertyName = "cdata")]
        public string CompressedData { get; set; }

        [JsonIgnore]
        public int Count
        {
            get
            {
                var length = Data == null ? DecompressedLength : Data.Length;
                return length / ValueByteWidth;
            }
        }

        [JsonIgnore]
        public int DataLength
        {
            get
            {
                var length = Data == null ? DecompressedLength : Data.Length;
                return length;
            }
        }

        public IntegerListModel()
        {
        }

        public static IntegerListModel Create<T>(IReadOnlyList<T> values, Func<T, int> selector)
        {
            var minValue = values.Min(selector);
            var maxValue = values.Max(v => selector(v) - minValue);
            var byteWidth = NumberUtils.GetByteWidth(maxValue);
            var list = new IntegerListModel(byteWidth, values.Count, minValue);

            for (int i = 0; i < values.Count; i++)
            {
                list[i] = selector(values[i]);
            }

            return list;
        }

        public IntegerListModel(int byteWidth, int numberOfValues, int minValue)
        {
            Data = new byte[byteWidth * numberOfValues];
            ValueByteWidth = byteWidth;
            MinValue = minValue;
        }

        [Number(Ignore = true)]
        public int this[int index]
        {
            get
            {
                var value = GetIndexDirect(index);
                return value + MinValue;
            }

            set
            {
                value -= MinValue;
                SetIndexDirect(index, value);
            }
        }

        internal int GetIndexDirect(int index)
        {
            int value = 0;
            int dataIndex = index * ValueByteWidth;
            int byteOffset = 0;
            for (int i = 0; i < ValueByteWidth; i++, byteOffset += 8)
            {
                value |= (Data[dataIndex++] << byteOffset);
            }

            return value;
        }

        internal void SetIndexDirect(int index, int value)
        {
            int dataIndex = index * ValueByteWidth;
            for (int i = 0; i < ValueByteWidth; i++)
            {
                Data[dataIndex++] = unchecked((byte)value);
                value >>= 8;
            }
        }

        internal void Optimize(OptimizationContext context)
        {
            if (CompressedData == null && Data != null)
            {
                var compressedData = context.Compress(Data);
                if (compressedData.Length < Data.Length)
                {
                    DecompressedLength = Data.Length;
                    CompressedLength = compressedData.Length;
                    CompressedData = Convert.ToBase64String(compressedData);
                }
                else
                {
                    CompressedData = Convert.ToBase64String(Data);
                }

                Data = null;
            }
        }

        internal void ExpandData(OptimizationContext context)
        {
            Data = Convert.FromBase64String(CompressedData);
            if (DecompressedLength != 0)
            {
                var compressedData = Data;
                Data = new byte[DecompressedLength];
                using (var compressedStream = new DeflateStream(new MemoryStream(compressedData), CompressionMode.Decompress))
                {
                    compressedStream.Read(Data, 0, DecompressedLength);
                }
            }

            CompressedData = null;
            DecompressedLength = 0;
            CompressedLength = 0;
        }
    }
}
