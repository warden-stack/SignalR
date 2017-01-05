// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Sockets.Internal
{
    public class MessagingConnectionState : ConnectionState
    {
        public new MessagingConnection Connection => (MessagingConnection)base.Connection;
        public IChannelConnection<Message> Application { get; }

        public MessagingConnectionState(MessagingConnection connection, IChannelConnection<Message> application) : base(connection)
        {
            Application = application;
        }

        public override void Dispose()
        {
            Connection.Dispose();
            Application.Dispose();
        }
    }
}
