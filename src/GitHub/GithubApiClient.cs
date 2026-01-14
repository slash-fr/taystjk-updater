using LightJson.Serialization;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TaystJkUpdater.GitHub
{
    internal class GithubApiClient
    {
        private readonly HttpClient httpClient;

        public GithubApiClient()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "TaystJK Updater");
        }

        /// <summary>
        /// Gets info about the latest TaystJK release, from the GitHub API. Does not download any assets.
        /// </summary>
        /// <exception cref="GithubApiException"></exception>
        /// <exception cref="TooManyRequestsException"></exception>
        public async Task<ReleaseInfo> GetLatestTaystJkReleaseInfo(CancellationToken cancellationToken)
        {
            Uri requestUri = new Uri("http://localhost/latest.php");
            // TODO: "https://api.github.com/repos/taysta/TaystJK/releases/tags/latest"

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

            LightJson.JsonValue parsedResponse;

            string responseAsString = await response.Content.ReadAsStringAsync();
            try
            {
                parsedResponse = LightJson.JsonValue.Parse(responseAsString);
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

            return new ReleaseInfo(parsedResponse["created_at"], parsedResponse["target_commitish"]);
        }
    }
}
