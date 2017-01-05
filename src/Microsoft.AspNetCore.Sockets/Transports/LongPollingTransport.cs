// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Sockets.Transports
{
    public class LongPollingTransport : IHttpTransport
    {
        private readonly IReadableChannel<Message> _connection;
        private CancellationTokenSource _cancellationSource = new CancellationTokenSource();
        private readonly ILogger _logger;

        public LongPollingTransport(IReadableChannel<Message> connection, ILoggerFactory loggerFactory)
        {
            _connection = connection;
            _logger = loggerFactory.CreateLogger<LongPollingTransport>();
        }

        public async Task ProcessRequestAsync(HttpContext context)
        {
            if (_connection.Completion.IsCompleted)
            {
                // Client should stop if it receives a 204
                _logger.LogInformation("Terminating Long Polling connection by sending 204 response.");
                context.Response.StatusCode = 204;
                return;
            }

            try
            {
                using (var message = await _connection.ReadAsync(_cancellationSource.Token))
                {
                    _logger.LogDebug("Writing {0} byte message to response", message.Payload.Buffer.Length);
                    context.Response.ContentLength = message.Payload.Buffer.Length;
                    await message.Payload.Buffer.CopyToAsync(context.Response.Body);
                }
            }
            catch (OperationCanceledException)
            {
                // Suppress the exception
                _logger.LogDebug("Client disconnected from Long Polling endpoint.");
            }
            catch(Exception ex)
            {
                _logger.LogError("Error reading next message from Application: {0}", ex);
                throw;
            }
        }

        public void Cancel()
        {
            _cancellationSource.Cancel();
        }
    }
}
