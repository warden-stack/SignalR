using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.SignalR.Client
{
    public class RpcConnection : IDisposable
    {
        private readonly IInvocationAdapter _adapter;
        private readonly Connection _connection;
        private readonly Stream _stream;
        private readonly Task _reader;

        private readonly CancellationTokenSource _readerCts = new CancellationTokenSource();
        private readonly ConcurrentDictionary<string, InvocationRequest> _pendingCalls = new ConcurrentDictionary<string, InvocationRequest>();

        private int _nextId = 0;

        private RpcConnection(Connection connection, IInvocationAdapter adapter)
        {
            _connection = connection;
            _stream = connection.GetStream();
            _adapter = adapter;
            _reader = ReceiveMessages(_readerCts.Token);
        }

        public Task<T> Invoke<T>(string methodName, params object[] args) => Invoke<T>(methodName, CancellationToken.None, args);
        public async Task<T> Invoke<T>(string methodName, CancellationToken cancellationToken, params object[] args) => ((T)(await Invoke(methodName, typeof(T), cancellationToken, args)));

        public Task<object> Invoke(string methodName, Type returnType, params object[] args) => Invoke(methodName, returnType, CancellationToken.None, args);
        public async Task<object> Invoke(string methodName, Type returnType, CancellationToken cancellationToken, params object[] args)
        {
            // Write a
            var descriptor = new InvocationDescriptor()
            {
                Id = GetNextId(),
                Method = methodName,
                Arguments = args
            };

            // I just want an excuse to use 'irq' as a variable name...
            var irq = new InvocationRequest(cancellationToken, returnType);
            var addedSuccessfully = _pendingCalls.TryAdd(descriptor.Id, irq);

            // This should always be true since we monotonically increase ids.
            Debug.Assert(addedSuccessfully, "Id already in use?");

            // Write the invocation to the stream
            await _adapter.WriteInvocationDescriptorAsync(descriptor, _stream, cancellationToken);

            // Return the completion task. It will be completed by ReceiveMessages when the response is received.
            return irq.Completion.Task;
        }

        public void Dispose()
        {
            _readerCts.Cancel();
            _connection.Dispose();
        }

        // TODO: Clean up the API here. Negotiation of format would be better than providing an adapter instance.
        public static Task<RpcConnection> ConnectAsync(Uri url, IInvocationAdapter adapter, ITransport transport, PipelineFactory pipelineFactory) => ConnectAsync(url, transport, new HttpClient(), pipelineFactory, NullLoggerFactory.Instance);
        public static Task<RpcConnection> ConnectAsync(Uri url, IInvocationAdapter adapter, ITransport transport, PipelineFactory pipelineFactory, ILoggerFactory loggerFactory) => ConnectAsync(url, transport, new HttpClient(), pipelineFactory, loggerFactory);
        public static Task<RpcConnection> ConnectAsync(Uri url, IInvocationAdapter adapter, ITransport transport, HttpClient httpClient, PipelineFactory pipelineFactory) => ConnectAsync(url, transport, httpClient, pipelineFactory, NullLoggerFactory.Instance);

        public static async Task<RpcConnection> ConnectAsync(Uri url, IInvocationAdapter adapter, ITransport transport, HttpClient httpClient, PipelineFactory pipelineFactory, ILoggerFactory loggerFactory)
        {
            // Connect the underlying connection
            var connection = await Connection.ConnectAsync(url, transport, httpClient, pipelineFactory, loggerFactory);

            // Create the RPC connection wrapper
            return new RpcConnection(connection, adapter);
        }

        private async Task ReceiveMessages(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // This is a little odd... we want to remove the InvocationRequest once and only once so we pull it out in the callback,
                // and stash it here because we know the callback will have finished before the end of the await.
                InvocationRequest irq = default(InvocationRequest);
                var result = await _adapter.ReadInvocationResultDescriptorAsync(_stream, id =>
                {
                    if (!_pendingCalls.TryRemove(id, out irq))
                    {
                        throw new InvalidOperationException($"Unsolicited response received for invocation '{id}'");
                    }
                    return irq.ResultType;
                }, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Debug.Assert(irq.Completion != null, "Didn't properly capture InvocationRequest in callback for ReadInvocationResultDescriptorAsync");

                // If the invocation hasn't been cancelled, dispatch the result
                if (!irq.CancellationToken.IsCancellationRequested)
                {
                    irq.Registration.Dispose();

                    // Complete the request based on the result
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        irq.Completion.TrySetException(new Exception(result.Error));
                    }
                    else
                    {
                        irq.Completion.TrySetResult(result.Result);
                    }
                }
            }
        }

        private string GetNextId()
        {
            // Increment returns the incremented value, so we subtract one to get the previous value.
            return (Interlocked.Increment(ref _nextId) - 1).ToString();
        }

        private struct InvocationRequest
        {
            public Type ResultType { get; }
            public CancellationToken CancellationToken { get; }
            public CancellationTokenRegistration Registration { get; }
            public TaskCompletionSource<object> Completion { get; }

            public InvocationRequest(CancellationToken cancellationToken, Type resultType)
            {
                var tcs = new TaskCompletionSource<object>();
                Completion = tcs;
                CancellationToken = cancellationToken;
                Registration = cancellationToken.Register(() => tcs.TrySetCanceled());
                ResultType = resultType;
            }
        }
    }
}
