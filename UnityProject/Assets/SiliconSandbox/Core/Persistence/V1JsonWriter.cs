using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SiliconSandbox.Persistence
{
    // Small dependency-free emitter for the fixed V1 record shapes. The
    // semantic codec decides every field; this class only guarantees JSON
    // punctuation, invariant numbers, and valid UTF-8 escaping.
    internal sealed class V1JsonWriter
    {
        private sealed class Frame
        {
            public char Kind;
            public bool HasValue;
            public bool ExpectsValue;
        }

        private readonly StringBuilder output = new StringBuilder();
        private readonly Stack<Frame> frames = new Stack<Frame>();
        private bool rootWritten;

        public void BeginObject() { BeforeValue(); output.Append('{'); frames.Push(new Frame { Kind = 'o' }); }
        public void EndObject()
        {
            if (frames.Count == 0 || frames.Peek().Kind != 'o' ||
                frames.Peek().ExpectsValue)
                throw new InvalidOperationException("JSON object is incomplete.");
            frames.Pop(); output.Append('}');
        }
        public void BeginArray() { BeforeValue(); output.Append('['); frames.Push(new Frame { Kind = 'a' }); }
        public void EndArray()
        {
            if (frames.Count == 0 || frames.Peek().Kind != 'a')
                throw new InvalidOperationException("JSON array is incomplete.");
            frames.Pop(); output.Append(']');
        }
        public void Name(string name)
        {
            if (frames.Count == 0 || frames.Peek().Kind != 'o' ||
                frames.Peek().ExpectsValue)
                throw new InvalidOperationException("JSON field is out of place.");
            var frame = frames.Peek();
            if (frame.HasValue) output.Append(',');
            Quoted(name);
            output.Append(':');
            frame.ExpectsValue = true;
        }
        public void String(string value)
        { BeforeValue(); Quoted(value); }
        public void Integer(long value)
        { BeforeValue(); output.Append(value.ToString(CultureInfo.InvariantCulture)); }
        public void Real(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            BeforeValue();
            output.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }
        public void Boolean(bool value)
        { BeforeValue(); output.Append(value ? "true" : "false"); }
        public void Null()
        { BeforeValue(); output.Append("null"); }
        public byte[] ToUtf8()
        {
            if (!rootWritten || frames.Count != 0)
                throw new InvalidOperationException("JSON document is incomplete.");
            return new UTF8Encoding(false, true).GetBytes(output.ToString());
        }

        private void BeforeValue()
        {
            if (frames.Count == 0)
            {
                if (rootWritten) throw new InvalidOperationException("Extra JSON root.");
                rootWritten = true;
                return;
            }
            var frame = frames.Peek();
            if (frame.Kind == 'a')
            {
                if (frame.HasValue) output.Append(',');
                frame.HasValue = true;
            }
            else
            {
                if (!frame.ExpectsValue)
                    throw new InvalidOperationException("JSON field name is missing.");
                frame.ExpectsValue = false;
                frame.HasValue = true;
            }
        }

        private void Quoted(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            output.Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\b': output.Append("\\b"); break;
                    case '\f': output.Append("\\f"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                        {
                            output.Append("\\u");
                            output.Append(((int)character).ToString("x4",
                                CultureInfo.InvariantCulture));
                        }
                        else output.Append(character);
                        break;
                }
            }
            output.Append('"');
        }
    }
}
