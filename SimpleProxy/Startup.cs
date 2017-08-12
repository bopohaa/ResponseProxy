using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResponseProxy;

namespace SimpleProxy
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app
                .UseMiddleware<ResponseProxyMiddleware>(new SimpleProxyStreamProvider())
                .UseStaticFiles(new StaticFileOptions() {
                    OnPrepareResponse = hc =>
                    {
                        if (System.IO.Path.GetExtension(hc.File.Name) == ".js")
                        {
                            hc.Context.Items["test"] = true;
                        }
                    }
            });
           

            app.Run(async (context) =>
            {
                await context.Response.WriteAsync("Hello World!");
            });
        }
    }
}
