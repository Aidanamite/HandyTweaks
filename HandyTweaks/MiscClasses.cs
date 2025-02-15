using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace HandyTweaks
{
    public static class ColorConvert
    {
        public static void ToHSL(this Color c, out float hue, out float saturation, out float luminosity) => ToHSL(c.r, c.g, c.b, out hue, out saturation, out luminosity);
        public static void ToHSL(float R, float G, float B, out float hue, out float saturation, out float luminosity)
        {
            var max = Math.Max(Math.Max(R, G), B);
            var min = Math.Min(Math.Min(R, G), B);
            luminosity = max;
            if (min == max)
            {
                hue = 0;
                saturation = 0;
                return;
            }
            saturation = (max - min) / max;
            if (R == max)
            {
                if (G >= B)
                    hue = (G - min) * H60 / (max - min);
                else
                    hue = H360 - (B - min) * H60 / (max - min);
            }
            else if (G == max)
            {
                if (B >= R)
                    hue = H120 + (B - min) * H60 / (max - min);
                else
                    hue = H120 - (R - min) * H60 / (max - min);
            }
            else
            {
                if (R >= G)
                    hue = H240 + (R - min) * H60 / (max - min);
                else
                    hue = H240 - (G - min) * H60 / (max - min);
            }
        }
        public static Color FromHSL(float hue, float saturation, float luminosity)
        {
            FromHSL(hue, saturation, luminosity, out var R, out var G, out var B);
            return new Color(R, G, B);
        }
        const float H60 = 1f / 6;
        const float H120 = 2f / 6;
        const float H180 = 3f / 6;
        const float H240 = 4f / 6;
        const float H300 = 5f / 6;
        const float H360 = 1;
        public static void FromHSL(float hue, float saturation, float luminosity, out float R, out float G, out float B)
        {
            hue %= 1;
            if (hue < 0)
                hue += 1;
            var max = luminosity;
            if (saturation == 0)
            {
                R = G = B = max;
                return;
            }
            var min = max - (saturation * max);
            if (hue <= H60)
            {
                B = min;
                R = max;
                G = min + hue * (max - min) / H60;
            }
            else if (hue <= H120)
            {
                B = min;
                G = max;
                R = min + (H120 - hue) * (max - min) / H60;
            }
            else if (hue <= H180)
            {
                R = min;
                G = max;
                B = min + (hue - H120) * (max - min) / H60;
            }
            else if (hue <= H240)
            {
                R = min;
                B = max;
                G = min + (H240 - hue) * (max - min) / H60;
            }
            else if (hue <= H300)
            {
                G = min;
                B = max;
                R = min + (hue - H240) * (max - min) / H60;
            }
            else
            {
                G = min;
                R = max;
                B = min + (H360 - hue) * (max - min) / H60;
            }
        }

        public static Color Normalized(this Color c)
        {
            c.ToHSL(out var h, out var s, out var l);
            Normalized(ref h, ref s, ref l);
            return FromHSL(h, s, l);
        }

        public static Color Clamped(this Color c) => new Color(Math.Min(1, Math.Max(0, c.r)), Math.Min(1, Math.Max(0, c.g)), Math.Min(1, Math.Max(0, c.b)), Math.Min(1, Math.Max(0, c.a)));
        public static void Normalized(ref float hue, ref float saturation, ref float luminosity)
        {
            saturation = Math.Min(1, Math.Max(0, saturation));
            luminosity = Math.Min(1, Math.Max(0, luminosity));
        }
    }

    class ComparePetValue : IComparer<(float, RaisedPetData)>
    {
        public int Compare((float, RaisedPetData) a, (float, RaisedPetData) b)
        {
            var c = b.Item1.CompareTo(a.Item1);
            return c == 0 ? 1 : c;
        }
    }

    public class MaterialEdit
    {
        static ConditionalWeakTable<Material, MaterialEdit> data = new ConditionalWeakTable<Material, MaterialEdit>();
        public static MaterialEdit Get(Material material)
        {
            if (data.TryGetValue(material, out var edit)) return edit;
            edit = data.GetOrCreateValue(material);
            if (material.HasProperty("_EmissiveColor"))
            {
                var c = material.GetColor("_EmissiveColor");
                edit.OriginalEmissive = (Math.Max(Math.Max(c.r, c.g), c.b), c.a, c);
            }
            return edit;
        }
        public (float strength, float alpha, Color original) OriginalEmissive;
    }

    public class CustomStatInfo
    {
        public readonly string AttributeName;
        public readonly string DisplayName;
        public readonly string Abreviation;
        public readonly bool Valid;
        public CustomStatInfo(string Att, string Dis, string Abv, bool Val)
        {
            AttributeName = Att;
            DisplayName = Dis;
            Abreviation = Abv;
            Valid = Val;
        }
    }
}
