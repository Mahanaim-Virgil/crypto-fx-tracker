using System.Net;
using System.Text;

namespace CryptoFxTracker.UnitTests.Fakes;

// Gerçek ağa çıkmadan HttpClient'ın gönderdiği isteği kaydeder ve önceden belirlenen yanıtı döner.
public sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK, string responseBody = """{"ok":true}""")
    : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri, body));

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
    }
}

public sealed record RecordedRequest(HttpMethod Method, Uri? Uri, string? Body);
