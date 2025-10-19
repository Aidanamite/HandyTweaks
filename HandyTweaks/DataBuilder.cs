using System;
using System.Collections.Generic;
using static HandyTweaks.DataBuilder;

namespace HandyTweaks
{
    public class DataBuilder
    {
        List<Writer> data = new List<Writer>();
        int size;
        public DataBuilder Append<T>(T value) where T : struct
        {
            var writer = new Writer<T>(value);
            size += writer.Size;
            data.Add(writer);
            return this;
        }
        public static void Append<T>(DataBuilder instance, T value) where T : struct => instance.Append(value);

        public DataBuilder AppendArray<T>(T[] value, int rotateStart = 0) where T : struct
        {
            unsafe
            {
                return AppendArray(value, ElementAppender.Create<T>(&Append), rotateStart);
            }
        }
        public static void AppendArray<T>(DataBuilder instance, T[] value) where T : struct => instance.AppendArray(value);

        public DataBuilder AppendList<T>(IList<T> value) where T : struct
        {
            unsafe
            {
                return AppendList(value, ElementAppender.Create<T>(&Append));
            }
        }
        public static void AppendList<T>(DataBuilder instance, IList<T> value) where T : struct => instance.AppendList(value);

        public DataBuilder AppendArray<T>(T[] value, ElementAppender<T> appender, int rotateStart = 0)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            Append(value.Length);
            unsafe
            {
                fixed (T* start = value)
                {
                    rotateStart = rotateStart % value.Length;
                    var rotatedStart = start + rotateStart;
                    if (rotateStart < 0)
                        rotatedStart += value.Length;
                    var pos = rotatedStart;
                    var end = start + value.Length;
                    while (pos != end)
                    {
                        appender.Append(this, *pos);
                        pos++;
                    }
                    if (rotateStart != 0)
                    {
                        pos = start;
                        end = rotatedStart;
                        while (pos != end)
                        {
                            appender.Append(this, *pos);
                            pos++;
                        }
                    }
                }
            }
            return this;
        }
        public DataBuilder AppendList<T>(IList<T> value, ElementAppender<T> appender)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            Append(value.Count);
            foreach (var item in value)
                appender.Append(this, item);
            return this;
        }

        public byte[] ToArray()
        {
            var result = new byte[size];
            unsafe
            {
                fixed (byte* c = result)
                {
                    var pointer = c;
                    foreach (var writer in data)
                        writer.Write(ref pointer);
                }
            }
            return result;
        }


        abstract class Writer
        {
            public readonly int Size;
            public Writer(int size) => Size = size;
            public abstract unsafe void Write(ref byte* pointer);
        }
        class Writer<T> : Writer where T : struct
        {
            public readonly T Value;
            public unsafe Writer(T value) : base(sizeof(T)) => Value = value;
            public override unsafe void Write(ref byte* pointer)
            {
                *(T*)pointer = Value;
                pointer += Size;
            }
        }
    }

    public abstract class ElementAppender
    {
        public static unsafe ElementAppender<T> Create<T>(delegate*<DataBuilder, T, DataBuilder> action) => new PointerAppender<T>() { action = action };
        public static unsafe ElementAppender<T> Create<T>(delegate*<DataBuilder, T, void> action) => new PointerAppender2<T>() { action = action };
        public static ElementAppender<T> Create<T>(Action<DataBuilder, T> action) => new ActionAppender<T>() { action = action };
        public static ElementAppender<T> Create<T>(Action<T> action) => new ActionAppender2<T>() { action = action };

        protected class PointerAppender<T> : ElementAppender<T> { public unsafe delegate*<DataBuilder, T, DataBuilder> action; public override unsafe void Append(DataBuilder builder, T value) => action(builder, value); }
        protected class PointerAppender2<T> : ElementAppender<T> { public unsafe delegate*<DataBuilder, T, void> action; public override unsafe void Append(DataBuilder builder, T value) => action(builder, value); }
        protected class ActionAppender<T> : ElementAppender<T> { public Action<DataBuilder, T> action; public override void Append(DataBuilder builder, T value) => action(builder, value); }
        protected class ActionAppender2<T> : ElementAppender<T> { public Action<T> action; public override void Append(DataBuilder builder, T value) => action(value); }
    }
    public abstract class ElementAppender<T> : ElementAppender
    {
        public abstract void Append(DataBuilder builder, T value);

        public static unsafe implicit operator ElementAppender<T>(delegate*<DataBuilder, T, DataBuilder> action) => new PointerAppender<T>() { action = action };
        public static unsafe implicit operator ElementAppender<T>(delegate*<DataBuilder, T, void> action) => new PointerAppender2<T>() { action = action };
        public static implicit operator ElementAppender<T>(Action<DataBuilder, T> action) => new ActionAppender<T>() { action = action };
        public static implicit operator ElementAppender<T>(Action<T> action) => new ActionAppender2<T>() { action = action };
    }
}
