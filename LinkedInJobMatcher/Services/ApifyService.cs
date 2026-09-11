using LinkedInJobMatcher.Models;
using System.Text;
using System.Text.Json;

namespace LinkedInJobMatcher.Services
{
    public class ApifyService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiToken;

        // Actor id from your URL: harvestapi~linkedin-post-search
        private const string ActorId = "harvestapi~linkedin-post-search";

        public ApifyService(HttpClient httpClient, string apiToken)
        {
            _httpClient = httpClient;
            _apiToken = apiToken;
        }

        /// <summary>
        /// Runs the LinkedIn post-search Actor synchronously and returns the
        /// resulting dataset items directly (no separate run + poll + fetch needed).
        /// Note: run-sync-get-dataset-items has an upper bound on run time
        /// (Apify typically caps this around 5 minutes) — for larger/slower
        /// scrapes, switch to the async run + poll pattern instead.
        /// </summary>
        public async Task<List<LinkedInPost>> FetchPostsAsync(
            List<string> searchQueries,
            int maxPosts = 200,
            string? postedLimit = "week")
        {
            var url =
                $"https://api.apify.com/v2/acts/{ActorId}/run-sync-get-dataset-items?token={_apiToken}";

            var payload = new
            {
                searchQueries,
                maxPosts,
                postedLimit,
                scrapeComments = false,
                scrapeReactions = false,
                postNestedComments = false,
                postNestedReactions = false
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Apify request failed: {response.StatusCode} - {body}");
            }

            var posts = JsonSerializer.Deserialize<List<LinkedInPost>>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return posts ?? new List<LinkedInPost>();
        }
    }
}