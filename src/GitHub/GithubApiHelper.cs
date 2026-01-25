using LightJson;
using LightJson.Serialization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TaystJkUpdater.GitHub
{
    internal static class GithubApiHelper
    {
        /// <summary>
        /// Gets info about the specified release, from the GitHub API. Does not download any assets.
        /// </summary>
        /// <param name="httpClient">HTTP Client to use for the request</param>
        /// <param name="requestUri">URI pointing to a GitHub API endpoint that returns info about a release.</param>
        /// <exception cref="GithubApiException"></exception>
        /// <exception cref="TooManyRequestsException"></exception>
        public static async Task<ReleaseInfo> GetReleaseInfo(
            HttpClient httpClient,
            Uri requestUri,
            CancellationToken cancellationToken)
        {
            SetApiHeaders(httpClient);
            HttpResponseMessage response = await GetAsync(httpClient, requestUri, cancellationToken);
            string responseAsString = await response.Content.ReadAsStringAsync();
            return ParseResponse(requestUri, responseAsString);
        }

        private static void SetApiHeaders(HttpClient httpClient)
        {
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "TaystJK Updater");
        }

        /// <exception cref="GithubApiException"></exception>
        /// <exception cref="TooManyRequestsException"></exception>
        private static async Task<HttpResponseMessage> GetAsync(
            HttpClient httpClient,
            Uri requestUri,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response;

            try
            {
                response = await httpClient.GetAsync(requestUri, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                throw new GithubApiException(requestUri, "Network error", exception);
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                if ((int)response.StatusCode == 429) // Too Many Requests
                {
                    throw new TooManyRequestsException(requestUri);
                }

                throw new GithubApiException(
                    requestUri,
                    "Failed with status: " + (int)response.StatusCode + " (" + response.StatusCode + ")."
                );
            }

            return response;
        }

        /// <param name="requestUri">Request URI (for informative purposes)</param>
        /// <param name="responseAsString">A string (hopefully) containing JSON</param>
        /// <exception cref="GithubApiException"></exception>
        private static ReleaseInfo ParseResponse(Uri requestUri, string responseAsString)
        {
            JsonValue parsedResponse;

            try
            {
                parsedResponse = JsonValue.Parse(responseAsString);
            }
            catch (JsonParseException exception)
            {
                throw new GithubApiException(requestUri, "Received invalid JSON", exception);
            }

            if (!parsedResponse["created_at"].IsDateTime)
            {
                throw new GithubApiException(
                    requestUri,
                    "Invalid \"created_at\" value (" + parsedResponse["created_at"] + ")."
                );
            }

            if (!parsedResponse["target_commitish"].IsString)
            {
                throw new GithubApiException(
                    requestUri,
                    "Invalid \"target_commitish\" value (" + parsedResponse["target_commitish"] + ")."
                );
            }

            if (!parsedResponse["assets"].IsJsonArray)
            {
                throw new GithubApiException(
                    requestUri,
                    "Invalid \"assets\" value: " + parsedResponse["assets"]
                );
            }

            IDictionary<string, string> downloadUrlByAssetName = new Dictionary<string, string>();

            foreach (JsonValue asset in parsedResponse["assets"].AsJsonArray)
            {
                if (!asset["name"].IsString || String.IsNullOrEmpty(asset["name"]))
                {
                    throw new GithubApiException(
                        requestUri,
                        "Invalid asset \"name\": " + asset["name"]
                    );
                }

                if (!asset["browser_download_url"].IsString ||
                    !Uri.IsWellFormedUriString(asset["browser_download_url"], UriKind.Absolute))
                {
                    throw new GithubApiException(
                        requestUri,
                        "Invalid asset \"browser_download_url\": " + asset["browser_download_url"]
                    );
                }                

                downloadUrlByAssetName[asset["name"]] = asset["browser_download_url"];
            }

            return new ReleaseInfo(
                parsedResponse["created_at"],
                parsedResponse["target_commitish"],
                downloadUrlByAssetName
            );
        }
    }
}
