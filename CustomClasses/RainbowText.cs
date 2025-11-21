using System.Text;

namespace DefinitiveWeaponVariants.CustomClasses
{
    public static class RainbowText
    {
        /// <summary>
        /// Create rainbow text using Unity rich text color tags: <color=#RRGGBB>char</color>
        /// </summary>
        public static string RainbowUnityRichText(string input, bool skipSpaces = true)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new StringBuilder(input.Length * 12);
            int colorIndex = 0;
            int lengthForGradient = Math.Max(1, CountColorableChars(input, skipSpaces));

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (skipSpaces && Char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                    continue;
                }

                double t = (lengthForGradient == 1) ? 0.0 : (double)colorIndex / (lengthForGradient - 1);
                string hex = ColorFromHueToHex(t);
                sb.Append("<color=#").Append(hex).Append(">").Append(c).Append("</color>");
                colorIndex++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Create rainbow text using HTML span tags: <span style="color:#RRGGBB">char</span>
        /// </summary>
        public static string RainbowHtmlSpan(string input, bool skipSpaces = true)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new StringBuilder(input.Length * 15);
            int colorIndex = 0;
            int lengthForGradient = Math.Max(1, CountColorableChars(input, skipSpaces));

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (skipSpaces && Char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                    continue;
                }

                double t = (lengthForGradient == 1) ? 0.0 : (double)colorIndex / (lengthForGradient - 1);
                string hex = ColorFromHueToHex(t);
                sb.Append("<span style=\"color:#").Append(hex).Append("\">").Append(System.Net.WebUtility.HtmlEncode(c.ToString())).Append("</span>");
                colorIndex++;
            }

            return sb.ToString();
        }

        // Count characters that will receive colors (skip spaces option)
        private static int CountColorableChars(string s, bool skipSpaces)
        {
            if (!skipSpaces) return s.Length;
            int c = 0;
            foreach (var ch in s) if (!Char.IsWhiteSpace(ch)) c++;
            return c;
        }

        // Convert a normalized hue position t in [0,1] to an RGB hex string (RRGGBB).
        // We map t to hue 0..360 degrees and convert HSV(h,1,1) to RGB.
        private static string ColorFromHueToHex(double t)
        {
            // clamp t
            if (t < 0) t = 0;
            if (t > 1) t = 1;

            double hue = t * 360.0; // 0-360
            (int r, int g, int b) = HSVtoRGB(hue, 1.0, 1.0);
            return r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }

        // HSV (Hue 0..360, Sat 0..1, Val 0..1) to RGB 0..255
        private static (int r, int g, int b) HSVtoRGB(double h, double s, double v)
        {
            double c = v * s;
            double hh = (h / 60.0) % 6.0;
            double x = c * (1 - Math.Abs(hh % 2 - 1));
            double m = v - c;

            double rf = 0, gf = 0, bf = 0;
            if (0 <= hh && hh < 1) { rf = c; gf = x; bf = 0; }
            else if (1 <= hh && hh < 2) { rf = x; gf = c; bf = 0; }
            else if (2 <= hh && hh < 3) { rf = 0; gf = c; bf = x; }
            else if (3 <= hh && hh < 4) { rf = 0; gf = x; bf = c; }
            else if (4 <= hh && hh < 5) { rf = x; gf = 0; bf = c; }
            else if (5 <= hh && hh < 6) { rf = c; gf = 0; bf = x; }

            int r = (int)Math.Round((rf + m) * 255.0);
            int g = (int)Math.Round((gf + m) * 255.0);
            int b = (int)Math.Round((bf + m) * 255.0);

            r = Clamp(r, 0, 255);
            g = Clamp(g, 0, 255);
            b = Clamp(b, 0, 255);

            return (r, g, b);
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
