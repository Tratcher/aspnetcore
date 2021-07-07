// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Testing;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Microsoft.AspNetCore.Server.HttpSys
{
    // Requires these reg keys and a reboot.
    // reg add "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\HTTP\Parameters" /v EnableHttp3 /t REG_DWORD /d 1 /f
    // Second one is needed only if you want to send altsvc frame on h2 connections.
    // reg add "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\HTTP\Parameters" /v EnableAltSvc /t REG_DWORD /d 1 /f
    [MsQuicSupported] // Required by HttpClient
    public class Http3Tests
    {
        [ConditionalFact]
        public async Task Http3_Direct()
        {
            using var server = Utilities.CreateDynamicHttpsServer(out var address, async httpContext =>
            {
                try
                {
                    Assert.True(httpContext.Request.IsHttps);
                    Assert.Equal("HTTP/3", httpContext.Request.Protocol);
                }
                catch (Exception ex)
                {
                    await httpContext.Response.WriteAsync(ex.ToString());
                }
            });
            var handler = new HttpClientHandler();
            // https://github.com/dotnet/runtime/issues/55192, but needed on CI
            // handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            using var client = new HttpClient(handler);
            client.DefaultRequestVersion = HttpVersion.Version30;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            var response = await client.GetStringAsync(address);
            Assert.Equal(string.Empty, response);
        }

        [ConditionalFact]
        public async Task Http3_AltSvcHeader_UpgradeFromHttp1()
        {
            var altsvc = "";
            using var server = Utilities.CreateDynamicHttpsServer(out var address, async httpContext =>
            {
                try
                {
                    Assert.True(httpContext.Request.IsHttps);
                    if (httpContext.Request.Path == "/1")
                    {
                        Assert.Equal("HTTP/1.1", httpContext.Request.Protocol);
                        httpContext.Response.Headers.AltSvc = altsvc;
                    }
                    else if (httpContext.Request.Path == "/3")
                    {
                        Assert.Equal("HTTP/3", httpContext.Request.Protocol);
                    }
                    else
                    {
                        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                    }
                }
                catch (Exception ex)
                {
                    await httpContext.Response.WriteAsync(ex.ToString());
                }
            });

            altsvc = $@"h3="":{new Uri(address).Port}""";
            var handler = new HttpClientHandler();
            // https://github.com/dotnet/runtime/issues/55192, but needed on CI
            // handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            using var client = new HttpClient(handler);
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;

            // First request is HTTP/1.1, gets an alt-svc response
            var request = new HttpRequestMessage(HttpMethod.Get, address + "1");
            request.Version = HttpVersion.Version11;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            var response1 = await client.SendAsync(request);
            response1.EnsureSuccessStatusCode();
            Assert.Equal(altsvc, response1.Headers.GetValues(HeaderNames.AltSvc).SingleOrDefault());
            Assert.Equal(string.Empty, await response1.Content.ReadAsStringAsync());

            // Second request is HTTP/3
            var response3 = await client.GetStringAsync(address + "3");
            Assert.Equal(string.Empty, response3);
        }

        [ConditionalFact]
        public async Task Http3_AltSvcHeader_UpgradeFromHttp2()
        {
            var altsvc = "";
            using var server = Utilities.CreateDynamicHttpsServer(out var address, async httpContext =>
            {
                try
                {
                    Assert.True(httpContext.Request.IsHttps);
                    if (httpContext.Request.Path == "/2")
                    {
                        Assert.Equal("HTTP/2", httpContext.Request.Protocol);
                        httpContext.Response.Headers.AltSvc = altsvc;
                    }
                    else if (httpContext.Request.Path == "/3")
                    {
                        Assert.Equal("HTTP/3", httpContext.Request.Protocol);
                    }
                    else
                    {
                        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                    }
                }
                catch (Exception ex)
                {
                    await httpContext.Response.WriteAsync(ex.ToString());
                }
            });

            altsvc = $@"h3="":{new Uri(address).Port}""";
            var handler = new HttpClientHandler();
            // https://github.com/dotnet/runtime/issues/55192, but needed on CI
            // handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            using var client = new HttpClient(handler);
            client.DefaultRequestVersion = HttpVersion.Version20;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;

            // First request is HTTP/2, gets an alt-svc response
            var response2 = await client.GetAsync(address + "2");
            response2.EnsureSuccessStatusCode();
            Assert.Equal(altsvc, response2.Headers.GetValues(HeaderNames.AltSvc).SingleOrDefault());
            Assert.Equal(string.Empty, await response2.Content.ReadAsStringAsync());

            // Second request is HTTP/3
            var response3 = await client.GetStringAsync(address + "3");
            Assert.Equal(string.Empty, response3);
        }

        [ConditionalFact(Skip = "Why isn't Http.Sys sending the ALTSVC frame?")]
        public async Task Http3_AltSvcFrame_UpgradeFromHttp2()
        {
            using var server = Utilities.CreateDynamicHttpsServer(out var address, async httpContext =>
            {
                try
                {
                    Assert.True(httpContext.Request.IsHttps);
                    if (httpContext.Request.Path == "/2")
                    {
                        Assert.Equal("HTTP/2", httpContext.Request.Protocol);
                    }
                    else if (httpContext.Request.Path == "/3")
                    {
                        Assert.Equal("HTTP/3", httpContext.Request.Protocol);
                    }
                    else
                    {
                        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                    }
                }
                catch (Exception ex)
                {
                    await httpContext.Response.WriteAsync(ex.ToString());
                }
            });

            var handler = new SocketsHttpHandler();
            // https://github.com/dotnet/runtime/issues/55192, but needed on CI
            // handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            using var client = new HttpClient(handler);
            client.DefaultRequestVersion = HttpVersion.Version20;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;

            // First request is HTTP/2, gets an alt-svc frame response
            var response2 = await client.GetAsync(address + "2");
            response2.EnsureSuccessStatusCode();
            Assert.False(response2.Headers.TryGetValues(HeaderNames.AltSvc, out var _));
            Assert.Equal(string.Empty, await response2.Content.ReadAsStringAsync());

            // Second request is HTTP/3
            var response3 = await client.GetStringAsync(address + "3");
            Assert.Equal(string.Empty, response3);
        }
    }
}
