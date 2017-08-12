using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Threading.Tasks;

namespace ResponseProxy
{
    public class ResponseProxyMiddleware
    {
        private readonly RequestDelegate _next;
        private IProxyStreamProvider _provider;

        public ResponseProxyMiddleware(RequestDelegate next, IProxyStreamProvider provider)
        {
            _next = next;
            _provider = provider;
        }

        public async Task Invoke(HttpContext context)
        {
            var bodyStream = context.Response.Body;
            var originalBufferFeature = context.Features.Get<IHttpBufferingFeature>();
            var originalSendFileFeature = context.Features.Get<IHttpSendFileFeature>();

            var bodyWrapperStream = new BodyWrapperStream(context, bodyStream, _provider,
                originalBufferFeature, originalSendFileFeature);
            context.Response.Body = bodyWrapperStream;
            context.Features.Set<IHttpBufferingFeature>(bodyWrapperStream);
            if (originalSendFileFeature != null)
                context.Features.Set<IHttpSendFileFeature>(bodyWrapperStream);

            try
            {
                await _next(context);
                bodyWrapperStream.EnableFlush = true;
                await bodyWrapperStream.FlushAsync();
                bodyWrapperStream.Dispose();
            }
            finally
            {
                context.Response.Body = bodyStream;
                context.Features.Set(originalBufferFeature);
                if (originalSendFileFeature != null)
                    context.Features.Set(originalSendFileFeature);
            }

        }
    }
}
