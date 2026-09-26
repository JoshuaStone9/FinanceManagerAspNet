using System.Net;
using System.Text;
using MoveInPlanner.Models.Results;

namespace MoveInPlanner.Services.ProductMetadata;

public sealed class ProductMetadataService(HttpClient httpClient, IEnumerable<IRetailerProductMetadataProvider> providers) : IProductMetadataService
{
    private const int MaximumHtmlCharacters = 2_000_000;
    private const int MaximumRedirects = 5;

    private readonly IReadOnlyList<IRetailerProductMetadataProvider> _providers =
        providers.ToList();

    public async Task<ProductMetadataResult> GetFromUrlAsync(
        string productUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productUrl) || !Uri.TryCreate(productUrl.Trim(), UriKind.Absolute, out var requestedUri) ||
            !RetailerUrlMatcher.UsesHttp(requestedUri))
        {
            return Failure("Enter a valid product URL.");
        }

        var provider = _providers.FirstOrDefault(candidate =>
            candidate.Supports(requestedUri));

        if (provider is null)
        {
            return Failure(
                "This retailer is not supported yet. Enter an Amazon or TikTok Shop product URL.");
        }

        try
        {
            var page = await FetchPageAsync(requestedUri, provider, cancellationToken);

            var commonMetadata = ProductMetadataHtmlReader.ReadCommon(page.Html);
            var metadata = provider.Enrich(page, commonMetadata);

            if (string.IsNullOrWhiteSpace(metadata.Name) &&
                string.IsNullOrWhiteSpace(metadata.ImageUrl) &&
                metadata.Price is null)
            {
                return Failure(
                    $"{provider.RetailerName} did not expose product metadata on this request. " +
                    "The fields can still be entered manually.");
            }

            return new ProductMetadataResult
            {
                Success = true,
                Name = metadata.Name,
                Price = metadata.Price,
                ImageUrl = metadata.ImageUrl,
                Retailer = provider.RetailerName,
                ResolvedUrl = (metadata.ResolvedUri ?? page.ResolvedUri).ToString()
            };
        }
        catch (ProductMetadataImportException exception)
        {
            return Failure(exception.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(
                $"The {provider.RetailerName} request timed out. Try again or enter the details manually.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return Failure(
                $"{provider.RetailerName} could not be reached. Check the URL or enter the details manually.");
        }
        catch (Exception)
        {
            return Failure(
                "The product details could not be imported. Enter them manually.");
        }
    }

    private async Task<ProductMetadataPage> FetchPageAsync(
        Uri requestedUri,
        IRetailerProductMetadataProvider provider,
        CancellationToken cancellationToken)
    {
        var currentUri = requestedUri;
        var redirects = new List<Uri>();

        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            request.Headers.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 Chrome/124 Safari/537.36");
            request.Headers.TryAddWithoutValidation(
                "Accept-Language",
                "en-GB,en;q=0.9");
            request.Headers.TryAddWithoutValidation(
                "Accept",
                "text/html,application/xhtml+xml");

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (IsRedirect(response.StatusCode))
            {
                if (redirects.Count >= MaximumRedirects)
                {
                    throw new ProductMetadataImportException(
                        $"{provider.RetailerName} redirected too many times.");
                }

                var location = response.Headers.Location;
                if (location is null)
                {
                    throw new ProductMetadataImportException(
                        $"{provider.RetailerName} returned a redirect without a destination.");
                }

                var nextUri = location.IsAbsoluteUri
                    ? location
                    : new Uri(currentUri, location);

                if (!provider.AllowsPageUri(nextUri))
                {
                    throw new ProductMetadataImportException(
                        $"{provider.RetailerName} redirected outside an approved domain.");
                }

                redirects.Add(nextUri);
                currentUri = nextUri;
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ProductMetadataImportException(
                    $"{provider.RetailerName} returned HTTP {(int)response.StatusCode}. " +
                    "Enter the details manually or try again later.");
            }

            var html = await ReadLimitedHtmlAsync(response, cancellationToken);
            if (string.IsNullOrWhiteSpace(html))
            {
                throw new ProductMetadataImportException(
                    $"{provider.RetailerName} returned an empty page.");
            }

            return new ProductMetadataPage(
                RequestedUri: requestedUri,
                ResolvedUri: response.RequestMessage?.RequestUri ?? currentUri,
                Html: html,
                Redirects: redirects);
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.MovedPermanently or
        HttpStatusCode.Redirect or
        HttpStatusCode.RedirectMethod or
        HttpStatusCode.TemporaryRedirect or
        HttpStatusCode.PermanentRedirect;

    private static async Task<string> ReadLimitedHtmlAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var buffer = new char[16_384];
        var result = new StringBuilder();

        while (result.Length < MaximumHtmlCharacters)
        {
            var remaining = Math.Min(
                buffer.Length,
                MaximumHtmlCharacters - result.Length);

            var read = await reader.ReadAsync(
                buffer.AsMemory(0, remaining),
                cancellationToken);

            if (read == 0) break;
            result.Append(buffer, 0, read);
        }

        return result.ToString();
    }

    private static ProductMetadataResult Failure(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    private sealed class ProductMetadataImportException(string message)
        : Exception(message);
}
