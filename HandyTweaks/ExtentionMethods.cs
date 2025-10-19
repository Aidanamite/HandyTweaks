using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Net;
using UnityEngine;
using System.Runtime.InteropServices;
using Object = UnityEngine.Object;

namespace HandyTweaks
{
    static class ExtentionMethods
    {
        public static bool TryGetAttributeField(this string att, out string fieldName)
        {
            if (att != null && AvAvatarController.mModifierFieldMap != null)
                return AvAvatarController.mModifierFieldMap.TryGetValue(att, out fieldName);
            fieldName = null;
            return false;
        }

        public static string ReadContent(this WebResponse response, Encoding encoding = null)
        {
            using (var stream = response.GetResponseStream())
            {
                var b = new byte[stream.Length];
                stream.Read(b, 0, b.Length);
                return (encoding ?? Encoding.UTF8).GetString(b);
            }
        }
        public static string GetJsonEntry(this WebResponse response, string key, Encoding encoding = null)
        {
            using (var stream = response.GetResponseStream())
            {
                var reader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(stream, new System.Xml.XmlDictionaryReaderQuotas() {  });
                while (reader.Name != key && reader.Read())
                { }
                if (reader.Name == key && reader.Read())
                    return reader.Value;
                return null;
            }
        }

        public static bool IsRankLocked(this ItemData data, out int rid, int rankType)
        {
            rid = 0;
            if (data.RewardTypeID > 0)
                rankType = data.RewardTypeID;
            if (data.Points != null && data.Points.Value > 0)
            {
                rid = data.Points.Value;
                UserAchievementInfo userAchievementInfoByType = UserRankData.GetUserAchievementInfoByType(rankType);
                return userAchievementInfoByType == null || userAchievementInfoByType.AchievementPointTotal == null || rid > userAchievementInfoByType.AchievementPointTotal.Value;
            }
            if (data.RankId != null && data.RankId.Value > 0)
            {
                rid = data.RankId.Value;
                UserRank userRank = (rankType == 8) ? PetRankData.GetUserRank(SanctuaryManager.pCurPetData) : UserRankData.GetUserRankByType(rankType);
                return userRank == null || rid > userRank.RankID;
            }
            return false;
        }

        public static bool HasPrereqItem(this ItemData data)
        {
            if (data.Relationship == null)
                return true;
            ItemDataRelationship[] relationship = data.Relationship;
            foreach (var itemDataRelationship in data.Relationship)
                if (itemDataRelationship.Type == "Prereq")
                    return (ParentData.pIsReady && ParentData.pInstance.HasItem(itemDataRelationship.ItemId)) || CommonInventoryData.pInstance.FindItem(itemDataRelationship.ItemId) != null;
            return true;
        }
        public static Dictionary<string, Color> colorPresets = typeof(Color).GetProperties().Where(x => x.PropertyType == x.DeclaringType && x.GetGetMethod(false)?.IsStatic == true).ToDictionary(x => x.Name.ToLowerInvariant(),x => (Color)x.GetValue(null));
        public static bool TryParseColor(this string clr,out Color color)
        {
            clr = clr.ToLowerInvariant();
            color = default;
            if (colorPresets.TryGetValue(clr,out var v))
                color = v;
            else if (uint.TryParse(clr,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out var n))
                color = new Color32((byte)(n / 0x10000 & 0xFF), (byte)(n / 0x100 & 0xFF), (byte)(n & 0xFF), 255);
            else
                return false;
            return true;
        }
        public static string ToHex(this Color32 color) => color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToHex(this Color color) => ((Color32)color).ToHex();
        public static Color Shift(this Color oc, Color nc)
        {
            var s = Math.Max(Math.Max(oc[0], oc[1]), oc[2]);
            return new Color(nc.r * s, nc.g * s, nc.b * s, nc.a * oc.a);
        }
        public static ParticleSystem.MinMaxGradient Shift(this ParticleSystem.MinMaxGradient o, Color newColor)
        {
            if (o.mode == ParticleSystemGradientMode.Color)
                return new ParticleSystem.MinMaxGradient(o.color.Shift(newColor));

            if (o.mode == ParticleSystemGradientMode.Gradient)
                return new ParticleSystem.MinMaxGradient(new Gradient()
                {
                    mode = o.gradient.mode,
                    alphaKeys = o.gradient.alphaKeys,
                    colorKeys = o.gradient.colorKeys.Select(x => new GradientColorKey(x.color.Shift(newColor), x.time)).ToArray()
                });

            if (o.mode == ParticleSystemGradientMode.TwoColors)
                return new ParticleSystem.MinMaxGradient(o.colorMin.Shift(newColor), o.colorMax.Shift(newColor));

            if (o.mode == ParticleSystemGradientMode.TwoGradients)
                return new ParticleSystem.MinMaxGradient(new Gradient()
                {
                    mode = o.gradientMin.mode,
                    alphaKeys = o.gradientMin.alphaKeys,
                    colorKeys = o.gradientMin.colorKeys.Select(x => new GradientColorKey(x.color.Shift(newColor), x.time)).ToArray()
                }, new Gradient()
                {
                    mode = o.gradientMax.mode,
                    alphaKeys = o.gradientMax.alphaKeys,
                    colorKeys = o.gradientMax.colorKeys.Select(x => new GradientColorKey(x.color.Shift(newColor), x.time)).ToArray()
                });

            return o;
        }
        public static List<T> GetRandom<T>(this ICollection<T> c, int count)
        {
            var r = new System.Random();
            var n = c.Count;
            if (count >= n)
                return c.ToList();
            var l = new List<T>(count);
            if (count > 0)
                foreach (var i in c)
                    if (r.Next(n--) < count)
                    {
                        count--;
                        l.Add(i);
                        if (count == 0)
                            break;
                    }
            return l;
        }
        public static Vector2 Rotate(this Vector2 v, float delta, Vector2 center = default)
        {
            if (center != default)
                v -= center;
            v = new Vector2(
                v.x * Mathf.Cos(delta) - v.y * Mathf.Sin(delta),
                v.x * Mathf.Sin(delta) + v.y * Mathf.Cos(delta)
            );
            if (center != default)
                v += center;
            return v;
        }
        public static bool TryParseColor(this string[] values, out Color result, int start = 0)
        {
            if (values != null
                && values.Length >= start + 3
                && values[start] != null && int.TryParse(values[start], out var r)
                && values[start + 1] != null && int.TryParse(values[start + 1], out var g)
                && values[start + 2] != null && int.TryParse(values[start + 2], out var b))
            {
                result = new Color(r / 255f, g / 255f, b / 255f);
                return true;
            }
            result = default;
            return false;
        }
        public static string JoinValues(this Color c, string delimeter = "$") => (int)Math.Round(c.r * 255.0) + delimeter + (int)Math.Round(c.g * 255.0) + delimeter + (int)Math.Round(c.b * 255.0);
        public static StringBuilder AppendColorDirectHex(this StringBuilder builder, Color color, string delimeter = "$")
            => builder.AppendFormat("{0:X}{3}{1:X}{3}{2:X}", color.r.DirectAs<float, int>(), color.g.DirectAs<float, int>(), color.b.DirectAs<float, int>(), delimeter);
        public static StringBuilder AppendColorDirectHex(this StringBuilder builder, RaisedPetColor color, string delimeter = "$")
            => builder.AppendFormat("{0:X}{3}{1:X}{3}{2:X}", color.Red.DirectAs<float, int>(), color.Green.DirectAs<float, int>(), color.Blue.DirectAs<float, int>(),delimeter);
        public static string DirectHex(this Color color, string delimeter = "$") => new StringBuilder().AppendColorDirectHex(color, delimeter).ToString();
        public static string DirectHex(this RaisedPetColor color, string delimeter = "$") => new StringBuilder().AppendColorDirectHex(color, delimeter).ToString();
        static bool _TryParseColorDirectHex(string[] values, out int r, out int g, out int b, int index)
        {
            if (values != null
                && values.Length >= index + 3
                && values[index] != null && int.TryParse(values[index], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
                && values[index + 1] != null && int.TryParse(values[index + 1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
                && values[index + 2] != null && int.TryParse(values[index + 2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
                return true;
            r = 0;
            g = 0;
            b = 0;
            return false;
        }
        public static bool TryParseColorDirectHex(this string[] values, out Color result, int index = 0)
        {
            if (_TryParseColorDirectHex(values,out var r, out var g, out var b, index))
            {
                result = new Color(r.DirectAs<int,float>(), g.DirectAs<int, float>(), b.DirectAs<int, float>());
                return true;
            }
            result = default;
            return false;
        }
        public static bool TryParsePetColorDirectHex(this string[] values, out RaisedPetColor result, int index = 0)
        {
            if (_TryParseColorDirectHex(values, out var r, out var g, out var b, index))
            {
                result = new RaisedPetColor() { Red = r.DirectAs<int, float>(), Green = g.DirectAs<int, float>(), Blue = b.DirectAs<int, float>() };
                return true;
            }
            result = default;
            return false;
        }
        public static unsafe Y DirectAs<X,Y>(this X value)
        {
            if (sizeof(X) != sizeof(Y))
                throw new InvalidCastException();
            return *(Y*)&value;
        }
        public static T GetOrAddComponent<T>(this GameObject g) where T : Component => g.GetComponent<T>() ?? g.AddComponent<T>();
        public static Y GetValueOrDefault<X, Y>(this IReadOnlyDictionary<X, Y> d, X key) => d.TryGetValue(key, out var value) ? value : default;

        public static T GetSafe<T>(this IList<T> l, int index, T fallback = default)
        {
            if (index < 0 || index > l.Count)
                return fallback;
            return l[index];
        }

        public static bool SoDtoUnityRich(this string str, out string result, Color baseColor, bool tmpro = false, bool forEdit = false)
        {
            var _result = new StringBuilder(str.Length);
            var flag = false;
            var curr = new RichTextState(baseColor);
            var last = new RichTextState(curr);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void AppendColor(Color color)
                => _result.AppendFormat("<color=#{0:X2}{1:X2}{2:X2}{3:X2}>",
                    Math.Min(Math.Max((int)Math.Round(color.r * 255), 0), 255),
                    Math.Min(Math.Max((int)Math.Round(color.g * 255), 0), 255),
                    Math.Min(Math.Max((int)Math.Round(color.b * 255), 0), 255),
                    Math.Min(Math.Max((int)Math.Round(color.a * 255), 0), 255));
            for (int i = 0; i <= str.Length;)
            {
                int pi = i;
                if (i == str.Length || curr.ParseSymbol(str, ref i))
                {
                    if (i == str.Length)
                    {
                        i++;
                        if (flag)
                            curr = new() { ignore = true };
                        else
                            continue;
                    }
                    else if (forEdit)
                        _result.Append(str, pi, i - pi);
                    if ((last.strike != curr.strike) || (last.under != curr.under))
                    { }
                    if (last.sub != curr.sub && tmpro)
                    {
                        if (last.sub != 0)
                            _result.Append(last.sub == 1 ? "</sub>" : "</sup>");
                        if (curr.sub != 0)
                            _result.Append(curr.sub == 1 ? "<sub>" : "<sup>");
                    }
                    if (last.italic != curr.italic)
                        _result.Append(curr.italic ? "<i>" : "</i>");
                    if (last.bold != curr.bold)
                        _result.Append(curr.bold ? "<b>" : "</b>");
                    if (last.ignore != curr.ignore)
                    {
                        if (curr.ignore)
                            for (int j = last.colors.size - 1; j >= 1; j--)
                                _result.Append("</color>");
                        else
                            for (int j = curr.colors.size - 1; j >= 1; j--)
                                AppendColor(curr.colors[j]);
                    }
                    else if (!curr.ignore && last.colors.size != curr.colors.size)
                    {
                        if (last.colors.size < curr.colors.size)
                            AppendColor(curr.colors[curr.colors.size - 1]);
                        else
                            _result.Append("</color>");
                    }
                    last.CopyFrom(curr);
                    flag = true;
                    continue;
                }
                _result.Append(str[i]);
                i++;
            }
            if (flag)
            {
                for (int j = curr.colors.size - 1; j >= 1; j--)
                    _result.Append("</color>");
            }
            //    Debug.Log($"Formatted\n{str}\n----------- to\n{_result}");
            result = flag ? _result.ToString() : str;
            return flag;
        }
        public static string Join<T>(this IEnumerable<T> values, Func<T, string> converter = null, string delimeter = ", ") => new StringBuilder().Join(values, converter, delimeter).ToString();
        public static StringBuilder Join<T>(this StringBuilder builder, IEnumerable<T> values, Func<T, string> converter = null, string delimeter = ", ")
        {
            bool first = false;
            foreach (var v in values)
            {
                if (first)
                    first = false;
                else
                    builder.Append(delimeter);
                if (converter == null)
                    builder.Append(v);
                else
                    builder.Append(converter(v));
            }
            return builder;
        }
        public static Color[] GetPixelsSafe(this Texture2D source)
        {
            if (source.isReadable)
                return source.GetPixels(0);
            RenderTexture temp = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
            Graphics.Blit(source, temp);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = temp;
            Texture2D texture = new Texture2D(source.width, source.height);
            texture.ReadPixels(new Rect(0, 0, temp.width, temp.height), 0, 0);
            texture.Apply();
            var pixels = texture.GetPixels(0);
            Object.DestroyImmediate(texture);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(temp);
            return pixels;
        }
    }
}