namespace OctoPublisher.Tests.Helpers;

public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _asyncHandler;

    public List<HttpRequestMessage> CapturedRequests { get; } = [];

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> syncHandler) => _asyncHandler = (req, _) => Task.FromResult(syncHandler(req));

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> asyncHandler) => _asyncHandler = asyncHandler;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CapturedRequests.Add(request);
        return await _asyncHandler(request, cancellationToken);
    }
}
