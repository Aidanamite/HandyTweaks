using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using static HandyTweaks.ReportManager.FrameCapture;
using static System.Reflection.BindingFlags;
using Object = UnityEngine.Object;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.Applications.Events.DataModels;

namespace HandyTweaks
{
    public class ReportManager : MonoBehaviour // THIS IS A WIP CLASS
    {
        Camera cam;
        long lastFrameTime;
        int pos = 0;
        FrameCapture[] rends;
        public void Awake()
        {
            GetSystemTimeAsFileTime(out lastFrameTime);
            cam = GetComponent<Camera>();
        }
        public void Update()
        {
            if (!cam)
            {
                DestroyImmediate(this);
                return;
            }
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.LeftAlt))
            {
                Report.Generate(rends, (pos + rends.Length - 1) % rends.Length);
            }
        }
        public void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            RenderTexture prev = RenderTexture.active;

            Graphics.Blit(src, dest); // REQUIRED!

            GetSystemTimeAsFileTime(out var tick);
            if (tick - lastFrameTime >= 10000000 / Main.MaxRecordingFPS)
            {
                EnsureRendsArray();
                if (pos >= rends.Length)
                    pos = 0;
                var rend = rends[pos];
                rend.delay = tick - lastFrameTime;
                lastFrameTime = tick;
                var height = dest.height;
                var width = dest.width;
                if (Main.PlayerReporterResolutionOverride > 0 && Main.PlayerReporterResolutionOverride < height)
                {
                    width = width * Main.PlayerReporterResolutionOverride / height;
                    height = Main.PlayerReporterResolutionOverride;
                }
                if (!rend.img || rend.img.width != width || rend.img.height != height)
                {
                    if (rend.img)
                        DestroyImmediate(rend.img);
                    rend.img = new RenderTexture(height, width, dest.depth, dest.format, 0);
                }
                Graphics.Blit(dest, rend.img);
                rend.CapturePlayers(cam);
                pos++;
                if (pos >= rends.Length)
                    pos = 0;
            }

            RenderTexture.active = prev;
        }

        void EnsureRendsArray()
        {
            var expectedLength = (int)(Main.MaxRecordingLength * Main.MaxRecordingFPS);
            if (rends != null && expectedLength == rends.Length)
                return;
            var nRends = new FrameCapture[expectedLength];
            if (rends == null)
            {
                for (int i = 0; i < expectedLength; i++)
                    nRends[i] = new FrameCapture();
                pos = 0;
                rends = nRends;
                return;
            }
            var copied = Math.Min(rends.Length - (pos % rends.Length),nRends.Length);
            Array.Copy(rends, pos % rends.Length, nRends, 0, copied);
            if (copied < rends.Length && copied < nRends.Length)
            {
                var rem = Math.Min(rends.Length - copied, nRends.Length - copied);
                Array.Copy(rends, 0, nRends, copied, rem);
                copied += rem;
            }
            for (; copied < nRends.Length; copied++)
                nRends[copied] = new FrameCapture();
            rends = nRends;
            pos = 0;
        }


        [DllImport("kernel32")]
        static extern void GetSystemTimeAsFileTime(out long value);

        public sealed class FrameCapture
        {
            public RenderTexture img;
            public long delay;
            public List<PlayerCapture> players = new List<PlayerCapture>();

            public void CapturePlayers(Camera cam)
            {
                if (players.Count != 0)
                    players.Clear();
                if (!MainStreetMMOClient.pInstance || MainStreetMMOClient.pInstance.pPlayerList == null || MainStreetMMOClient.pInstance.pPlayerList.Count == 0)
                    return;
                var view = GeometryUtility.CalculateFrustumPlanes(cam);
                foreach (var player in MainStreetMMOClient.pInstance.pPlayerList.Values)
                {
                    var capture = new PlayerCapture();
                    capture.userId = player.pUserID;
                    capture.username = player.mUserName;
                    capture.position = player.pController.transform.position;
                    capture.bounds = player.pController.mProjCollider.bounds;
                    foreach (var renderer in player.pController.GetComponentsInChildren<Renderer>())
                        capture.bounds.Encapsulate(renderer.bounds);
                    bool hasPet = player.pSanctuaryPet;
                    if (hasPet)
                    {
                        capture.petData = player.mRaisedPetDataString;
                        capture.petPosition = player.mSanctuaryPet.transform.position;
                        capture.petBounds = player.pSanctuaryPet.pClickCollider.bounds;
                        foreach (var renderer in player.pSanctuaryPet.GetComponentsInChildren<Renderer>())
                            capture.petBounds.Encapsulate(renderer.bounds);
                    }
                    if (GeometryUtility.TestPlanesAABB(view,capture.bounds) || (hasPet && GeometryUtility.TestPlanesAABB(view, capture.petBounds)))
                        players.Add(capture);
                }
            }

            public struct PlayerCapture
            {
                public string userId;
                public string username;
                public Vector3 position;
                public Bounds bounds;
                public string petData;
                public Vector3 petPosition;
                public Bounds petBounds;
            }
        }
    }

    public static class Report
    {
        public static byte[] Generate(ReportManager.FrameCapture[] frames, int readPosition)
        {
            var t = frames[readPosition].img;
            var height = t.height;
            var width = t.width;
            RenderTexture prev = RenderTexture.active;
            var count = 1;
            do
            {
                readPosition--;
                if (readPosition < 0)
                    readPosition += frames.Length;
                t = frames[readPosition].img;
                if (t.height != height || t.width != width)
                    break;
                count++;
            }
            while (count < frames.Length);
            readPosition = (readPosition + frames.Length - count + 1) % frames.Length;
            var hFramesPerImage = 16384 / width;
            var vFramesPerImage = 16384 / height;
            var framesPerImage = vFramesPerImage * hFramesPerImage; // 16384 is max image height and width in unity
            var texs = new Texture2D[(int)Math.Ceiling(count / (double)framesPerImage)];
            var result = new _File() { FrameInfo = new FrameInfo[count], Frames = new byte[texs.Length][] };
            for (int ind = 0; ind < count; ind++)
            {
                var texture = texs[ind / framesPerImage];
                if (!texture)
                {
                    if ((ind / framesPerImage) < texs.Length - 1)
                        texture = new Texture2D(width * hFramesPerImage, height * vFramesPerImage, TextureFormat.ARGB32, false, true);
                    else
                    {
                        FindBestFactorPair(count - (texs.Length - 1) * framesPerImage, hFramesPerImage, vFramesPerImage, out var iw, out var ih);
                        texture = new Texture2D(width * iw, height * ih, TextureFormat.ARGB32, false, true);
                    }
                    texs[ind / framesPerImage] = texture;
                }
                var iind = ind % framesPerImage;
                var frame = frames[readPosition];
                result.FrameInfo[ind].delay = frame.delay;
                result.FrameInfo[ind].players = frame.players.ToArray();
                RenderTexture.active = frame.img;
                texture.ReadPixels(new Rect(0, 0, width, height), (iind * width) % texture.width, ((iind * width) / texture.width) * height, false);
                readPosition = (readPosition + 1) % frames.Length;
            }
            for (int i = 0; i < texs.Length; i++)
            { 
                result.Frames[i] = texs[i].EncodeToJPG();
                Object.DestroyImmediate(texs[i]);
            }
            RenderTexture.active = prev;

            using (var mem = new MemoryStream()) {
                using (var writer = new BinaryWriter(mem))
                    writer.Write(result);
                return mem.ToArray();
            }
        }

        static void FindBestFactorPair(int count, int maxWidth, int maxHeight, out int width, out int height)
        {
            if (count <= maxWidth)
            {
                width = count;
                height = 1;
                return;
            }
            var best = 0;
            var bestRem = count;
            for (var i = maxWidth; i > 1; i--)
            {
                if (count / i > maxHeight)
                    break;
                var rem = count % i;
                if (rem == 0)
                {
                    best = i;
                    break;
                }
                if (rem < bestRem)
                {
                    best = i;
                    bestRem = rem;
                }
            }
            width = best;
            height = count / best;
        }

        struct _File
        {
            public FrameInfo[] FrameInfo;
            public byte[][] Frames;
        }
        
        struct FrameInfo
        {
            public long delay;
            public PlayerCapture[] players;
        }

        static void Write(this BinaryWriter writer, object graph)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            if (graph is bool bl)
                writer.Write(bl);
            else if (graph is byte b)
                writer.Write(b);
            else if (graph is sbyte sb)
                writer.Write(sb);
            else if (graph is short s)
                writer.Write(s);
            else if (graph is ushort us)
                writer.Write(us);
            else if (graph is int i)
                writer.Write(i);
            else if (graph is uint ui)
                writer.Write(ui);
            else if (graph is long l)
                writer.Write(l);
            else if (graph is ulong ul)
                writer.Write(ul);
            else if (graph is float f)
                writer.Write(f);
            else if (graph is double d)
                writer.Write(d);
            else if (graph is decimal d2)
                writer.Write(d2);
            else if (graph is char c)
                writer.Write(c);
            else if (graph is string str)
                writer.Write(str);
            else if (graph.GetType().GetCollectionType(out var colType))
            {
                var col = (ICollection)graph;
                writer.Write(col.Count);
                foreach (var item in col)
                    if (item == null)
                        throw new ArgumentNullException(nameof(graph) + "[]");
                    else if (item.GetType() == colType)
                        writer.Write(item);
                    else
                        throw new ArgumentException("Cannot write item of collection where it is different from collection content type", nameof(graph) + "[]");
            }
            else if (!graph.GetType().IsValueType)
                throw new ArgumentException($"Cannot write reference type \"{graph.GetType().FullName}\".", nameof(graph));
            var fl = new List<(string, object)>();
            foreach (var field in graph.GetType().GetFields(Instance | Public | NonPublic))
            {
                var value = field.GetValue(graph);
                if (value != null)
                {
                    if (value.GetType() != field.FieldType)
                        throw new ArgumentException("Cannot write value of field where it is different from field type",nameof(value) + "." + field.Name);
                    fl.Add((field.Name,value));
                }
            }
            writer.Write(fl.Count);
            foreach (var i in fl)
            {
                writer.Write(i.Item1);
                writer.Write(i.Item2);
            }
        }
        static object Read(this BinaryReader reader, Type type)
        {
            if (type == typeof(bool))
                return reader.ReadBoolean();
            if (type == typeof(byte))
                return reader.ReadByte();
            if (type == typeof(sbyte))
                return reader.ReadSByte();
            if (type == typeof(short))
                return reader.ReadInt16();
            if (type == typeof(ushort))
                return reader.ReadUInt16();
            if (type == typeof(int))
                return reader.ReadInt32();
            if (type == typeof(uint))
                return reader.ReadUInt32();
            if (type == typeof(long))
                return reader.ReadInt64();
            if (type == typeof(ulong))
                return reader.ReadUInt64();
            if (type == typeof(float))
                return reader.ReadSingle();
            if (type == typeof(double))
                return reader.ReadDouble();
            if (type == typeof(decimal))
                return reader.ReadDecimal();
            if (type == typeof(char))
                return reader.ReadChar();
            if (type == typeof(string))
                return reader.ReadString();
            if (type.GetCollectionType(out var colType))
            {
                var length = reader.ReadInt32();
                ConstructorInfo con = null;
                foreach (var c in type.GetConstructors(~Default))
                {
                    var paras = c.GetParameters();
                    if (paras.Length == 0)
                        con = c;
                    else if (paras[0].ParameterType == typeof(int) && paras.Skip(1).All(x => x.HasDefaultValue))
                    {
                        con = c;
                        break;
                    }
                    else if (con == null && paras.All(x => x.HasDefaultValue))
                        con = c;
                }
                ICollection obj;
                if (con == null)
                    obj = (ICollection)FormatterServices.GetUninitializedObject(type);
                else
                {
                    var paras = con.GetParameters();
                    if (paras.Length == 0)
                        obj = (ICollection)con.Invoke([]);
                    else if (paras[0].ParameterType == typeof(int))
                        obj = (ICollection)con.Invoke([.. (new object[] { length }), .. paras.Skip(1).Select(x => x.DefaultValue)]);
                    else
                        obj = (ICollection)con.Invoke(paras.Select(x => x.DefaultValue).ToArray());
                }
                if (obj is IList l)
                {
                    if (obj.Count == length)
                        for (int i = 0; i < length; i++)
                            l[i] = reader.Read(colType);
                    else
                        for (int i = 0; i < length; i++)
                            l.Add(reader.Read(colType));
                }
                else
                {
                    var add = type.GetMethods(Instance | Public | NonPublic)
                        .First(x => x.Name == "Add" && x.ReturnType == typeof(void) && x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType == colType);
                    for (int i = 0; i < length; i++)
                        add.Invoke(obj, [reader.Read(colType)]);
                }
                return obj;
            }
            else
            {
                var fcount = reader.ReadInt32();
                ConstructorInfo con = null;
                foreach (var c in type.GetConstructors(~Default))
                {
                    var paras = c.GetParameters();
                    if (paras.Length == 0)
                        con = c;
                    else if (paras[0].ParameterType == typeof(int) && paras.Skip(1).All(x => x.HasDefaultValue))
                    {
                        con = c;
                        break;
                    }
                    else if (con == null && paras.All(x => x.HasDefaultValue))
                        con = c;
                }
                ICollection obj;
                if (con == null)
                    obj = (ICollection)FormatterServices.GetUninitializedObject(type);
                else
                    obj = null; // TODO: Finish constructor invoke and populate new object
                return obj;
            }
        }
        static bool GetCollectionType(this Type type, out Type collectionType)
        {
            foreach (var i in type.GetInterfaces())
                if (i.IsConstructedGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))
                {
                    collectionType = i.GetGenericArguments()[0];
                    return true;
                }
            collectionType = null;
            return false;
        }
    }
}
