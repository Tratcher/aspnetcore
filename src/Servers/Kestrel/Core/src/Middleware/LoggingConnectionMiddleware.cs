// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal
{
    internal class LoggingConnectionMiddleware
    {
        private readonly ConnectionDelegate _next;
        private readonly ILogger _logger;

        public LoggingConnectionMiddleware(ConnectionDelegate next, ILogger logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task OnConnectionAsync(ConnectionContext context)
        {
            var oldTransport = context.Transport;

            try
            {
                await using (var loggingDuplexPipe = new LoggingDuplexPipe(context.Transport, _logger))
                {
                    context.Transport = loggingDuplexPipe;

                    await _next(context);
                }
            }
            finally
            {
                context.Transport = oldTransport;
            }
        }

        private static readonly NetCapture capture = new NetCapture(@"c:\temp\capture.pcap");

        private class LoggingDuplexPipe : DuplexPipeStreamAdapter<PcapStream>
        {
            public LoggingDuplexPipe(IDuplexPipe transport, ILogger logger) :
                base(transport, stream => capture.AddStream(stream))
            {
            }
        }
    }
}
