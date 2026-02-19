using System.Text.RegularExpressions;

namespace Mozaika.Api.Services;

public static class ColorMath
{
    private static readonly Regex HexColorPattern = new("^[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public readonly record struct Lab(double L, double A, double B);

    public static string NormalizeHex(string value)
    {
        var raw = (value ?? string.Empty).Trim().TrimStart('#');
        if (raw.Length == 3)
        {
            raw = string.Concat(raw.Select(ch => $"{ch}{ch}"));
        }

        if (!HexColorPattern.IsMatch(raw))
        {
            throw new ArgumentException("HEX-цвет должен быть в формате #RRGGBB.");
        }

        return $"#{raw.ToUpperInvariant()}";
    }

    public static (byte R, byte G, byte B) HexToRgb(string value)
    {
        var normalized = NormalizeHex(value);
        return (
            Convert.ToByte(normalized.Substring(1, 2), 16),
            Convert.ToByte(normalized.Substring(3, 2), 16),
            Convert.ToByte(normalized.Substring(5, 2), 16)
        );
    }

    public static Lab RgbToLab(byte red, byte green, byte blue)
    {
        static double ToLinear(double channel)
        {
            return channel > 0.04045
                ? Math.Pow((channel + 0.055) / 1.055, 2.4)
                : channel / 12.92;
        }

        var r = ToLinear(red / 255.0);
        var g = ToLinear(green / 255.0);
        var b = ToLinear(blue / 255.0);

        var x = (r * 0.4124564) + (g * 0.3575761) + (b * 0.1804375);
        var y = (r * 0.2126729) + (g * 0.7151522) + (b * 0.0721750);
        var z = (r * 0.0193339) + (g * 0.1191920) + (b * 0.9503041);

        x /= 0.95047;
        y /= 1.00000;
        z /= 1.08883;

        static double F(double t)
        {
            const double delta = 6.0 / 29.0;
            var threshold = Math.Pow(delta, 3);
            return t > threshold
                ? Math.Pow(t, 1.0 / 3.0)
                : (t / (3 * delta * delta)) + (4.0 / 29.0);
        }

        var fx = F(x);
        var fy = F(y);
        var fz = F(z);

        var l = (116 * fy) - 16;
        var a = 500 * (fx - fy);
        var bCh = 200 * (fy - fz);
        return new Lab(l, a, bCh);
    }

    public static double SquaredDistance(Lab first, Lab second)
    {
        var dL = first.L - second.L;
        var dA = first.A - second.A;
        var dB = first.B - second.B;
        return (dL * dL) + (dA * dA) + (dB * dB);
    }
}
