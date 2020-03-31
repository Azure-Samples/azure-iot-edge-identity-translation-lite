// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Edge.Util.Uds
{
    using System;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    class HttpUdsMessageHandler : HttpMessageHandler
    {
        readonly Uri providerUri;

        public HttpUdsMessageHandler(Uri providerUri)
        {
            this.providerUri = providerUri;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Socket socket = await this.GetConnectedSocketAsync();
            var stream = new HttpBufferedStream(new NetworkStream(socket, true));

            var serializer = new HttpRequestResponseSerializer();
            byte[] requestBytes = serializer.SerializeRequest(request);

            await stream.WriteAsync(requestBytes, 0, requestBytes.Length, cancellationToken);
            if (request.Content != null)
            {
                await request.Content.CopyToAsync(stream);
            }

            HttpResponseMessage response = await serializer.DeserializeResponse(stream, cancellationToken);

            return response;
        }

        async Task<Socket> GetConnectedSocketAsync()
        {
            var endpoint = new UnixDomainSocketEndPoint(this.providerUri.LocalPath);
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(endpoint);

            return socket;
        }
    }
}
