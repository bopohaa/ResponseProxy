using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;
using ResponseProxy;

namespace SimpleProxy
{
    public class SimpleProxyStreamProvider : IProxyStreamProvider
    {
        public Stream CreateStream(HttpContext context, Stream inner_stream)
        {
            return context.Items.ContainsKey("test") ?
                new SimpleProxyStream(inner_stream, Encoding.UTF8) :
                null;
        }
    }
}
