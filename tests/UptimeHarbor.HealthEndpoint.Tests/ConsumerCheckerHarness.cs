using System.Net;
using Microsoft.Extensions.Options;
using UptimeHarbor.Application.Monitoring;
using UptimeHarbor.Infrastructure.Monitoring;
using Xunit;

namespace UptimeHarbor.HealthEndpoint.Tests;

internal class ConsumerCheckerHarness : IDisposable
{
    internal const string ENDPOINT_URL = "https://isolated.invalid/health";
    private readonly HttpClient client;

    internal ConsumerCheckerHarness(byte[] payload, bool advertiseLength = true)
    {
        client = new HttpClient(new PayloadHandler(payload, advertiseLength));
        Checker = new HttpMonitoringEndpointChecker(new ClientFactory(client),
            Options.Create(new MonitoringOptions { TimeoutSeconds = 10 }), TimeProvider.System);
    }

    internal HttpMonitoringEndpointChecker Checker { get; }

    public void Dispose()
    {
        client.Dispose();
    }

    private class ClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal(HttpMonitoringEndpointChecker.HTTP_CLIENT_NAME, name);

            return client;
        }
    }

    private class PayloadHandler(byte[] payload, bool advertiseLength) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(ENDPOINT_URL, request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new PayloadContent(payload, advertiseLength)
            });
        }
    }

    private class PayloadContent(byte[] payload, bool advertiseLength) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return stream.WriteAsync(payload).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = payload.Length;

            return advertiseLength;
        }
    }
}
