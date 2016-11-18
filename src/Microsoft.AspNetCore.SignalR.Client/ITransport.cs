using System;
using System.IO.Pipelines;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.SignalR.Client
{
    public interface ITransport : IDisposable
    {
        Task StartAsync(Uri url, IPipelineConnection pipeline);
    }
}