using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;

namespace LinkedInJobMatcher.Services;

public class GmailAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;

    public GmailAuthService(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Runs the local-loopback OAuth flow and returns a valid Gmail access token.
    /// </summary>
    public async Task<string> GetAccessTokenAsync()
    {
        var clientId = _configuration["Google:ClientId"]
            ?? throw new InvalidOperationException("Google:ClientId is missing.");
        var clientSecret = _configuration["Google:ClientSecret"]
            ?? throw new InvalidOperationException("Google:ClientSecret is missing.");

        const string redirectUri = "http://localhost:1179/callback";
        const string scopeString =
            "https://www.googleapis.com/auth/gmail.readonly https://www.googleapis.com/auth/gmail.compose";

        var authUri = new Uri("https://accounts.google.com/o/oauth2/v2/auth" +
                              $"?client_id={HttpUtility.UrlEncode(clientId)}" +
                              $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
                              $"&response_type=code" +
                              $"&scope={HttpUtility.UrlEncode(scopeString)}" +
                              $"&access_type=offline" +
                              $"&prompt=consent" +
                              $"&state=linkedin_job_matcher");

        var code = await GetAuthorizationCodeAsync(authUri, redirectUri);

        if (string.IsNullOrEmpty(code))
        {
            throw new InvalidOperationException("Failed to acquire Google authorization code.");
        }

        using var tokenClient = new HttpClient();
        var tokenRequestParams = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri
        };

        var tokenResponse = await tokenClient.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(tokenRequestParams));

        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Token exchange failed: {tokenJson}");
        }

        using var jsonDoc = JsonDocument.Parse(tokenJson);
        var accessToken = jsonDoc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Access token missing from Google response.");

        return accessToken;
    }

    private async Task<string?> GetAuthorizationCodeAsync(Uri authorizationUrl, string redirectUri)
    {
        Console.WriteLine();
        Console.WriteLine("Opening Google OAuth...");
        Console.WriteLine(authorizationUrl);
        Console.WriteLine();

        var listenerPrefix = new Uri(redirectUri).GetLeftPart(UriPartial.Authority);
        if (!listenerPrefix.EndsWith("/"))
            listenerPrefix += "/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(listenerPrefix);

        try
        {
            listener.Start();
            Console.WriteLine($"Waiting for OAuth callback at {listenerPrefix}");

            OpenBrowser(authorizationUrl);

            var callback = await listener.GetContextAsync();
            var query = HttpUtility.ParseQueryString(callback.Request.Url?.Query ?? string.Empty);

            var code = query["code"];
            var error = query["error"];

            const string html = """
                <html>
                    <body>
                        <h2>Gmail authentication successful.</h2>
                        <p>You can close this browser window.</p>
                    </body>
                </html>
                """;

            var bytes = Encoding.UTF8.GetBytes(html);
            callback.Response.ContentType = "text/html";
            callback.Response.ContentLength64 = bytes.Length;
            await callback.Response.OutputStream.WriteAsync(bytes);
            callback.Response.Close();

            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"OAuth error: {error}");
                return null;
            }

            if (string.IsNullOrEmpty(code))
            {
                Console.WriteLine("No authorization code received.");
                return null;
            }

            Console.WriteLine("Authorization code received.");
            return code;
        }
        finally
        {
            if (listener.IsListening)
                listener.Stop();
        }
    }

    private static void OpenBrowser(Uri url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url.ToString(),
            UseShellExecute = true
        });
    }
}