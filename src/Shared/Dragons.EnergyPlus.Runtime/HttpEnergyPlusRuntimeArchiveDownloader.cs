#if NET48
using System.Net.Http;
#endif

namespace Dragons.EnergyPlus.Runtime;

internal sealed class HttpEnergyPlusRuntimeArchiveDownloader : IEnergyPlusRuntimeArchiveDownloader
{
    private const int BufferSize = 81920;
    private static readonly HttpClient Client = CreateClient();

    public async Task DownloadAsync(
        Uri sourceUri,
        string destinationPartialPath,
        IProgress<EnergyPlusRuntimeDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        sourceUri = sourceUri ?? throw new ArgumentNullException(nameof(sourceUri));

        if (!sourceUri.IsAbsoluteUri || sourceUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("The archive source must be an absolute HTTPS URI.", nameof(sourceUri));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        using var response = await Client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

#if NET7_0_OR_GREATER
        using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
#endif
        using var destination = new FileStream(
            destinationPartialPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[BufferSize];
        long received = 0;
        var total = response.Content.Headers.ContentLength;
        progress?.Report(new EnergyPlusRuntimeDownloadProgress(received, total));
        while (true)
        {
#if NET7_0_OR_GREATER
            var count = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
#else
            var count = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
#endif
            if (count == 0)
            {
                break;
            }

#if NET7_0_OR_GREATER
            await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
#else
            await destination.WriteAsync(buffer, 0, count, cancellationToken).ConfigureAwait(false);
#endif
            received += count;
            progress?.Report(new EnergyPlusRuntimeDownloadProgress(received, total));
        }

#if NET7_0_OR_GREATER
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
#else
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
#endif
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Dragons-EnergyPlus-Runtime/0.1");
        return client;
    }
}
