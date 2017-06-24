using Codex.Utilities;

namespace Codex.ObjectModel
{
    public struct SymbolId
    {
        public readonly string Value;

        private SymbolId(string value)
        {
            Value = value;
        }

        public static SymbolId CreateFromId(string id)
        {
            // return new SymbolId(id);
            return new SymbolId(IndexingUtilities.ComputeSymbolUid(id));
        }

        public static SymbolId UnsafeCreateWithValue(string value)
        {
            return new SymbolId(value);
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
