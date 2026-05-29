// Generated from src/Sharpy.Stdlib/spy/colorsys_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/colorsys_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    public static partial class ColorsysModule
    {
        /// <summary>
        /// Convert the color from RGB coordinates to HSV coordinates.
        /// </summary>
        public static global::System.ValueTuple<double, double, double> Rgb_to_hsv(double r, double g, double b)
        {
            double maxc = global::System.Math.Max(r, global::System.Math.Max(g, b));
            double minc = global::System.Math.Min(r, global::System.Math.Min(g, b));
            double v = maxc;
            if (minc == maxc)
            {
                return (0.0, 0.0, v);
            }
            double s = (maxc - minc) / maxc;
            double rc = (maxc - r) / (maxc - minc);
            double gc = (maxc - g) / (maxc - minc);
            double bc = (maxc - b) / (maxc - minc);
            double h;
            if (r == maxc)
            {
                h = bc - gc;
            }
            else if (g == maxc)
            {
                h = 2.0 + rc - bc;
            }
            else
            {
                h = 4.0 + gc - rc;
            }
            h = (h / 6.0) % 1.0;
            if (h < 0.0)
            {
                h = h + 1.0;
            }
            return (h, s, v);
        }

        /// <summary>
        /// Convert the color from HSV coordinates to RGB coordinates.
        /// </summary>
        public static global::System.ValueTuple<double, double, double> Hsv_to_rgb(double h, double s, double v)
        {
            if (s == 0.0)
            {
                return (v, v, v);
            }
            int i = (int)(h * 6.0);
            double f = (h * 6.0) - i;
            double p = v * (1.0 - s);
            double q = v * (1.0 - s * f);
            double t = v * (1.0 - s * (1.0 - f));
            i = i % 6;
            if (i == 0)
            {
                return (v, t, p);
            }
            if (i == 1)
            {
                return (q, v, p);
            }
            if (i == 2)
            {
                return (p, v, t);
            }
            if (i == 3)
            {
                return (p, q, v);
            }
            if (i == 4)
            {
                return (t, p, v);
            }
            return (v, p, q);
        }

        /// <summary>
        /// Convert the color from RGB coordinates to HLS coordinates.
        /// </summary>
        public static global::System.ValueTuple<double, double, double> Rgb_to_hls(double r, double g, double b)
        {
            double maxc = global::System.Math.Max(r, global::System.Math.Max(g, b));
            double minc = global::System.Math.Min(r, global::System.Math.Min(g, b));
            double sumc = maxc + minc;
            double rangec = maxc - minc;
            double l = sumc / 2.0;
            if (minc == maxc)
            {
                return (0.0, l, 0.0);
            }
            double s;
            if (l <= 0.5)
            {
                s = rangec / sumc;
            }
            else
            {
                s = rangec / (2.0 - sumc);
            }
            double rc = (maxc - r) / rangec;
            double gc = (maxc - g) / rangec;
            double bc = (maxc - b) / rangec;
            double h;
            if (r == maxc)
            {
                h = bc - gc;
            }
            else if (g == maxc)
            {
                h = 2.0 + rc - bc;
            }
            else
            {
                h = 4.0 + gc - rc;
            }
            h = (h / 6.0) % 1.0;
            if (h < 0.0)
            {
                h = h + 1.0;
            }
            return (h, l, s);
        }

        /// <summary>
        /// Convert the color from HLS coordinates to RGB coordinates.
        /// </summary>
        public static global::System.ValueTuple<double, double, double> Hls_to_rgb(double h, double l, double s)
        {
            if (s == 0.0)
            {
                return (l, l, l);
            }
            double m2;
            if (l <= 0.5)
            {
                m2 = l * (1.0 + s);
            }
            else
            {
                m2 = l + s - (l * s);
            }
            double m1 = 2.0 * l - m2;
            return (_V(m1, m2, h + (1.0 / 3.0)), _V(m1, m2, h), _V(m1, m2, h - (1.0 / 3.0)));
        }

        private static double _V(double m1, double m2, double hue)
        {
            hue = hue % 1.0;
            if (hue < 0.0)
            {
                hue = hue + 1.0;
            }
            if (hue < (1.0 / 6.0))
            {
                return m1 + (m2 - m1) * hue * 6.0;
            }
            if (hue < 0.5)
            {
                return m2;
            }
            if (hue < (2.0 / 3.0))
            {
                return m1 + (m2 - m1) * ((2.0 / 3.0) - hue) * 6.0;
            }
            return m1;
        }

        /// <summary>
        /// Convert the color from RGB coordinates to YIQ coordinates.
        /// </summary>
        public static global::System.ValueTuple<double, double, double> Rgb_to_yiq(double r, double g, double b)
        {
            double y = 0.30 * r + 0.59 * g + 0.11 * b;
            double i = 0.74 * (r - y) - 0.27 * (b - y);
            double q = 0.48 * (r - y) + 0.41 * (b - y);
            return (y, i, q);
        }

        /// <summary>
        /// Convert the color from YIQ coordinates to RGB coordinates.
        /// </summary>
        public static global::System.ValueTuple<double, double, double> Yiq_to_rgb(double y, double i, double q)
        {
            double r = y + 0.9468822170900693 * i + 0.6235565819861433 * q;
            double g = y - 0.27478764629897834 * i - 0.6356910791873801 * q;
            double b = y - 1.1085450346420322 * i + 1.7090069284064666 * q;
            if (r < 0.0)
            {
                r = 0.0;
            }
            if (r > 1.0)
            {
                r = 1.0;
            }
            if (g < 0.0)
            {
                g = 0.0;
            }
            if (g > 1.0)
            {
                g = 1.0;
            }
            if (b < 0.0)
            {
                b = 0.0;
            }
            if (b > 1.0)
            {
                b = 1.0;
            }
            return (r, g, b);
        }
    }
}
