using System.Globalization;
using System.Numerics;

namespace GonieGonie.BuildingEnergy.Contracts.Internal;

/// <summary>
/// Formats the shortest round-tripping IEEE-754 binary64 decimal without depending on the host CLR's dtoa implementation.
/// </summary>
internal static class CanonicalDouble
{
    private const int SignificantDigits = 17;
    private const double Log10OfTwo = 0.30102999566398119521373889472449d;

    internal static string FormatCanonical(double value) => Format(value, pythonLayout: false);

    internal static string FormatPythonFloat(double value) => Format(value, pythonLayout: true);

    private static string Format(double value, bool pythonLayout)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        bool negative = bits < 0;
        long magnitudeBits = bits & long.MaxValue;
        int exponentBits = (int)((magnitudeBits >> 52) & 0x7ffL);
        long fraction = magnitudeBits & 0x000fffffffffffffL;
        if (exponentBits == 0x7ff)
        {
            if (!pythonLayout)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "A canonical JSON number must be finite.");
            }

            if (fraction != 0)
            {
                return "nan";
            }

            return negative ? "-inf" : "inf";
        }

        if (magnitudeBits == 0)
        {
            if (pythonLayout)
            {
                return negative ? "-0.0" : "0.0";
            }

            return negative ? "-0" : "0";
        }

        long mantissa;
        int binaryExponent;
        if (exponentBits == 0)
        {
            mantissa = fraction;
            binaryExponent = -1074;
        }
        else
        {
            mantissa = (1L << 52) | fraction;
            binaryExponent = exponentBits - 1023 - 52;
        }

        BigInteger numerator = mantissa;
        BigInteger denominator = BigInteger.One;
        if (binaryExponent >= 0)
        {
            numerator <<= binaryExponent;
        }
        else
        {
            denominator <<= -binaryExponent;
        }

        int binaryMagnitude = BitLength(mantissa) - 1 + binaryExponent;
        int decimalExponent = (int)Math.Floor(binaryMagnitude * Log10OfTwo);
        while (CompareToPowerOfTen(numerator, denominator, decimalExponent) < 0)
        {
            decimalExponent--;
        }

        while (CompareToPowerOfTen(numerator, denominator, decimalExponent + 1) >= 0)
        {
            decimalExponent++;
        }

        for (int precision = 1; precision <= SignificantDigits; precision++)
        {
            PrecisionCandidates candidates = CandidatesAtPrecision(
                numerator,
                denominator,
                decimalExponent,
                precision);
            if (RoundsToBinary64(
                    candidates.Primary.Coefficient,
                    candidates.Primary.Exponent,
                    magnitudeBits))
            {
                return FormatSigned(candidates.Primary, negative, pythonLayout);
            }

            if (candidates.Alternate.HasValue
                && RoundsToBinary64(
                    candidates.Alternate.Value.Coefficient,
                    candidates.Alternate.Value.Exponent,
                    magnitudeBits))
            {
                return FormatSigned(candidates.Alternate.Value, negative, pythonLayout);
            }
        }

        throw new InvalidOperationException("Could not produce a round-tripping binary64 decimal.");
    }

    private static int BitLength(long value)
    {
        int length = 0;
        while (value != 0)
        {
            value >>= 1;
            length++;
        }

        return length;
    }

    private static int CompareToPowerOfTen(
        BigInteger numerator,
        BigInteger denominator,
        int exponent)
    {
        return exponent >= 0
            ? numerator.CompareTo(denominator * PowerOfTen(exponent))
            : (numerator * PowerOfTen(-exponent)).CompareTo(denominator);
    }

    private static BigInteger PowerOfTen(int exponent)
    {
        return BigInteger.Pow(10, exponent);
    }

    private static PrecisionCandidates CandidatesAtPrecision(
        BigInteger numerator,
        BigInteger denominator,
        int decimalExponent,
        int precision)
    {
        int decimalShift = precision - 1 - decimalExponent;
        BigInteger scaledNumerator = numerator;
        BigInteger scaledDenominator = denominator;
        if (decimalShift >= 0)
        {
            scaledNumerator *= PowerOfTen(decimalShift);
        }
        else
        {
            scaledDenominator *= PowerOfTen(-decimalShift);
        }

        BigInteger remainder;
        BigInteger floor = BigInteger.DivRem(
            scaledNumerator,
            scaledDenominator,
            out remainder);
        RoundedDecimal floorCandidate = NormalizeCandidate(floor, decimalExponent, precision);
        if (remainder.IsZero)
        {
            return new PrecisionCandidates(floorCandidate, null);
        }

        BigInteger ceiling = floor + BigInteger.One;
        RoundedDecimal ceilingCandidate = NormalizeCandidate(ceiling, decimalExponent, precision);
        int halfComparison = (remainder << 1).CompareTo(scaledDenominator);
        bool floorIsPrimary = halfComparison < 0
            || (halfComparison == 0 && floor.IsEven);
        return floorIsPrimary
            ? new PrecisionCandidates(floorCandidate, ceilingCandidate)
            : new PrecisionCandidates(ceilingCandidate, floorCandidate);
    }

    private static RoundedDecimal NormalizeCandidate(
        BigInteger coefficient,
        int decimalExponent,
        int precision)
    {
        int normalizedExponent = decimalExponent;
        if (coefficient == PowerOfTen(precision))
        {
            coefficient /= 10;
            normalizedExponent++;
        }

        int trailingZeros = 0;
        while (coefficient % 10 == 0)
        {
            coefficient /= 10;
            trailingZeros++;
        }

        return new RoundedDecimal(
            coefficient,
            normalizedExponent - (precision - 1) + trailingZeros,
            normalizedExponent);
    }

    private static string FormatSigned(
        RoundedDecimal candidate,
        bool negative,
        bool pythonLayout)
    {
        string digits = candidate.Coefficient.ToString(CultureInfo.InvariantCulture);
        bool fixedNotation = pythonLayout
            ? candidate.DecimalExponent is >= -4 and <= 15
            : candidate.DecimalExponent is >= -6 and <= 20;
        string magnitude = fixedNotation
            ? FormatFixed(digits, candidate.Exponent)
            : FormatScientific(digits, candidate.DecimalExponent, padExponent: pythonLayout);
        if (pythonLayout && fixedNotation && magnitude.IndexOf('.') < 0)
        {
            magnitude += ".0";
        }

        return negative ? "-" + magnitude : magnitude;
    }

    private static bool RoundsToBinary64(
        BigInteger coefficient,
        int coefficientExponent,
        long magnitudeBits)
    {
        BigInteger candidateNumerator = coefficient;
        BigInteger candidateDenominator = BigInteger.One;
        if (coefficientExponent >= 0)
        {
            candidateNumerator *= PowerOfTen(coefficientExponent);
        }
        else
        {
            candidateDenominator *= PowerOfTen(-coefficientExponent);
        }

        BinaryRational current = DecodeMagnitude(magnitudeBits);
        BinaryRational previous = DecodeMagnitude(magnitudeBits - 1);
        BinaryRational next = magnitudeBits == 0x7fefffffffffffffL
            ? new BinaryRational(BigInteger.One << 1024, BigInteger.One)
            : DecodeMagnitude(magnitudeBits + 1);
        BinaryRational lower = Midpoint(previous, current);
        BinaryRational upper = Midpoint(current, next);
        int lowerComparison = CompareRationals(
            candidateNumerator,
            candidateDenominator,
            lower.Numerator,
            lower.Denominator);
        int upperComparison = CompareRationals(
            candidateNumerator,
            candidateDenominator,
            upper.Numerator,
            upper.Denominator);
        bool even = (magnitudeBits & 1L) == 0;
        return even
            ? lowerComparison >= 0 && upperComparison <= 0
            : lowerComparison > 0 && upperComparison < 0;
    }

    private static BinaryRational DecodeMagnitude(long magnitudeBits)
    {
        if (magnitudeBits == 0)
        {
            return new BinaryRational(BigInteger.Zero, BigInteger.One);
        }

        int exponentBits = (int)((magnitudeBits >> 52) & 0x7ffL);
        long fraction = magnitudeBits & 0x000fffffffffffffL;
        long mantissa;
        int binaryExponent;
        if (exponentBits == 0)
        {
            mantissa = fraction;
            binaryExponent = -1074;
        }
        else
        {
            mantissa = (1L << 52) | fraction;
            binaryExponent = exponentBits - 1023 - 52;
        }

        BigInteger numerator = mantissa;
        BigInteger denominator = BigInteger.One;
        if (binaryExponent >= 0)
        {
            numerator <<= binaryExponent;
        }
        else
        {
            denominator <<= -binaryExponent;
        }

        return new BinaryRational(numerator, denominator);
    }

    private static BinaryRational Midpoint(BinaryRational left, BinaryRational right)
    {
        return new BinaryRational(
            (left.Numerator * right.Denominator) + (right.Numerator * left.Denominator),
            2 * left.Denominator * right.Denominator);
    }

    private static int CompareRationals(
        BigInteger leftNumerator,
        BigInteger leftDenominator,
        BigInteger rightNumerator,
        BigInteger rightDenominator)
    {
        return (leftNumerator * rightDenominator).CompareTo(
            rightNumerator * leftDenominator);
    }

    private static string FormatFixed(string digits, int coefficientExponent)
    {
        int decimalPoint = digits.Length + coefficientExponent;
        if (decimalPoint <= 0)
        {
            return "0." + new string('0', -decimalPoint) + digits;
        }

        if (decimalPoint >= digits.Length)
        {
            return digits + new string('0', decimalPoint - digits.Length);
        }

        return digits.Insert(decimalPoint, ".");
    }

    private static string FormatScientific(
        string digits,
        int decimalExponent,
        bool padExponent = false)
    {
        string significand = digits.Length == 1
            ? digits
            : digits[0] + "." + digits.Substring(1);
        string exponentDigits = Math.Abs(decimalExponent).ToString(
            padExponent ? "D2" : "D",
            CultureInfo.InvariantCulture);
        string exponent = (decimalExponent >= 0 ? "+" : "-") + exponentDigits;
        return significand + "e" + exponent;
    }

    private readonly struct RoundedDecimal
    {
        internal RoundedDecimal(
            BigInteger coefficient,
            int exponent,
            int decimalExponent)
        {
            Coefficient = coefficient;
            Exponent = exponent;
            DecimalExponent = decimalExponent;
        }

        internal BigInteger Coefficient { get; }

        internal int Exponent { get; }

        internal int DecimalExponent { get; }
    }

    private readonly struct PrecisionCandidates
    {
        internal PrecisionCandidates(RoundedDecimal primary, RoundedDecimal? alternate)
        {
            Primary = primary;
            Alternate = alternate;
        }

        internal RoundedDecimal Primary { get; }

        internal RoundedDecimal? Alternate { get; }
    }

    private readonly struct BinaryRational
    {
        internal BinaryRational(BigInteger numerator, BigInteger denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
        }

        internal BigInteger Numerator { get; }

        internal BigInteger Denominator { get; }
    }
}
