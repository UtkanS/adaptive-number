using System;
using System.Globalization;
using System.Numerics;

namespace Utkan
{
    /// <summary>
    /// A number that uses <see cref="long"/> for small values and switches to <see cref="BigInteger"/>
    /// when the value exceeds the range of <see cref="long"/>. <br/>
    /// Supports addition, subtraction, multiplication, comparison, and equality.
    /// </summary>
    public readonly struct AdaptiveNumber : IComparable<AdaptiveNumber>, IEquatable<AdaptiveNumber>
    {
        private readonly long small;
        private readonly BigInteger? big; // null => using small
        public static readonly AdaptiveNumber Zero = new(0);
        public static readonly AdaptiveNumber One = new(1);
        private static readonly BigInteger Scale100 = new(100);

        private bool IsBig 
            => big.HasValue;

        private BigInteger Big 
            => big ?? new BigInteger(small);

        public bool IsZero 
            => !IsBig ? small == 0 : Big.IsZero;

        public AdaptiveNumber(long value) 
        { 
            small = value; 
            big = null; 
        }

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

        public static AdaptiveNumber operator +(AdaptiveNumber a, AdaptiveNumber b)
        {
            if (!a.IsBig && !b.IsBig)
            {
                try { checked { return new AdaptiveNumber(a.small + b.small); } }
                catch (OverflowException) { /* fallthrough */ }
            }
            return new AdaptiveNumber(a.Big + b.Big);
        }

        public static AdaptiveNumber operator -(AdaptiveNumber a, AdaptiveNumber b)
        {
            if (!a.IsBig && !b.IsBig)
            {
                try { checked { return new AdaptiveNumber(a.small - b.small); } }
                catch (OverflowException) { /* fallthrough */ }
            }
            return new AdaptiveNumber(a.Big - b.Big);
        }

        public static AdaptiveNumber operator *(AdaptiveNumber a, AdaptiveNumber b)
        {
            if (!a.IsBig && !b.IsBig)
            {
                try { checked { return new AdaptiveNumber(a.small * b.small); } }
                catch (OverflowException) { /* fallthrough */ }
            }
            return new AdaptiveNumber(a.Big * b.Big);
        }

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

        public int CompareTo(AdaptiveNumber other)
            => Big.CompareTo(other.Big);

        public bool Equals(AdaptiveNumber other)
            => Big == other.Big;

        public override bool Equals(object obj)
            => obj is AdaptiveNumber number && Equals(number);

        public override int GetHashCode()
            => Big.GetHashCode();

        public override string ToString() 
            => Big.ToString();

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

        private static BigInteger DivideRounded(BigInteger numerator, BigInteger denominator, PercentRounding rounding)
        {
            if (denominator.IsZero)
                throw new DivideByZeroException();

            if (rounding == PercentRounding.Truncate)
                return numerator / denominator;

            BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
            if (remainder.IsZero)
                return quotient;

            BigInteger absRemainder = BigInteger.Abs(remainder);
            BigInteger absDenominator = BigInteger.Abs(denominator);
            BigInteger incrementDirection = (numerator.Sign == denominator.Sign) ? BigInteger.One : BigInteger.MinusOne;

            if (rounding == PercentRounding.HalfAwayFromZero)
            {
                if (absRemainder * 2 >= absDenominator)
                    quotient += incrementDirection;
            }
            else if (rounding == PercentRounding.HalfToEven)
            {
                BigInteger twiceAbsRemainder = absRemainder * 2;

                if (twiceAbsRemainder > absDenominator)
                    quotient += incrementDirection;
                else if (twiceAbsRemainder == absDenominator && !quotient.IsEven)
                    quotient += incrementDirection; // exact half -> move toward the even integer
            }
            else throw new NotSupportedException($"Rounding mode {rounding} not supported.");

            return quotient;
        }

        /// <summary>Rounded integer division</summary>
        public static AdaptiveNumber Divide(AdaptiveNumber numerator, AdaptiveNumber denominator, PercentRounding rounding)
            => new(DivideRounded(numerator.Big, denominator.Big, rounding));

        public static AdaptiveNumber PercentageOf(AdaptiveNumber value, AdaptiveNumber percent, PercentRounding rounding)
            => new(DivideRounded(value.Big * percent.Big, Scale100, rounding));

        public static bool TryParse(string str, out AdaptiveNumber value)
        {
            if (BigInteger.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger big))
            {
                value = new AdaptiveNumber(big); // auto downcasts to long if it fits
                return true;
            }
            value = Zero;
            return false;
        }

        public static AdaptiveNumber Parse(string str)
        {
            BigInteger big = BigInteger.Parse(str, NumberStyles.Integer, CultureInfo.InvariantCulture);
            return new AdaptiveNumber(big);
        }

        /// <summary>Precise ratio current/max in [0,1]. Throws if max == 0</summary>
        public static float Normalize(in AdaptiveNumber current, in AdaptiveNumber max)
        {
            BigInteger denominator = max.Big;
            if (denominator.IsZero)
                throw new DivideByZeroException("Cannot normalize by zero.");

            BigInteger numerator = current.Big;

            // Make denominator positive; flip numerator accordingly so the ratio keeps its sign
            if (denominator.Sign < 0)
            {
                denominator = BigInteger.Negate(denominator);
                numerator = BigInteger.Negate(numerator);
            }

            // Clamp obvious edges early
            if (numerator <= BigInteger.Zero) 
                return 0f;

            if (numerator >= denominator) 
                return 1f;

            // Fixed-point scale chosen for float precision:
            // 24 bits mantissa for float
            //  A few guard bits to round correctly
            const int floatMantissaBits = 24;
            const int guardBits = 3;
            const int fixedPointBits = floatMantissaBits + guardBits;

            // Compute scaled fraction: floor((numerator << fixedPointBits) / denominator)
            BigInteger scaledNumerator = numerator << fixedPointBits;
            BigInteger fixedPointValue = BigInteger.DivRem(scaledNumerator, denominator, out BigInteger remainder);

            // Round to nearest, ties-to-even on the fixed-point integer
            BigInteger twiceRemainder = remainder << 1;
            int compare = twiceRemainder.CompareTo(denominator);
            if (compare > 0 || (compare == 0 && !fixedPointValue.IsEven))
                fixedPointValue += BigInteger.One;

            // fixedPointValue is in [0, 2^fixedPointBits). It fits in 64 bits since fixedPointBits <= 27
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
    }

    // Extensions for one-value / instance-centric ops
    public static class AdaptiveNumberExtensions
    {
        public static AdaptiveNumber Scale (this in AdaptiveNumber number, in AdaptiveNumber numerator, in AdaptiveNumber denominator, PercentRounding rounding)
            => AdaptiveNumber.Divide(number * numerator, denominator, rounding);

        public static AdaptiveNumber ScaleByPercent (this in AdaptiveNumber number, in AdaptiveNumber totalPercent, PercentRounding rounding)
            => number.Scale(totalPercent, 100, rounding);

        public static AdaptiveNumber Abs(this in AdaptiveNumber number)
            => (number < 0) ? -number : number;

        public static float ToRatioOf(this in AdaptiveNumber current, in AdaptiveNumber max)
            => AdaptiveNumber.Normalize(current, max);
    }

    // Static helpers for multi-value / aggregation ops
    public static class AdaptiveMath
    {
        public static AdaptiveNumber Min(in AdaptiveNumber a, in AdaptiveNumber b) 
            => (a < b) ? a : b;

        public static AdaptiveNumber Max(in AdaptiveNumber a, in AdaptiveNumber b) 
            => (a > b) ? a : b;

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

    public enum PercentRounding
    {
        Truncate,
        HalfAwayFromZero,
        HalfToEven
    }
}
