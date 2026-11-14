// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Ported from VarR8FromDec and VarR4FromDec of System.Decimal.DecCalc in dotnet/runtime
// (src/libraries/System.Private.CoreLib/src/System/Decimal.DecCalc.cs, .NET 10), itself a port of the OLEAUT32 functions of the same names.

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace RDCore.SDK.Model.Types;

// a Decimal made a Double or a Single as OLEAUT32 makes one: the 96-bit integer made a Double, then divided by the Double power of ten of its scale, so that
// the value is rounded more than once. The framework's own conversion is not relied on: it rounds the exact value once on .NET 11.
internal static class DecimalToFloatingPoint
{
    // the Double that is 2^64.
    private const double TwoToThe64 = 1.8446744073709552E+019;

    // the powers of ten as Double literals, as DecCalc has them: past 1E22, each is the Double nearest the power.
    private static readonly double[] DoublePowersOf10 =
    [
        1E0, 1E1, 1E2, 1E3, 1E4, 1E5, 1E6, 1E7, 1E8, 1E9, 1E10, 1E11, 1E12, 1E13, 1E14,
        1E15, 1E16, 1E17, 1E18, 1E19, 1E20, 1E21, 1E22, 1E23, 1E24, 1E25, 1E26, 1E27, 1E28,
    ];

    public static double ToDouble(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        var low64 = (ulong)(uint)bits[0] | ((ulong)(uint)bits[1] << 32);
        var high = (uint)bits[2];
        var scale = (bits[3] >> 16) & 0xFF;

        // the low 64 bits are made a Double as a signed integer, with 2^64 added when the top bit is set, which rounds them twice too (observed in Excel):
        // DecCalc makes the unsigned integer a Double at once.
        var signed = (long)low64;
        var low = signed < 0 ? (double)signed + TwoToThe64 : signed;
        var result = (low + (double)high * TwoToThe64) / DoublePowersOf10[scale];
        return bits[3] < 0 ? -result : result;
    }

    public static float ToSingle(decimal value) => (float)ToDouble(value);
}
