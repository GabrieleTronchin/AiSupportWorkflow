namespace LlmFlows.Testing;

using System.Collections.Concurrent;
using System.Net.Sockets;

/// <summary>
/// Routes outgoing HTTP requests to in-process services by host name (e.g. <c>http://engine</c> → engine TestServer).
/// A host can be marked down to simulate an unavailable service: requests then fail like a refused connection.
/// </summary>
public sealed class HostRoutingHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, HttpMessageInvoker> _hosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _down = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string host, HttpMessageHandler handler) => _hosts[host] = new HttpMessageInvoker(handler, disposeHandler: false);

    public void SetDown(string host, bool down = true) => _down[host] = down;

    public bool IsDown(string host) => _down.GetValueOrDefault(host);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var host = request.RequestUri?.Host ?? "";

        if (IsDown(host) || !_hosts.TryGetValue(host, out var invoker))
        {
            throw new HttpRequestException(
                $"No connection could be made to '{host}'.",
                new SocketException((int)SocketError.ConnectionRefused));
        }

        return invoker.SendAsync(request, cancellationToken);
    }
}
