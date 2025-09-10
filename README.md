# AdaptiveNumber

A small C# utility type that stores integers in `long` until it would overflow, then **automatically upgrades** to `BigInteger`. Includes precise division helpers with configurable rounding and a `Normalize` helper to turn big ratios into `[0..1]` floats safely.

> Why this exists: you sometimes want integers for exactness, but still need to cross `long` limits without throwing. `AdaptiveNumber` avoids surprises: small values stay fast, huge values stay correct.

## Highlights
- **Auto-switch** between `long` and `BigInteger`
- Safe **add/subtract/multiply/divide** operators
- **Rounding modes** for integer division:
  - `Truncate`
  - `HalfAwayFromZero`
  - `HalfToEven` (banker’s rounding)
- `Normalize(current, max)` → `float` in `[0..1]` with careful rounding
- `TryParse/Parse`, comparisons, equality, `TryAsLong`

## Quick usage

```csharp
using System;
using System.Numerics;
using Utkan; // namespace in the file

class Demo
{
    static void Main()
    {
        AdaptiveNumber a = long.MaxValue;
        AdaptiveNumber b = 10;

        // Multiplication promotes to BigInteger automatically (no overflow)
        AdaptiveNumber huge = a * b;
        Console.WriteLine(huge); // prints a big value

        // Integer division with rounding modes
        var d1 = AdaptiveNumber.Divide(5, 2, PercentRounding.Truncate);         // 2
        var d2 = AdaptiveNumber.Divide(5, 2, PercentRounding.HalfAwayFromZero); // 3
        var d3 = AdaptiveNumber.Divide(5, 2, PercentRounding.HalfToEven);       // 2 (banker’s to nearest even)

        // Percent helper: (value * percent) / 100 with rounding
        var tip = AdaptiveNumber.PercentageOf(150, 12, PercentRounding.HalfToEven); // 18

        // Normalize to [0..1] with good precision for floats
        float hp = AdaptiveNumber.Normalize(4500, 10000); // 0.45f

        // Optional: Try reading back as long
        if (huge.TryAsLong(out long smallValue))
            Console.WriteLine(smallValue);
        else
            Console.WriteLine("Does not fit in long");
    }
}
