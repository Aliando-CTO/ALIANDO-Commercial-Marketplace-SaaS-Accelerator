using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Marketplace.SaaS.Accelerator.Services.Services;

/// <summary>
/// SAGitReleasesService Service.
/// </summary>
public class SAGitReleasesService : ISAGitReleasesService
{
    private const string ReleasesUrl =
        "https://api.github.com/repos/Azure/Commercial-Marketplace-SaaS-Accelerator/releases/latest";

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<SAGitReleasesService> logger;

    public SAGitReleasesService(
        IHttpClientFactory httpClientFactory,
        ILogger<SAGitReleasesService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    /// <summary>
    /// Gets the latest release number.
    /// </summary>
    /// <returns> Release Version.</returns>
    public async Task<string> GetLatestReleaseFromGitHubAsync()
    {
        try
        {
            var client = this.httpClientFactory.CreateClient(nameof(SAGitReleasesService));
            client.DefaultRequestHeaders.UserAgent.TryParseAdd("SaaSAccelerator");

            var response = await client.GetAsync(ReleasesUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var releaseInfo = JsonSerializer.Deserialize<JsonElement>(content);
            return releaseInfo.GetProperty("tag_name").GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Unable to get latest SA release from GitHub");
            return string.Empty;
        }
    }
}
