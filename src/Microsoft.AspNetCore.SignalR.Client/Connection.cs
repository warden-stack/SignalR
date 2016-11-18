using System;
using System.IO.Pipelines;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.SignalR.Client
{
    public class Connection : IPipelineConnection
    {
        private IPipelineConnection _consumerPipe;
        private ITransport _transport;
        private readonly ILogger _logger;

        public Uri Url { get; }

        public Connection(Uri url, ITransport transport, IPipelineConnection consumerPipe, ILoggerFactory _loggerFactory)
        {
            Url = url;

            _logger = _loggerFactory.CreateLogger<Connection>();
            _transport = transport;
            _consumerPipe = consumerPipe;
        }

        public IPipelineReader Input => _consumerPipe.Input;
        public IPipelineWriter Output => _consumerPipe.Output;

        public void Dispose()
        {
            _consumerPipe.Dispose();
            _transport.Dispose();
        }
    }
}