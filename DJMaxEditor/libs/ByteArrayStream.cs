using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DJMaxEditor 
{
    class ByteArrayStream : System.IO.Stream 
    {

        public int Available
        {
            get
            {
                return (int)(_array.Length - _pos - _origin);
            }
        }

        public override long Position
        {
            get
            {
                return _pos;
            }
            set
            {
                _pos = (uint)value;
            }
        }

        public override long Length
        {
            get { return _array.Length - _origin; }
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public ByteArrayStream() 
        {
            _pos = 0;
        }

        public ByteArrayStream(byte[] array) 
        {
            this._array = array;
            _pos = 0;
        }

        public byte[] GetData() 
        {
            return _array;
        }

        public void Skip(uint bytes) 
        {
            _pos += bytes;
        }

        public string ReadString(int l) 
        {
            if (l == 0) return ""; // simple error checking

            byte[] arr = new byte[l];
            Read(arr);

            StringBuilder NewStr = new StringBuilder(l);
            for (int i = 0; i < l; i++) { 
                if (arr[i] == 0) {
                    break;
                }
                NewStr.Append((char)arr[i]);
            }

            return NewStr.ToString().Trim();
        }

        public void Seek(uint pos) 
        {
            this._pos = pos;
        }

        public void Seek(int pos) 
        {
            this._pos = (uint)pos;
        }

        public ushort ReadUShort() 
        {
            _pos += 2;
            return (ushort)(_array[_pos - 2 + _origin] | _array[_pos - 1 + _origin] << 8);
        }

        public byte ReadByte() 
        {
            return _array[_origin + _pos++];
        }

        public byte[] ReadBytes(int l) 
        {
            byte[] arr = new byte[l];
            Read(arr);
            return arr;
        }

        public float ReadFloat()
        {
            byte[] flt = ReadBytes(4);
            return BitConverter.ToSingle(flt, 0);
        }

        public uint ReadUInt() 
        {
            uint res = 0;
            for (int i = 0; i < 4; i++) {
                res |= (uint)ReadByte() << 8 * i;
            }
            return res; 
        }

        public int ReadInt() 
        {
            uint res = 0;
            for (int i = 0; i < 4; i++) {
                res |= (uint)ReadByte() << 8 * i;
            }
            return (int)res;
        }

        public void Read(byte[] dest) 
        {
            Array.Copy(_array, _pos + _origin, dest, 0, dest.Length);
            _pos += (uint)(dest.Length);
        }

        public bool LengthAvailable(int len) 
        {
            return Available >= len;
        }

        public void WriteString(string str, int length) 
        {
            byte[] buffer = Encoding.ASCII.GetBytes(str.PadRight(length, '\0'));
            Write(buffer, 0, buffer.Length);
        }

        public void Write(byte[] data) 
        {
            Write(data, 0, data.Length);
        }

        public void WriteByte(byte b) 
        {
            Write(new byte[1] { b }, 0, 1);
        }

        public void WriteFloat(float f)
        {
            byte[] res = BitConverter.GetBytes(f);
            Write(res);
        }

        public void WriteUShort(ushort u) 
        {
            WriteByte((byte)u);
            WriteByte((byte)(u >> 8));
        }

        public void writeUInt(uint u) 
        {
            WriteByte((byte)u);
            WriteByte((byte)(u >> 8));
            WriteByte((byte)(u >> 16));
            WriteByte((byte)(u >> 24));
        }

        public void writeInt(int i) 
        {
            writeUInt((uint)i);

        }

        public void writeLong(long u) 
        {
            WriteByte((byte)u);
            WriteByte((byte)(u >> 8));
            WriteByte((byte)(u >> 16));
            WriteByte((byte)(u >> 24));
            WriteByte((byte)(u >> 32));
            WriteByte((byte)(u >> 40));
            WriteByte((byte)(u >> 48));
            WriteByte((byte)(u >> 56));
        }

        #region stream overrrided functions

        public override long Seek(long offset, System.IO.SeekOrigin origin) 
        {
            switch (origin) {
                case System.IO.SeekOrigin.Begin:
                _pos = this._origin + (uint)offset;
                break;
                case System.IO.SeekOrigin.Current:
                _pos += (uint)offset;
                break;
                case System.IO.SeekOrigin.End:
                _pos = (uint)_array.Length - this._origin - (uint)offset;
                break;
            }
            return _pos;
        }

        public override int Read(byte[] buffer, int offset, int count) 
        {
            Array.Copy(_array, _pos + _origin, buffer, offset, count);
            _pos += (uint)count;
            return count;
        }

        public override void Write(byte[] buffer, int offset, int count) 
        {
            if (!LengthAvailable(count)) {
                SetLength(_pos + count);
            }
            Array.Copy(buffer, offset, _array, _pos + _origin, count);
            _pos += (uint)count;
        }

        public override void SetLength(long value) 
        {
            Array.Resize<byte>(ref _array, (int)value + (int)_origin);
        }

        public override void Flush() 
        {
        }

        #endregion // stream overrrided functions

        private byte[] _array = new byte[0];

        private uint _pos = 0, _origin = 0;

    }

}
