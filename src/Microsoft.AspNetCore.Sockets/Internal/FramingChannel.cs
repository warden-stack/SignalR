﻿using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Channels;

namespace Microsoft.AspNetCore.Sockets.Internal
{
    /// <summary>
    /// Creates a <see cref="IChannelConnection{Message}"/> out of a <see cref="IPipelineConnection"/> by framing data
    /// read out of the Pipeline and flattening out frames to write them to the Pipeline when received.
    /// </summary>
    public class FramingChannel : IChannelConnection<Message>, IReadableChannel<Message>, IWritableChannel<Message>
    {
        private readonly IPipelineConnection _pipeline;
        private readonly TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>();
        private readonly Format _format;

        Task IReadableChannel<Message>.Completion => _tcs.Task;

        public IReadableChannel<Message> Input => this;
        public IWritableChannel<Message> Output => this;

        public FramingChannel(IPipelineConnection pipeline, Format format)
        {
            _pipeline = pipeline;
            _format = format;
        }

        ValueTask<Message> IReadableChannel<Message>.ReadAsync(CancellationToken cancellationToken)
        {
            var awaiter = _pipeline.Input.ReadAsync();
            if (awaiter.IsCompleted)
            {
                return new ValueTask<Message>(ReadSync(awaiter.GetResult(), cancellationToken));
            }
            else
            {
                return new ValueTask<Message>(AwaitReadAsync(awaiter, cancellationToken));
            }
        }

        bool IReadableChannel<Message>.TryRead(out Message item)
        {
            // We need to think about how we do this. There's no way to check if there is data available in a Pipeline... though maybe there should be
            // We could ReadAsync and check IsCompleted, but then we'd also need to stash that Awaitable for later since we can't call ReadAsync a second time...
            // CancelPendingReads could help here.
            item = default(Message);
            return false;
        }

        Task<bool> IReadableChannel<Message>.WaitToReadAsync(CancellationToken cancellationToken)
        {
            // See above for TryRead. Same problems here.
            throw new NotSupportedException();
        }

        Task IWritableChannel<Message>.WriteAsync(Message item, CancellationToken cancellationToken)
        {
            // Just dump the message on to the pipeline
            var buffer = _pipeline.Output.Alloc();
            buffer.Append(item.Payload.Buffer);
            return buffer.FlushAsync();
        }

        Task<bool> IWritableChannel<Message>.WaitToWriteAsync(CancellationToken cancellationToken)
        {
            // We need to think about how we do this. We don't have a wait to synchronously check for back-pressure in the Pipeline.
            throw new NotSupportedException();
        }

        bool IWritableChannel<Message>.TryWrite(Message item)
        {
            // We need to think about how we do this. We don't have a wait to synchronously check for back-pressure in the Pipeline.
            return false;
        }

        bool IWritableChannel<Message>.TryComplete(Exception error)
        {
            _pipeline.Output.Complete(error);
            return true;
        }

        private async Task<Message> AwaitReadAsync(ReadableBufferAwaitable awaiter, CancellationToken cancellationToken)
        {
            // Just await and then call ReadSync
            var result = await awaiter;
            return ReadSync(result, cancellationToken);
        }

        private Message ReadSync(ReadResult result, CancellationToken cancellationToken)
        {
            var buffer = result.Buffer;

            // Preserve the buffer and advance the pipeline past it
            var preserved = buffer.Preserve();
            _pipeline.Input.Advance(buffer.End);

            var msg = new Message(preserved, _format, endOfMessage: true);

            if (result.IsCompleted)
            {
                // Complete the task
                _tcs.TrySetResult(null);
            }

            return msg;
        }

        public void Dispose()
        {
            _tcs.TrySetResult(null);
            _pipeline.Dispose();
        }
    }
}
