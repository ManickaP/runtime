// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Http.Functional.Tests
{
    public class HttpSession
    {
        private readonly HttpClient _httpClient;

        public HttpSession(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public CookieContainer CookieContainer { get; set; } = null!;

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
            if (CookieContainer is not null)
            {
                request.Options.Set(new HttpRequestOptionsKey<CookieContainer>(nameof(CookieContainer)), CookieContainer);
            }

            return _httpClient.SendAsync(request, completionOption, cancellationToken);
        }
    }
}