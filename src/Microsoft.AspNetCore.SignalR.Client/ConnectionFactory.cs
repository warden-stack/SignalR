using System;
using System.IO.Pipelines;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.SignalR.Client
{
    // REVIEW: Split into Microsoft.AspNetCore.Sockets.Client?
    public class ConnectionFactory
    {
        private readonly HttpClient _httpClient;
        private readonly ILoggerFactory _loggerFactory;
        private readonly PipelineFactory _pipelineFactory;
        private readonly ILogger _logger;

        public ConnectionFactory(HttpClient httpClient, PipelineFactory pipelineFactory, ILoggerFactory loggerFactory)
        {
            _httpClient = httpClient;
            _pipelineFactory = pipelineFactory;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<ConnectionFactory>();
        }

        public async Task<Connection> ConnectAsync(Uri url, ITransport transport)
        {
            var getIdUrl = Utils.AppendPath(url, "getid");

            string connectionId;
            try
            {
                // Get a connection ID from the server
                _logger.LogDebug("Reserving Connection Id from: {0}", getIdUrl);
                connectionId = await _httpClient.GetStringAsync(getIdUrl);
                _logger.LogDebug("Reserved Connection Id: {0}", connectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start connection. Error getting connection id from '{0}': {1}", getIdUrl, ex);
                throw;
            }

            // Create a pair of pipelines for input and output
            var clientToServer = _pipelineFactory.Create();
            var serverToClient = _pipelineFactory.Create();

            // Transport reads from clientToServer and writes to serverToClient
            var transportPipe = new PipelineConnection(clientToServer, serverToClient);

            // Consumer reads from serverToClient and writes to clientToServer
            var consumerPipe = new PipelineConnection(serverToClient, clientToServer);

            var connectedUrl = Utils.AppendQueryString(url, "id=" + connectionId);

            // Start the transport, giving it the transport pipeline
            try
            {
                await transport.StartAsync(connectedUrl, transportPipe);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start connection. Error starting transport '{0}': {1}", transport.GetType().Name, ex);
                throw;
            }

            // Create the connection, giving it the consumer pipeline
            return new Connection(url, transport, consumerPipe, _loggerFactory);
        }
    }
}
