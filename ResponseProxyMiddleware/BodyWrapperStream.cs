using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ResponseProxy
{
    internal class BodyWrapperStream : Stream, IHttpBufferingFeature, IHttpSendFileFeature
    {
        private readonly HttpContext _context;
        private readonly Stream _bodyOriginalStream;
        private readonly IHttpBufferingFeature _innerBufferFeature;
        private readonly IHttpSendFileFeature _innerSendFileFeature;

        private IProxyStreamProvider _provider = null;
        private bool _proxyChecked = false;
        private Stream _proxyStream = null;

        public bool EnableFlush { get; set; }

        internal BodyWrapperStream(HttpContext context, Stream bodyOriginalStream, IProxyStreamProvider provider,
            IHttpBufferingFeature innerBufferFeature, IHttpSendFileFeature innerSendFileFeature)
        {
            _context = context;
            _bodyOriginalStream = bodyOriginalStream;
            _provider = provider;
            _innerBufferFeature = innerBufferFeature;
            _innerSendFileFeature = innerSendFileFeature;
            EnableFlush = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (_proxyStream != null)
            {
                _proxyStream.Dispose();
                _proxyStream = null;
            }
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => _bodyOriginalStream.CanWrite;

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override void Flush()
        {
            if (!_proxyChecked)
            {
                OnWrite();
                _bodyOriginalStream.Flush();
                return;
            }

            if (_proxyStream != null)
            {
                if (EnableFlush)
                    _proxyStream.Flush();
            }
            else
            {
                _bodyOriginalStream.Flush();
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (!_proxyChecked)
            {
                OnWrite();
                return _bodyOriginalStream.FlushAsync(cancellationToken);
            }

            if (_proxyStream != null)
            {
                return EnableFlush ? _proxyStream.FlushAsync(cancellationToken) : Task.CompletedTask;
            }

            return _bodyOriginalStream.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            OnWrite();

            if (_proxyStream != null)
            {
                _proxyStream.Write(buffer, offset, count);
            }
            else
            {
                _bodyOriginalStream.Write(buffer, offset, count);
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            OnWrite();

            if (_proxyStream != null)
            {
                await _proxyStream.WriteAsync(buffer, offset, count, cancellationToken);
            }
            else
            {
                await _bodyOriginalStream.WriteAsync(buffer, offset, count, cancellationToken);
            }
        }

        private void OnWrite()
        {
            if (!_proxyChecked)
            {
                _proxyChecked = true;
                _proxyStream = _provider.CreateStream(_context, _bodyOriginalStream);
                if (_proxyStream != null)
                {
                    _context.Response.Headers.Remove(HeaderNames.ContentMD5);
                    _context.Response.Headers.Remove(HeaderNames.ContentLength);
                }
            }
        }

        public void DisableRequestBuffering()
        {
            _innerBufferFeature?.DisableRequestBuffering();
        }

        public void DisableResponseBuffering()
        {
            _innerBufferFeature?.DisableResponseBuffering();
        }

        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellation)
        {
            OnWrite();

            if (_proxyStream != null)
            {
                return InnerSendFileAsync(path, offset, count, cancellation);
            }

            return _innerSendFileFeature.SendFileAsync(path, offset, count, cancellation);
        }

        private async Task InnerSendFileAsync(string path, long offset, long? count, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(path);
            if (offset < 0 || offset > fileInfo.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, string.Empty);
            }
            if (count.HasValue &&
                (count.Value < 0 || count.Value > fileInfo.Length - offset))
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, string.Empty);
            }

            int bufferSize = 1024 * 16;

            var fileStream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: bufferSize,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            using (fileStream)
            {
                fileStream.Seek(offset, SeekOrigin.Begin);
                await StreamCopyOperation.CopyToAsync(fileStream, _proxyStream, count, cancellation);
            }
        }
    }
}
