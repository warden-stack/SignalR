// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.SignalR
{
    public interface IInvocationAdapter
    {
        Task<InvocationResultDescriptor> ReadInvocationResultDescriptorAsync(Stream stream, Func<string, Type> invocationIdToResultType, CancellationToken cancellationToken);

        Task<InvocationDescriptor> ReadInvocationDescriptorAsync(Stream stream, Func<string, Type[]> getParams, CancellationToken cancellationToken);

        Task WriteInvocationResultAsync(InvocationResultDescriptor resultDescriptor, Stream stream, CancellationToken cancellationToken);

        Task WriteInvocationDescriptorAsync(InvocationDescriptor invocationDescriptor, Stream stream, CancellationToken cancellationToken);
    }
}
