using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ResponseProxy
{
    public interface IProxyStreamProvider
    {
        Stream CreateStream(HttpContext context, Stream inner_stream);
    }
}
