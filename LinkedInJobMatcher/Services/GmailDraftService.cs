using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LinkedInJobMatcher.Services;

public class GmailDraftService
{
    private readonly HttpClient _httpClient;

    public GmailDraftService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Creates a Gmail draft using the standard (GA) Gmail REST API.
    /// No MCP / Developer Preview enrollment required.
    /// </summary>
    public async Task<string> CreateDraftAsync(
        string accessToken,
        string toAddress,
        string fromAddress,
        string subject,
        string bodyText)
    {
        // 1. Build a raw RFC 2822 MIME message
        var mimeMessage =
            $"From: {fromAddress}\r\n" +
            $"To: {toAddress}\r\n" +
            $"Subject: {subject}\r\n" +
            "Content-Type: text/plain; charset=\"UTF-8\"\r\n" +
            "\r\n" +
            bodyText;

        // 2. Base64url-encode it (Gmail requires URL-safe, no padding)
        var rawEncoded = Base64UrlEncode(mimeMessage);

        // 3. Build the request payload
        var payload = new
        {
            message = new
            {
                raw = rawEncoded
            }
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // 4. POST to the Gmail drafts endpoint
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://gmail.googleapis.com/gmail/v1/users/me/drafts")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Gmail draft creation failed: {response.StatusCode} - {responseBody}");
        }

        return responseBody; // contains the new draft's id + message id
    }

    /// <summary>
    /// Creates a Gmail draft with a PDF (or any binary file) attached, as a
    /// multipart/mixed MIME message.
    /// </summary>
    public async Task<string> CreateDraftWithAttachmentAsync(
        string accessToken,
        string toAddress,
        string fromAddress,
        string subject,
        string bodyText,
        string attachmentFilePath)
    {
        if (!File.Exists(attachmentFilePath))
            throw new FileNotFoundException("Attachment file not found.", attachmentFilePath);

        var attachmentBytes = await File.ReadAllBytesAsync(attachmentFilePath);
        var attachmentFileName = Path.GetFileName(attachmentFilePath);
        var contentType = GetContentType(attachmentFileName);

        var boundary = $"boundary_{Guid.NewGuid():N}";

        var attachmentBase64 = Convert.ToBase64String(attachmentBytes);
        var attachmentBase64Wrapped = WrapBase64Lines(attachmentBase64, 76);

        var mimeBuilder = new StringBuilder();
        mimeBuilder.Append($"From: {fromAddress}\r\n");
        mimeBuilder.Append($"To: {toAddress}\r\n");
        mimeBuilder.Append($"Subject: {subject}\r\n");
        mimeBuilder.Append("MIME-Version: 1.0\r\n");
        mimeBuilder.Append($"Content-Type: multipart/mixed; boundary=\"{boundary}\"\r\n");
        mimeBuilder.Append("\r\n");

        // Body part
        mimeBuilder.Append($"--{boundary}\r\n");
        mimeBuilder.Append("Content-Type: text/plain; charset=\"UTF-8\"\r\n");
        mimeBuilder.Append("\r\n");
        mimeBuilder.Append(bodyText);
        mimeBuilder.Append("\r\n\r\n");

        // Attachment part
        mimeBuilder.Append($"--{boundary}\r\n");
        mimeBuilder.Append($"Content-Type: {contentType}; name=\"{attachmentFileName}\"\r\n");
        mimeBuilder.Append($"Content-Disposition: attachment; filename=\"{attachmentFileName}\"\r\n");
        mimeBuilder.Append("Content-Transfer-Encoding: base64\r\n");
        mimeBuilder.Append("\r\n");
        mimeBuilder.Append(attachmentBase64Wrapped);
        mimeBuilder.Append("\r\n");

        mimeBuilder.Append($"--{boundary}--");

        var rawEncoded = Base64UrlEncode(mimeBuilder.ToString());

        var payload = new { message = new { raw = rawEncoded } };
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://gmail.googleapis.com/gmail/v1/users/me/drafts")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Gmail draft creation failed: {response.StatusCode} - {responseBody}");
        }

        return responseBody;
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };
    }

    private static string WrapBase64Lines(string base64, int lineLength)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < base64.Length; i += lineLength)
        {
            sb.Append(base64.Substring(i, Math.Min(lineLength, base64.Length - i)));
            sb.Append("\r\n");
        }
        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var base64 = Convert.ToBase64String(bytes);

        // Convert standard base64 to base64url and strip padding
        return base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}