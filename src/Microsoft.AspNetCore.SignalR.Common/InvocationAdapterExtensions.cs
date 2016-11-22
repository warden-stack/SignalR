// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.SignalR
{
    public static class InvocationAdapterExtensions
    {
        public static Task<InvocationResultDescriptor> ReadInvocationResultDescriptorAsync(this IInvocationAdapter self, Stream stream, Func<string, Type> invocationIdToResultType) =>
            self.ReadInvocationResultDescriptorAsync(stream, invocationIdToResultType, CancellationToken.None);

        public static Task<InvocationDescriptor> ReadInvocationDescriptorAsync(this IInvocationAdapter self, Stream stream, Func<string, Type[]> getParams) =>
            self.ReadInvocationDescriptorAsync(stream, getParams, CancellationToken.None);

        public static Task WriteInvocationResultAsync(this IInvocationAdapter self, InvocationResultDescriptor resultDescriptor, Stream stream) =>
            self.WriteInvocationResultAsync(resultDescriptor, stream, CancellationToken.None);

        public static Task WriteInvocationDescriptorAsync(this IInvocationAdapter self, InvocationDescriptor invocationDescriptor, Stream stream) =>
            self.WriteInvocationDescriptorAsync(invocationDescriptor, stream, CancellationToken.None);
    }
}
