// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Ported from VarDecFromR4 and VarDecFromR8 of System.Decimal.DecCalc in dotnet/runtime
// (src/libraries/System.Private.CoreLib/src/System/Decimal.DecCalc.cs), itself a port of the OLEAUT32 functions of the same names.

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace RDCore.SDK.Model.Types;

// a Single or a Double made a Decimal as OLEAUT32 makes one: the value rounded to the 7 or 15 significant digits the floating-point type holds, a tie to the
// even digit, so that no digit past its precision reaches the Decimal. The framework's own conversion is not relied on: it keeps the exact binary value on
// .NET 11.
internal static class FloatingPointToDecimal
{
    private const int MaxScale = 28;

    // the powers of ten as Double literals, as DecCalc has them: past 1E22, each is the Double nearest the power.
    private static readonly double[] DoublePowersOf10 =
    [
        1E0, 1E1, 1E2, 1E3, 1E4, 1E5, 1E6, 1E7, 1E8, 1E9, 1E10, 1E11, 1E12, 1E13, 1E14,
        1E15, 1E16, 1E17, 1E18, 1E19, 1E20, 1E21, 1E22, 1E23, 1E24, 1E25, 1E26, 1E27, 1E28,
    ];

    // a Single's mantissa is scaled up by at most 10^22, a Double's by at most 10^14.
    private static readonly decimal[] DecimalPowersOf10 =
    [
        1m, 1E1m, 1E2m, 1E3m, 1E4m, 1E5m, 1E6m, 1E7m, 1E8m, 1E9m, 1E10m, 1E11m,
        1E12m, 1E13m, 1E14m, 1E15m, 1E16m, 1E17m, 1E18m, 1E19m, 1E20m, 1E21m, 1E22m,
    ];

    public static decimal FromSingle(float value)
        => From(value, (int)((BitConverter.SingleToUInt32Bits(value) >> 23) & 0xFF) - 126, digits: 7);

    public static decimal FromDouble(double value)
        => From(value, (int)((BitConverter.DoubleToUInt64Bits(value) >> 52) & 0x7FF) - 1022, digits: 15);

    // exponent: the value's binary exponent, such that the value is below 2^exponent; digits: the significant digits the source type holds.
    private static decimal From(double value, int exponent, int digits)
    {
        // the most a value can be scaled by is 10^28, a little more than 2^93: a value below 2^-94 rounds to 0.
        if (exponent < -94)
        {
            return 0m;
        }

        // an infinity and a NaN have the greatest exponent, and are past the range as well.
        if (exponent > 96)
        {
            throw new OverflowException("The value is past the range of a Decimal.");
        }

        var negative = value < 0;
        var dbl = Math.Abs(value);

        // the greatest power of ten the value can reach, from its exponent times log10(2): log10(2) * 2^16 = 19728.3.
        var power = digits - 1 - ((exponent * 19728) >> 16);
        if (power >= 0)
        {
            // fewer digits than the type holds: scale the value up.
            if (power > MaxScale)
            {
                power = MaxScale;
            }

            dbl *= DoublePowersOf10[power];
        }
        else if (power != -1 || dbl >= DoublePowersOf10[digits])
        {
            dbl /= DoublePowersOf10[-power];
        }
        else
        {
            power = 0;
        }

        if (dbl < DoublePowersOf10[digits - 1] && power < MaxScale)
        {
            dbl *= 10;
            power++;
        }

        var mantissa = (ulong)Math.Round(dbl, MidpointRounding.ToEven);
        if (mantissa == 0)
        {
            return 0m;
        }

        decimal result;
        if (power < 0)
        {
            // a whole number past the type's digits: the digits it does not hold are zeros.
            result = mantissa * DecimalPowersOf10[-power];
        }
        else
        {
            // the trailing zeros of the mantissa leave the scale, as many as the scale and the type's digits allow.
            var trimmable = Math.Min(power, digits - 1);
            while (trimmable > 0 && mantissa % 10 == 0)
            {
                mantissa /= 10;
                power--;
                trimmable--;
            }

            result = new decimal(unchecked((int)(uint)mantissa), unchecked((int)(uint)(mantissa >> 32)), 0, false, (byte)power);
        }

        return negative ? -result : result;
    }
}
