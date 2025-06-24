// Author: Adalberto L. Simeone (Taranto, Italy)
// E-Mail: avengerdragon@gmail.com
// Website: http://www.avengersutd.com/blog
//
// This source code is Intellectual property of the Author
// and is released under the Creative Commons Attribution
// NonCommercial License, available at:
// http://creativecommons.org/licenses/by-nc/3.0/
// You can alter and use this source code as you wish,
// provided that you do not use the results in commercial
// projects, without the express and written consent of
// the Author.

using System;
using System.Drawing;

namespace Nikse.SubtitleEdit.Logic.ColorChooser
{
    public static class ColorHandler
    {
        // Handle conversions between RGB and HSV
        // (and Color types, as well).

        public static Argb HsvToRgb(int a, int h, int s, int v)
        {
            // H, S, and V must all be between 0 and 255.
            return HsvToRgb(new Hsv(a, h, s, v));
        }

        public static Color HsvToColor(Hsv hsv)
        {
            var argb = HsvToRgb(hsv);
            return Color.FromArgb(argb.Alpha, argb.Red, argb.Green, argb.Blue);
        }

        public static Color HsvToColor(int a, int h, int s, int v)
        {
            return HsvToColor(new Hsv(a, h, s, v));
        }

        public static Argb HsvToRgb(Hsv hsv)
        {
            // HSV contains values scaled as in the color wheel (0-255)
            // Convert to standard HSV ranges: H(0-360), S(0-1), V(0-1)
            var h = hsv.Hue * 360.0 / 255.0;
            var s = hsv.Saturation / 255.0;
            var v = hsv.Value / 255.0;

            double r, g, b;

            if (s < 0.01)
            {
                // Achromatic (gray) - saturation is effectively zero
                r = g = b = v;
            }
            else
            {
                // Chromatic color conversion
                var hi = (int)(h / 60) % 6;
                var f = h / 60 - hi;
                var p = v * (1 - s);
                var q = v * (1 - s * f);
                var t = v * (1 - s * (1 - f));

                (r, g, b) = hi switch
                {
                    0 => (v, t, p),
                    1 => (q, v, p),
                    2 => (p, v, t),
                    3 => (p, q, v),
                    4 => (t, p, v),
                    _ => (v, p, q)  // case 5
                };
            }

            // Convert back to 0-255 range and clamp values
            return new Argb(hsv.Alpha, 
                           (int)Math.Round(r * 255), 
                           (int)Math.Round(g * 255), 
                           (int)Math.Round(b * 255));
        }

        public static Hsv RgbToHsv(Argb argb)
        {
            // Normalize RGB values to 0-1 range
            var r = argb.Red / 255.0;
            var g = argb.Green / 255.0;
            var b = argb.Blue / 255.0;

            var min = Math.Min(Math.Min(r, g), b);
            var max = Math.Max(Math.Max(r, g), b);
            var delta = max - min;

            double h = 0;
            double s = max < 0.01 ? 0 : delta / max;
            var v = max;

            if (delta > 0.01)
            {
                if (Math.Abs(r - max) < 0.01)
                {
                    h = (g - b) / delta + (g < b ? 6 : 0);
                }
                else if (Math.Abs(g - max) < 0.01)
                {
                    h = (b - r) / delta + 2;
                }
                else
                {
                    h = (r - g) / delta + 4;
                }
                h /= 6;
            }

            // Convert to application's 0-255 scale
            return new Hsv(argb.Alpha, 
                          (int)Math.Round(h * 255), 
                          (int)Math.Round(s * 255), 
                          (int)Math.Round(v * 255));
        }

        public readonly struct Argb
        {
            // All values are between 0 and 255.
            public Argb(int a, int r, int g, int b)
            {
                Alpha = Math.Clamp(a, 0, byte.MaxValue);
                Red = Math.Clamp(r, 0, byte.MaxValue);
                Green = Math.Clamp(g, 0, byte.MaxValue);
                Blue = Math.Clamp(b, 0, byte.MaxValue);
            }

            public int Alpha { get; }
            public int Red { get; }
            public int Green { get; }
            public int Blue { get; }

            public override string ToString() => $"({Alpha}, {Red}, {Green}, {Blue})";
        }

        public readonly struct Hsv
        {
            // All values are between 0 and 255.
            public Hsv(int a, int h, int s, int v)
            {
                Alpha = Math.Clamp(a, 0, byte.MaxValue);
                Hue = Math.Clamp(h, 0, byte.MaxValue);
                Saturation = Math.Clamp(s, 0, byte.MaxValue);
                Value = Math.Clamp(v, 0, byte.MaxValue);
            }

            public int Alpha { get; }
            public int Hue { get; }
            public int Saturation { get; }
            public int Value { get; }

            public override string ToString() => $"({Hue}, {Saturation}, {Value})";
        }
    }
}
