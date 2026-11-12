using System.Text;

using System.Globalization;
using System.Text.RegularExpressions;

namespace DefinitiveWeaponVariants.Helpers
{
    public static class TextColoring
    {
        // The dwv- prefix keeps effect tags separate from {locale} placeholders
        // and Unity's built-in rich-text tags. Resolve locale placeholders first.
        private static readonly Regex ColorTags = new(
            @"<dwv-(?<effect>rainbow|gradient)(?:=(?<start>#[0-9a-f]{6}|[0-9a-f]{6}),(?<end>#[0-9a-f]{6}|[0-9a-f]{6}))?>(?<text>(?:(?!</?dwv-)[\s\S])*)</dwv-\k<effect>>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Expands paired &lt;dwv-rainbow&gt; and
        /// &lt;dwv-gradient=#RRGGBB,#RRGGBB&gt; tags into Unity color tags.
        /// Supports multiple and nested effects; malformed tags remain unchanged.
        /// </summary>
        public static string ApplyColorTags(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            while (true)
            {
                string result = ColorTags.Replace(input, match =>
                {
                    bool gradient = match.Groups["effect"].Value.Equals("gradient", StringComparison.OrdinalIgnoreCase);
                    bool hasColors = match.Groups["start"].Success;
                    if (gradient != hasColors) return match.Value;
                    string text = match.Groups["text"].Value;
                    return gradient
                        ? GradientUnityRichText(text, match.Groups["start"].Value, match.Groups["end"].Value)
                        : RainbowUnityRichText(text);
                });
                if (result == input) return result;
                input = result;
            }
        }

        /// <summary>
        /// Creates Unity rich text with two colors in RRGGBB or #RRGGBB format.
        /// Holds each endpoint color for approximately a quarter of the colorable
        /// text, blending per text element through the middle half. Whitespace
        /// does not advance the gradient when skipSpaces is true.
        /// </summary>
        public static string GradientUnityRichText(string input, string startColor, string endColor, bool skipSpaces = true)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var start = ParseHexColor(startColor, nameof(startColor));
            var end = ParseHexColor(endColor, nameof(endColor));
            return ColorUnityRichText(input, skipSpaces, (colorIndex, colorableCount) =>
            {
                // Keep symmetric solid ends, including a single endpoint on short text.
                int solidCount = Math.Max(1, colorableCount / 4);
                int blendSteps = colorableCount - 2 * solidCount + 1;
                double t = colorableCount <= 1 ? 0.0
                    : Math.Clamp((double)(colorIndex - solidCount + 1) / blendSteps, 0.0, 1.0);
                int r = (int)Math.Round(start.r + (end.r - start.r) * t);
                int g = (int)Math.Round(start.g + (end.g - start.g) * t);
                int b = (int)Math.Round(start.b + (end.b - start.b) * t);
                return r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
            });
        }

        private static (int r, int g, int b) ParseHexColor(string color, string parameterName)
        {
            if (color is null) throw new ArgumentNullException(parameterName);
            string hex = color.StartsWith('#') ? color[1..] : color;
            if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture, out int rgb))
            {
                throw new ArgumentException("Color must be RRGGBB or #RRGGBB hexadecimal.", parameterName);
            }

            return ((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        }

        /// <summary>
        /// Create rainbow text using Unity rich text color tags: <color=#RRGGBB>char</color>
        /// </summary>
        public static string RainbowUnityRichText(string input, bool skipSpaces = true)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return ColorUnityRichText(input, skipSpaces, (index, count) =>
                ColorFromHueToHex(count <= 1 ? 0.0 : (double)index / (count - 1)));
        }

        // Count visible Unicode text elements, preserving markup and explicit inner colors.
        private static string ColorUnityRichText(string input, bool skipSpaces, Func<int, int, string> colorAt)
        {
            var elements = new List<(string text, bool markup)>();
            int count = 0;
            foreach (Match token in Regex.Matches(input, @"<[^>]*>|[^<]+|<"))
            {
                if (token.Value.StartsWith('<') && token.Value.EndsWith('>'))
                {
                    elements.Add((token.Value, true));
                    continue;
                }
                var enumerator = StringInfo.GetTextElementEnumerator(token.Value);
                while (enumerator.MoveNext())
                {
                    string text = enumerator.GetTextElement();
                    elements.Add((text, false));
                    if (!skipSpaces || !string.IsNullOrWhiteSpace(text)) count++;
                }
            }

            var sb = new StringBuilder();
            int index = 0;
            int colorDepth = 0;
            foreach (var (text, markup) in elements)
            {
                if (markup)
                {
                    if (text.StartsWith("<color=", StringComparison.OrdinalIgnoreCase)) colorDepth++;
                    else if (text.Equals("</color>", StringComparison.OrdinalIgnoreCase)) colorDepth = Math.Max(0, colorDepth - 1);
                    sb.Append(text);
                }
                else if (skipSpaces && string.IsNullOrWhiteSpace(text)) sb.Append(text);
                else
                {
                    if (colorDepth > 0) sb.Append(text);
                    else sb.Append("<color=#").Append(colorAt(index, count)).Append('>').Append(text).Append("</color>");
                    index++;
                }
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
                if (skipSpaces && char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                    continue;
                }

                double t = lengthForGradient == 1 ? 0.0 : (double)colorIndex / (lengthForGradient - 1);
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
            foreach (var ch in s) if (!char.IsWhiteSpace(ch)) c++;
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
            double hh = h / 60.0 % 6.0;
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

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
