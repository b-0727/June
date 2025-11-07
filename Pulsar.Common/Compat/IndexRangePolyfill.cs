#if NETFRAMEWORK
namespace System
{
    /// <summary>
    /// Minimal polyfill for the <see cref="Index"/> struct introduced in newer .NET versions.
    /// Only the members required by the C# compiler when translating index syntax are provided.
    /// </summary>
    public readonly struct Index
    {
        private readonly int _value;

        public Index(int value, bool fromEnd)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _value = fromEnd ? ~value : value;
        }

        private Index(int value)
        {
            _value = value;
        }

        public static Index Start => new Index(0);

        public static Index End => new Index(~0);

        public int Value => _value < 0 ? ~_value : _value;

        public bool IsFromEnd => _value < 0;

        public static Index FromStart(int value) => new Index(value);

        public static Index FromEnd(int value) => new Index(~value);

        public int GetOffset(int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            int offset = IsFromEnd ? length - Value : Value;
            if (offset < 0 || offset > length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return offset;
        }

        public override bool Equals(object obj)
        {
            return obj is Index other && _value == other._value;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public override string ToString()
        {
            return IsFromEnd ? $"^{Value}" : Value.ToString();
        }
    }

    /// <summary>
    /// Minimal polyfill for the <see cref="Range"/> struct introduced in newer .NET versions.
    /// </summary>
    public readonly struct Range
    {
        public Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        public Index Start { get; }

        public Index End { get; }

        public static Range StartAt(Index start) => new Range(start, Index.End);

        public static Range EndAt(Index end) => new Range(Index.Start, end);

        public static Range All => new Range(Index.Start, Index.End);

        public void Deconstruct(out Index start, out Index end)
        {
            start = Start;
            end = End;
        }

        public (int Offset, int Length) GetOffsetAndLength(int length)
        {
            int start = Start.GetOffset(length);
            int end = End.GetOffset(length);
            if (end < start)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return (start, end - start);
        }

        public override bool Equals(object obj)
        {
            return obj is Range other && other.Start.Equals(Start) && other.End.Equals(End);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Start.GetHashCode() * 397) ^ End.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{Start}..{End}";
        }
    }
}
#endif
