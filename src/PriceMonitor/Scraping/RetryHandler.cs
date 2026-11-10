using System.Net;

namespace PriceMonitor.Scraping;

/// <summary>
/// Retries transient HTTP failures (network errors, 5xx, 408, 429) with exponential back-off: 2s, 4s.
/// </summary>
public class RetryHandler(ILogger<RetryHandler> logger) : DelegatingHandler
{
    private const int MaxAttempts = 3;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (!IsTransient(response.StatusCode) || attempt == MaxAttempts)
                {
                    return response;
                }

                logger.LogWarning("{Url} returned {Status}, retrying ({Attempt}/{Max})",
                    request.RequestUri, (int)response.StatusCode, attempt, MaxAttempts - 1);
                response.Dispose();
            }
            catch (HttpRequestException ex) when (attempt < MaxAttempts)
            {
                logger.LogWarning("{Url} failed: {Error}, retrying ({Attempt}/{Max})",
                    request.RequestUri, ex.Message, attempt, MaxAttempts - 1);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
        }
    }

    public static bool IsTransient(HttpStatusCode status) =>
        (int)status >= 500 || status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;
}
