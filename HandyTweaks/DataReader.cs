using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace HandyTweaks
{
    public class DataReader
    {
        byte[] data;
        long pos;

        public DataReader(byte[] Data)
        {
            data = Data;
        }
        public T ReadOrDefault<T>(T fallback = default) where T : struct => TryRead<T>(out var val) ? val : fallback;
        public bool TryRead<T>(out T result) where T : struct
        {
            if (pos >= data.Length)
            {
                result = default;
                return false;
            }
            unsafe
            {
                var size = sizeof(T);
                if (pos + size <= data.Length)
                {
                    fixed (byte* p = data)
                        result = *(T*)(p + pos);
                    pos += size;
                    return true;
                }
                pos += size;
                result = default;
                return false;
            }
        }
        public static bool TryRead<T>(DataReader reader, out T result) where T : struct => reader.TryRead(out result);

        public bool TryReadArray<T>(out T[] result, bool allowPartial = false, T itemFallback = default) where T : struct
        { unsafe { return TryReadArray(out result, ElementReader.Create<T>(&TryRead), allowPartial, itemFallback); } }

        public bool TryReadList<X, Y>(out X result, bool allowPartial = false, Y itemFallback = default) where Y : struct where X : IList<Y>, new()
        { unsafe { return TryReadList(out result, ElementReader.Create<Y>(&TryRead), allowPartial, itemFallback); } }

        public bool TryReadArray<T>(out T[] result, ElementReader<T> reader, bool allowPartial = false, T itemFallback = default)
        {
            result = null;
            if (!TryRead(out int length))
                return false;
            var a = new T[length];
            for (int i = 0; i < length; i++)
                if (!reader.TryRead(this,out a[i]))
                {
                    if (allowPartial)
                        a[i] = itemFallback;
                    else
                        return false;
                }
            result = a;
            return true;
        }
        public bool TryReadList<X, Y>(out X result, ElementReader<Y> reader, bool allowPartial = false, Y itemFallback = default) where X : IList<Y>, new()
        {
            result = default;
            if (!TryRead(out int length))
                return false;
            var a = new X();
            for (int i = 0; i < length; i++)
                if (reader.TryRead(this,out Y val))
                    a.Add(val);
                else
                {
                    if (allowPartial)
                        a.Add(itemFallback);
                    else
                        return false;
                }
            result = a;
            return true;
        }
        public void Seek(long position,SeekOrigin origin)
        {
            if (origin == SeekOrigin.End)
                pos = data.Length - position;
            else if (origin == SeekOrigin.Current)
                pos += position;
            else
                pos = position;
        }
    }
    public static class ElementReader
    {
        public static unsafe ElementReader<T> Create<T>(delegate*<DataReader, out T, bool> action) => new PointerReader<T>() { action = action };
        public static unsafe ElementReader<T> Create<T>(delegate*<DataReader, T> action) => new PointerReader2<T>() { action = action };
        public static ElementReader<T> Create<T>(TryReadAction<T> action) => new ActionReader<T>() { action = action };
        public static ElementReader<T> Create<T>(TryReadAction2<T> action) => new ActionReader2<T>() { action = action };
        public static ElementReader<T> Create<T>(Func<DataReader, T> action) => new ActionReader3<T>() { action = action };
        public static ElementReader<T> Create<T>(Func<T> action) => new ActionReader4<T>() { action = action };

        class PointerReader<T> : ElementReader<T> { public unsafe delegate*<DataReader, out T, bool> action; public override unsafe bool TryRead(DataReader reader, out T value) => action(reader, out value); }
        class PointerReader2<T> : ElementReader<T> { public unsafe delegate*<DataReader, T> action; public override unsafe bool TryRead(DataReader reader, out T value) { value = action(reader); return true; } }
        class ActionReader<T> : ElementReader<T> { public TryReadAction<T> action; public override bool TryRead(DataReader reader, out T value) => action(reader, out value); }
        class ActionReader2<T> : ElementReader<T> { public TryReadAction2<T> action; public override bool TryRead(DataReader reader, out T value) => action(out value); }
        class ActionReader3<T> : ElementReader<T> { public Func<DataReader, T> action; public override bool TryRead(DataReader reader, out T value) { value = action(reader); return true; } }
        class ActionReader4<T> : ElementReader<T> { public Func<T> action; public override bool TryRead(DataReader reader, out T value) { value = action(); return true; } }

        public delegate bool TryReadAction<T>(DataReader reader, out T value);
        public delegate bool TryReadAction2<T>(out T value);
    }
    public abstract class ElementReader<T>
    {
        public abstract bool TryRead(DataReader reader, out T value);
    }
}
