// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using Microsoft.Azure.Devices.Edge.Util.Uds;

    public class HttpClientHelper
    {
        const string HttpScheme = "http";
        const string HttpsScheme = "https";
        const string UnixScheme = "unix";

        public static HttpClient GetHttpClient(Uri serverUri)
        {
            HttpClient client;

            if (serverUri.Scheme.Equals(HttpScheme, StringComparison.OrdinalIgnoreCase) || serverUri.Scheme.Equals(HttpsScheme, StringComparison.OrdinalIgnoreCase))
            {
                client = new HttpClient();
                return client;
            }

            if (serverUri.Scheme.Equals(UnixScheme, StringComparison.OrdinalIgnoreCase))
            {
                client = new HttpClient(new HttpUdsMessageHandler(serverUri));
                return client;
            }

            throw new InvalidOperationException("ProviderUri scheme is not supported");
        }

        public static string GetBaseUrl(Uri serverUri)
        {
            if (serverUri.Scheme.Equals(UnixScheme, StringComparison.OrdinalIgnoreCase))
            {
                return $"{HttpScheme}://{serverUri.Segments.Last()}";
            }

            return serverUri.OriginalString;
        }
    }
}
