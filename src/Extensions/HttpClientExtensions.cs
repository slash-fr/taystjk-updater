using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TaystJkUpdater.Extensions
{
    // Source:  https://stackoverflow.com/a/46497896
    // License: CC BY-SA 3.0 / CC BY-SA 4.0 (possibly)
    // Authors: Bruno Zell and other StackOverflow contributors
    //          With slight modifications from Slash
    public static class HttpClientExtensions
    {
        /// <param name="progress">Progress report, from 0 to 100 (percent)</param>
        /// <exception cref="HttpRequestException"></exception>
        public static async Task DownloadAsync(
            this HttpClient client,
            string requestUri,
            Stream destination,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            // Get the HTTP headers first to examine the content length
            using (HttpResponseMessage response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                long? contentLength = response.Content.Headers.ContentLength;

                using (Stream download = await response.Content.ReadAsStreamAsync())
                {

                    // Ignore progress reporting when no progress reporter was passed
                    // or when the content length is unknown
                    if (progress == null || !contentLength.HasValue)
                    {
                        await download.CopyToAsync(destination);
                        return;
                    }

                    // Convert absolute progress (bytes downloaded) into relative progress (0% - 100%)
                    int progressPercentage = 0;
                    progress.Report(0);
                    Progress<long> relativeProgress = new Progress<long>(totalBytes => {
                        int newProgressPercentage = (int)((float)totalBytes / contentLength.Value * 100);
                        if (newProgressPercentage > progressPercentage) // Avoid spamming progress reports
                        {
                            progressPercentage = newProgressPercentage;
                            progress.Report(progressPercentage);
                        }
                    });

                    // Use our extension method to report progress while downloading
                    await download.CopyToAsync(destination, 81920, relativeProgress, cancellationToken);

                    progress.Report(100);
                }
            }
        }
    }
}
