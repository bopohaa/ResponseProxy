using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProxy
{
    public class SimpleProxyStream : Stream
    {
        private const int BUFFER_SIZE = 16384;
        private static int _inc = 0;
        private readonly Stream _innerStream;
        private ArraySegment<byte> _buffer;
        private Encoding _encoding;

        public SimpleProxyStream(Stream innerStream, Encoding encoding)
        {
            _innerStream = innerStream;
            _encoding = encoding;
            _buffer = new ArraySegment<byte>(new byte[BUFFER_SIZE], 0, BUFFER_SIZE);
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length { get { throw new NotSupportedException(); } }

        // Clear/Reset the buffer by setting Position, Seek, or SetLength to 0. Random access is not supported.
        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        // Clear/Reset the buffer by setting Position, Seek, or SetLength to 0. Random access is not supported.
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        // Clear/Reset the buffer by setting Position, Seek, or SetLength to 0. Random access is not supported.
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var newOffset = _buffer.Offset + count;
            if (newOffset > _buffer.Array.Length)
            {
                var buff = _buffer.Array;
                Array.Resize(ref buff, Math.Max(newOffset, buff.Length + BUFFER_SIZE));
                _buffer = new ArraySegment<byte>(buff, _buffer.Offset, buff.Length - _buffer.Offset);
            }
            Array.Copy(buffer, offset, _buffer.Array, _buffer.Offset, count);
            _buffer = new ArraySegment<byte>(_buffer.Array, _buffer.Offset + count, _buffer.Count - count);

        }

        public override void Flush()
        {
            FlushAsync().Wait();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_buffer.Offset > 0)
            {
                var content = _encoding.GetString(_buffer.Array, 0, _buffer.Offset);
                var nContent = content.Replace("Привет", "Hello" + Interlocked.Increment(ref _inc));
                var data = _encoding.GetBytes(nContent);
                await _innerStream.WriteAsync(data, 0, data.Length);

                _buffer = new ArraySegment<byte>(_buffer.Array, 0, _buffer.Array.Length);
            }
            await _innerStream.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("This Stream only supports Write operations.");
        }
    }
}
