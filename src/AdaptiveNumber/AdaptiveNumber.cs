using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace Utkan
{
    /// <summary>
    /// A value type that stores integers in <see cref="long"/> while they fit, and automatically
    /// promotes to <see cref="BigInteger"/> when needed. Supports arithmetic, comparison, and
    /// configurable rounding for integer division/percent math.
    /// </summary>
    public readonly partial struct AdaptiveNumber : IComparable<AdaptiveNumber>, IEquatable<AdaptiveNumber>
    {
        private readonly long small;
        private readonly BigInteger? big; // null => using small

        public static readonly AdaptiveNumber Zero = new(0);
        public static readonly AdaptiveNumber One = new(1);
        private static readonly BigInteger Scale100 = new(100);

        private bool IsBig => big.HasValue;

        private BigInteger Big => big ?? new BigInteger(small);

        /// <summary>Returns true if the numeric value equals zero</summary>
        public bool IsZero => !IsBig ? small == 0 : Big.IsZero;

        /// <summary>Create from <see cref="long"/> (stays in small mode)</summary>
        public AdaptiveNumber(long value) { small = value; big = null; }

        /// <summary>Create from <see cref="BigInteger"/> (downcasts if it fits in <see cref="long"/>)</summary>
        public AdaptiveNumber(BigInteger value)
        {
            if (value >= long.MinValue && value <= long.MaxValue)
            {
                small = (long)value;
                big = null;
            }
            else
            {
                small = 0;
                big = value;
            }
        }

        public static implicit operator AdaptiveNumber(long value) => new(value);
        public static implicit operator AdaptiveNumber(BigInteger value) => new(value);

        // Arithmetic operators

        public static AdaptiveNumber operator +(AdaptiveNumber a, AdaptiveNumber b)
        {
            if (!a.IsBig && !b.IsBig && TryAddNoOverflow(a.small, b.small, out long sum))
                return new AdaptiveNumber(sum);

            return new AdaptiveNumber(a.Big + b.Big);
        }

        public static AdaptiveNumber operator -(AdaptiveNumber a, AdaptiveNumber b)
        {
            if (!a.IsBig && !b.IsBig && TrySubNoOverflow(a.small, b.small, out long diff))
                return new AdaptiveNumber(diff);

            return new AdaptiveNumber(a.Big - b.Big);
        }

        public static AdaptiveNumber operator *(AdaptiveNumber a, AdaptiveNumber b)
        {
            if (!a.IsBig && !b.IsBig && TryMulNoOverflow(a.small, b.small, out long prod))
                return new AdaptiveNumber(prod);

            return new AdaptiveNumber(a.Big * b.Big);
        }

        /// <summary>
        /// Integer division using <see cref="PercentRounding.Truncate"/> by default (toward zero).
        /// Use <see cref="Divide(AdaptiveNumber, AdaptiveNumber, PercentRounding)"/> for other rounding modes.
        /// </summary>
        public static AdaptiveNumber operator /(AdaptiveNumber a, AdaptiveNumber b)
            => Divide(a, b, PercentRounding.Truncate);

        public static AdaptiveNumber operator ++(AdaptiveNumber x) => x + One;
        public static AdaptiveNumber operator --(AdaptiveNumber x) => x - One;
        public static AdaptiveNumber operator -(AdaptiveNumber a) => Zero - a;

        public static bool operator <(AdaptiveNumber a, AdaptiveNumber b) => a.CompareTo(b) < 0;
        public static bool operator >(AdaptiveNumber a, AdaptiveNumber b) => a.CompareTo(b) > 0;
        public static bool operator <=(AdaptiveNumber a, AdaptiveNumber b) => a.CompareTo(b) <= 0;
        public static bool operator >=(AdaptiveNumber a, AdaptiveNumber b) => a.CompareTo(b) >= 0;
        public static bool operator ==(AdaptiveNumber a, AdaptiveNumber b) => a.Equals(b);
        public static bool operator !=(AdaptiveNumber a, AdaptiveNumber b) => !a.Equals(b);

        // Comparison / equality

        /// <summary>Compares this value to <paramref name="other"/></summary>
        public int CompareTo(AdaptiveNumber other)
        {
            if (!IsBig && !other.IsBig) 
                return small.CompareTo(other.small);
            return Big.CompareTo(other.Big);
        }

        /// <summary>Value equality comparison with another <see cref="AdaptiveNumber"/></summary>
        public bool Equals(AdaptiveNumber other)
        {
            if (!IsBig && !other.IsBig) 
                return small == other.small;
            return Big.Equals(other.Big);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) 
            => obj is AdaptiveNumber number && Equals(number);

        public override int GetHashCode() 
            => !IsBig ? small.GetHashCode() : Big.GetHashCode();

        /// <summary>Culture invariant string form of the current value</summary>
        public override string ToString()
            => !IsBig ? small.ToString(CultureInfo.InvariantCulture)
                      : Big.ToString(CultureInfo.InvariantCulture);

        /// <summary>Attempts to materialize the value as <see cref="long"/> (returns false if it does not fit)</summary>
        public bool TryAsLong(out long value)
        {
            if (!IsBig) 
            { 
                value = small; 
                return true; 
            }

            if (Big >= long.MinValue && Big <= long.MaxValue) 
            { 
                value = (long)Big; 
                return true; 
            }

            value = 0; 
            return false;
        }

        // Rounding helpers

        private static BigInteger DivideRoundedBig(BigInteger dividend, BigInteger divisor, PercentRounding rounding)
        {
            if (divisor.IsZero) 
                throw new DivideByZeroException();

            if (rounding == PercentRounding.Truncate)
                return dividend / divisor;

            BigInteger quotient = BigInteger.DivRem(dividend, divisor, out BigInteger remainder);
            if (remainder.IsZero) 
                return quotient;

            BigInteger absRemainder = BigInteger.Abs(remainder);
            BigInteger absDivisor = BigInteger.Abs(divisor);
            BigInteger step = (dividend.Sign == divisor.Sign) ? BigInteger.One : BigInteger.MinusOne;

            if (rounding == PercentRounding.HalfAwayFromZero)
            {
                // absR * 2 >= absD -> move away from zero
                if (absRemainder * 2 >= absDivisor) 
                    quotient += step;
            }
            else if (rounding == PercentRounding.HalfToEven)
            {
                BigInteger twice = absRemainder * 2;
                if (twice > absDivisor) 
                    quotient += step;
                else if (twice == absDivisor && !quotient.IsEven) 
                    quotient += step; // exact half -> go to even
            }
            else throw new NotSupportedException($"Rounding mode {rounding} not supported.");

            return quotient;
        }

        private static long DivideRoundedLong(long dividend, long divisor, PercentRounding rounding)
        {
            if (divisor == 0) 
                throw new DivideByZeroException();

            if (rounding == PercentRounding.Truncate)
                return dividend / divisor;

            long quotient = dividend / divisor;
            long remainder = dividend % divisor;
            if (remainder == 0) 
                return quotient;

            long absRemainder = remainder >= 0 ? remainder : -remainder;
            long absDivisor = divisor >= 0 ? divisor : -divisor;
            bool sameSign = (dividend >= 0) == (divisor >= 0);
            long step = sameSign ? 1L : -1L;

            if (rounding == PercentRounding.HalfAwayFromZero)
            {
                // Increment if absR*2 >= absD  (avoid overflow: compare against ceil(absD/2))
                long halfCeil = absDivisor / 2 + ((absDivisor & 1L) != 0 ? 1L : 0L);
                if (absRemainder >= halfCeil) 
                    quotient += step;
            }
            else if (rounding == PercentRounding.HalfToEven)
            {
                long halfDown = absDivisor / 2;
                if (absRemainder > halfDown) quotient += step;
                else if (absRemainder == halfDown && (quotient & 1L) != 0L) 
                    quotient += step; // tie -> nearest even
            }
            else throw new NotSupportedException($"Rounding mode {rounding} not supported.");

            return quotient;
        }

        /// <summary>Rounded integer division</summary>
        public static AdaptiveNumber Divide(AdaptiveNumber dividend, AdaptiveNumber divisor, PercentRounding rounding)
        {
            if (!dividend.IsBig && !divisor.IsBig)
                return new AdaptiveNumber(DivideRoundedLong(dividend.small, divisor.small, rounding));

            return new AdaptiveNumber(DivideRoundedBig(dividend.Big, divisor.Big, rounding));
        }

        /// <summary>Computes (value * percent) / 100 using integer math with the given rounding</summary>
        public static AdaptiveNumber PercentageOf(AdaptiveNumber value, AdaptiveNumber percent, PercentRounding rounding)
        {
            if (!value.IsBig && !percent.IsBig && TryMulNoOverflow(value.small, percent.small, out long prod))
                return new AdaptiveNumber(DivideRoundedLong(prod, 100, rounding));

            return new AdaptiveNumber(DivideRoundedBig(value.Big * percent.Big, Scale100, rounding));
        }

        /// <summary>Parses an integer in invariant culture into an <see cref="AdaptiveNumber"/></summary>
        public static bool TryParse(string str, out AdaptiveNumber value)
        {
            if (BigInteger.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger bigInt))
            {
                value = new AdaptiveNumber(bigInt); // auto downcasts
                return true;
            }

            value = Zero;
            return false;
        }

        /// <summary>Parses an integer in invariant culture; throws if invalid</summary>
        public static AdaptiveNumber Parse(string str)
        {
            BigInteger bigInt = BigInteger.Parse(str, NumberStyles.Integer, CultureInfo.InvariantCulture);
            return new AdaptiveNumber(bigInt);
        }

        /// <summary>Precise ratio current / max as a <see cref="float"/> in [0,1]. Throws if <paramref name="max"/> is zero</summary>
        public static float Normalize(in AdaptiveNumber current, in AdaptiveNumber max)
        {
            BigInteger divisor = max.Big;
            if (divisor.IsZero) 
                throw new DivideByZeroException("Cannot normalize by zero");
            BigInteger dividend = current.Big;

            // Make divisor positive; flip dividend accordingly so the ratio keeps its sign
            if (divisor.Sign < 0) 
            { 
                divisor = BigInteger.Negate(divisor); 
                dividend = BigInteger.Negate(dividend); 
            }

            // Clamp obvious edges early
            if (dividend <= BigInteger.Zero) 
                return 0f;

            if (dividend >= divisor) 
                return 1f;

            // Fixed-point scale chosen for float precision:
            const int floatMantissaBits = 24;
            const int guardBits = 3;
            const int fixedPointBits = floatMantissaBits + guardBits;

            // Compute scaled fraction: floor((dividend << fixedPointBits) / divisor)
            BigInteger scaledDividend = dividend << fixedPointBits;
            BigInteger fixedPointValue = BigInteger.DivRem(scaledDividend, divisor, out BigInteger remainder);

            // Round to nearest, ties-to-even on the fixed-point integer
            BigInteger twiceRemainder = remainder << 1;
            int compare = twiceRemainder.CompareTo(divisor);
            if (compare > 0 || (compare == 0 && !fixedPointValue.IsEven))
                fixedPointValue += BigInteger.One;

            // fixedPointValue fits in 64 bits since fixedPointBits <= 27
            ulong fixedPointU64 = (ulong)fixedPointValue;

            // Convert to float by dividing by the fixed-point scale
            float result = (float)((double)fixedPointU64 / (double)(1UL << fixedPointBits));

            // Final safety clamp against tiny numeric drift
            if (result < 0f) 
                return 0f;

            if (result > 1f) 
                return 1f;

            return result;
        }

        // Overflow safe small path helpers

        private static bool TryAddNoOverflow(long x, long y, out long sum)
        {
            sum = x + y;
            // Overflow iff x and y have the same sign and sum has a different sign
            return ((x ^ sum) & (y ^ sum)) >= 0;
        }

        private static bool TrySubNoOverflow(long x, long y, out long diff)
        {
            diff = x - y;
            // Overflow iff x and y have different signs and diff has the sign of y
            return ((x ^ y) & (x ^ diff)) >= 0;
        }

        private static bool TryMulNoOverflow(long x, long y, out long product)
        {
            if (x == 0 || y == 0) 
            { product = 0; return true; }

            // The only case the division check misses is (-1 * long.MinValue)
            if ((x == -1 && y == long.MinValue) || (y == -1 && x == long.MinValue))
            { product = 0; return false; }

            long p = unchecked(x * y);
            if (p / x != y) 
            { product = 0; return false; }

            product = p;
            return true;
        }


    }

    // Extensions for instance centric convenience

    public static class AdaptiveNumberExtensions
    {
        /// <summary>Scales <paramref name="number"/> by a ratio <c>dividend/divisor</c> with rounding</summary>
        public static AdaptiveNumber Scale(this in AdaptiveNumber number, in AdaptiveNumber dividend, in AdaptiveNumber divisor, PercentRounding rounding)
            => AdaptiveNumber.Divide(number * dividend, divisor, rounding);

        /// <summary>Scales <paramref name="number"/> by <paramref name="percent"/> with rounding</summary>
        public static AdaptiveNumber ScaleByPercent(this in AdaptiveNumber number, in AdaptiveNumber percent, PercentRounding rounding)
            => number.Scale(percent, 100, rounding);

        /// <summary>Absolute value</summary>
        public static AdaptiveNumber Abs(this in AdaptiveNumber number)
            => (number < 0) ? -number : number;

        /// <summary>Alias for <see cref="AdaptiveNumber.Normalize(in AdaptiveNumber, in AdaptiveNumber)"/></summary>
        public static float ToRatioOf(this in AdaptiveNumber current, in AdaptiveNumber max)
            => AdaptiveNumber.Normalize(current, max);
    }

    // Static helpers for multi value operations

    public static class AdaptiveMath
    {
        public static AdaptiveNumber Min(in AdaptiveNumber a, in AdaptiveNumber b) 
            => (a < b) ? a : b;

        public static AdaptiveNumber Max(in AdaptiveNumber a, in AdaptiveNumber b) 
            => (a > b) ? a : b;

        /// <summary>Clamps <paramref name="number"/> to the inclusive range [<paramref name="min"/>, <paramref name="max"/>]</summary>
        public static AdaptiveNumber Clamp(in AdaptiveNumber number, in AdaptiveNumber min, in AdaptiveNumber max)
            => Max(min, Min(number, max));

        // Optional overload that forwards to the canonical implementation
        public static float Normalize(in AdaptiveNumber current, in AdaptiveNumber max)
            => AdaptiveNumber.Normalize(current, max);

        // Convenience overloads
        public static AdaptiveNumber Min(params AdaptiveNumber[] items) 
            => Min((ReadOnlySpan<AdaptiveNumber>)items);

        public static AdaptiveNumber Max(params AdaptiveNumber[] items) 
            => Max((ReadOnlySpan<AdaptiveNumber>)items);

        public static AdaptiveNumber Min(ReadOnlySpan<AdaptiveNumber> items)
        {
            if (items.Length == 0) 
                throw new ArgumentException("Empty span", nameof(items));

            AdaptiveNumber best = items[0];
            for (int i = 1; i < items.Length; i++)
            {
                if (items[i] < best)
                    best = items[i];
            }
            return best;
        }

        public static AdaptiveNumber Max(ReadOnlySpan<AdaptiveNumber> items)
        {
            if (items.Length == 0) 
                throw new ArgumentException("Empty span", nameof(items));

            AdaptiveNumber best = items[0];
            for (int i = 1; i < items.Length; i++)
            {
                if (items[i] > best)
                    best = items[i];
            }
            return best;
        }
    }

    /// <summary>Rounding modes for integer division and percent calculations</summary>
    public enum PercentRounding
    {
        /// <summary>Truncate toward zero (C# default integer division behavior)</summary>
        Truncate,
        /// <summary>Round to nearest; exact halves go away from zero (i.e. toward ±inf depending on sign)</summary>
        HalfAwayFromZero,
        /// <summary>Round to nearest; exact halves go to the nearest even integer (banker’s rounding)</summary>
        HalfToEven
    }
}
