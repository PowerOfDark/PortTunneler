using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PortTunneler.Common
{
    public class BufferMemoryStream : MemoryStream
    {
        private readonly byte[] _buffer;
        public BufferMemoryStream(byte[] buffer) : base(buffer)
        {
            _buffer = buffer;
            SetLength(buffer.Length);
            Position = 0;
        }


        public void Write(bool value)
        {
            WriteByte(value ? (byte)1 : (byte)0);
        }

        public void Write(byte value)
        {
            WriteByte(value);
        }

        public void Write(sbyte value)
        {
            WriteByte((byte)value);
        }

        public void Write(short value)
        {
            var p = Position;
            _buffer[p++] = (byte)value;
            _buffer[p++] = (byte)(value >> 8);
            Position = p;
        }

        public void Write(ushort value)
        {
            var p = Position;
            _buffer[p++] = (byte)value;
            _buffer[p++] = (byte)(value >> 8);
            Position = p;
        }

        public void Write(int value)
        {
            var p = Position;
            _buffer[p++] = (byte)value;
            _buffer[p++] = (byte)(value >> 8);
            _buffer[p++] = (byte)(value >> 16);
            _buffer[p++] = (byte)(value >> 24);
            Position = p;
        }

        public void Write(uint value)
        {
            var p = Position;
            _buffer[p++] = (byte)value;
            _buffer[p++] = (byte)(value >> 8);
            _buffer[p++] = (byte)(value >> 16);
            _buffer[p++] = (byte)(value >> 24);
            Position = p;
        }

        public void Write(long value)
        {
            var p = Position;
            _buffer[p++] = (byte)value;
            _buffer[p++] = (byte)(value >> 8);
            _buffer[p++] = (byte)(value >> 16);
            _buffer[p++] = (byte)(value >> 24);
            _buffer[p++] = (byte)(value >> 32);
            _buffer[p++] = (byte)(value >> 40);
            _buffer[p++] = (byte)(value >> 48);
            _buffer[p++] = (byte)(value >> 56);
            Position = p;
        }

        public void Write(ulong value)
        {
            var p = Position;
            _buffer[p++] = (byte)value;
            _buffer[p++] = (byte)(value >> 8);
            _buffer[p++] = (byte)(value >> 16);
            _buffer[p++] = (byte)(value >> 24);
            _buffer[p++] = (byte)(value >> 32);
            _buffer[p++] = (byte)(value >> 40);
            _buffer[p++] = (byte)(value >> 48);
            _buffer[p++] = (byte)(value >> 56);
            Position = p;
        }

        public void Write7BitInt(int value)
        {
            uint v = (uint)value;   // support negative numbers
            while (v >= 0x80)
            {
                Write((byte)(v | 0x80));
                v >>= 7;
            }
            Write((byte)v);
        }

        public int Read7BitInt()
        {
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            int count = 0;
            int shift = 0;
            int rb;
            byte b;
            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                    throw new FormatException();

                // ReadByte handles end of stream cases for us.
                rb = ReadByte();
                if (rb == -1)
                    throw new EndOfStreamException();
                b = (byte)rb;
                count |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return count;
        }

        public bool ReadBoolean()
        {
            return _buffer[Position++] != 0;
        }

        public short ReadInt16()
        {
            var p = Position;
            Position += 2;
            return (short)(_buffer[p] | (_buffer[p + 1] << 8));
        }

        public ushort ReadUInt16()
        {
            var p = Position;
            Position += 2;
            return (ushort)(_buffer[p] | (_buffer[p + 1] << 8));
        }

        public int ReadInt32()
        {
            var p = Position;
            Position += 4;
            return _buffer[p] | (_buffer[p + 1] << 8) | (_buffer[p + 2] << 16) | (_buffer[p + 3] << 24);
        }

        public uint ReadUInt32()
        {
            var p = Position;
            Position += 4;
            return (uint)(_buffer[p] | (_buffer[p + 1] << 8) | (_buffer[p + 2] << 16) | (_buffer[p + 3] << 24));
        }

        public long ReadInt64()
        {
            var p = Position;
            Position += 8;
            return _buffer[p] | (_buffer[p + 1] << 8) | (_buffer[p + 2] << 16) | (_buffer[p + 3] << 24) | (_buffer[p + 4] << 32) |
                   (_buffer[p + 5] << 40) | (_buffer[p + 6] << 48) | (_buffer[p + 7] << 56);
        }

        public ulong ReadUInt64()
        {
            var p = Position;
            Position += 8;
            return (ulong)(_buffer[p] | (_buffer[p + 1] << 8) | (_buffer[p + 2] << 16) | (_buffer[p + 3] << 24) |
                            (_buffer[p + 4] << 32) | (_buffer[p + 5] << 40) | (_buffer[p + 6] << 48) | (_buffer[p + 7] << 56));
        }

    }
}
