#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Server.Services;

namespace Server
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddGrpc();
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/dist";
            });
            services.AddCors(o => o.AddPolicy("AllowAll", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .WithExposedHeaders("Grpc-Status", "Grpc-Message");
            }));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            var rewriteOptions = new RewriteOptions()
                .AddRewrite(regex: @"(?:\d{4}-\d{2}-\d{2}/proxy/[^/]+/[^/]+/)(.+)", replacement: "$1", skipRemainingRules: true);
            app.UseRewriter(rewriteOptions);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                var indexPage = "ClientApp/dist/index.html";
                if (!File.Exists(indexPage))
                {
                    using var p = RunNpmScript("build:prod");
                    p.WaitForExit();
                }
            }
            else
            {
                app.UseHsts();
            }

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseSpaStaticFiles();
            app.UseRouting();
            app.UseCors();

            app.UseGrpcWeb();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<GreeterService>()
                .RequireCors("AllowAll")
                .EnableGrpcWeb();
            });

            app.UseSpa(spa =>
            {
                if (env.IsDevelopment())
                {
                    RunNpmScript("build:watch");
                }
            });
        }

        private static Process RunNpmScript(string script)
        {
            var fileName = "npm";
            var args = $"run {script}";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                fileName = "cmd";
                args = $"/c npm {args}";
            }

            return Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                WorkingDirectory = "ClientApp",
            });
        }
    }
}
