#if NET7_0_OR_GREATER
using System.Numerics;

namespace Utkan
{
    // Attach generic-math interfaces only on .NET 7+
    public readonly partial struct AdaptiveNumber :
        IAdditionOperators<AdaptiveNumber, AdaptiveNumber, AdaptiveNumber>,
        ISubtractionOperators<AdaptiveNumber, AdaptiveNumber, AdaptiveNumber>,
        IMultiplyOperators<AdaptiveNumber, AdaptiveNumber, AdaptiveNumber>,
        IDivisionOperators<AdaptiveNumber, AdaptiveNumber, AdaptiveNumber>,
        IUnaryNegationOperators<AdaptiveNumber, AdaptiveNumber>,
        IUnaryPlusOperators<AdaptiveNumber, AdaptiveNumber>,
        IIncrementOperators<AdaptiveNumber>,
        IDecrementOperators<AdaptiveNumber>,
        IEqualityOperators<AdaptiveNumber, AdaptiveNumber, bool>,
        IComparisonOperators<AdaptiveNumber, AdaptiveNumber, bool>,
        IAdditiveIdentity<AdaptiveNumber, AdaptiveNumber>,
        IMultiplicativeIdentity<AdaptiveNumber, AdaptiveNumber>
    {
        public static AdaptiveNumber AdditiveIdentity       => Zero;
        public static AdaptiveNumber MultiplicativeIdentity => One;
    }
}
#endif
