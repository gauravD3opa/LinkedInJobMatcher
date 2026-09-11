using LinkedInJobMatcher.Models;
using LinkedInJobMatcher.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LinkedInJobMatcher
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            // 1. Google OAuth (for Gmail draft creation)
            var gmailAuthService = new GmailAuthService(configuration, loggerFactory);
            var accessToken = await gmailAuthService.GetAccessTokenAsync();
            Console.WriteLine("Google OAuth token acquired.");

            var fromAddress = configuration["Gmail:FromAddress"] ?? "me";
            var resumePdfPath = configuration["Resume:PdfPath"]
                ?? Path.Combine("Data", "Gaurav_Deopa_Resume.pdf");

            // 2. Get LinkedIn posts — either fetch fresh from Apify, or reuse a
            //    previously saved fetch file.
            //    Usage:
            //      dotnet run                                   -> fetch fresh from Apify
            //      dotnet run -- --from-file path/to/posts.json  -> reuse saved data
            List<LinkedInPost> posts;
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");

            if (args.Length >= 2 && args[0] == "--from-file")
            {
                var sourcePath = Path.Combine("Data", "dataset_linkedin-post-search_2026-08-25_04-57-32-767.json");
                Console.WriteLine($"Loading previously fetched posts from {sourcePath}");

                var savedJson = await File.ReadAllTextAsync(sourcePath);
                posts = JsonSerializer.Deserialize<List<LinkedInPost>>(
                    savedJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new List<LinkedInPost>();

                Console.WriteLine($"Loaded {posts.Count} posts from file.");
            }
            else
            {
                var apifyToken = configuration["Apify:ApiToken"]
                    ?? throw new InvalidOperationException("Apify:ApiToken is missing from configuration.");

                using var apifyHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(6) };
                var apifyService = new ApifyService(apifyHttpClient, apifyToken);

                posts = await apifyService.FetchPostsAsync(
                    searchQueries: new List<string> { "Hiring dotnet india" },
                    maxPosts: 200,
                    postedLimit: "week");

                Console.WriteLine($"Fetched {posts.Count} posts from Apify.");

                // Save raw fetch results so this run can be replayed later via --from-file
                var apifyLogPath = Path.Combine("Output", $"apify-posts_{timestamp}.json");
                var apifyLogDirectory = Path.GetDirectoryName(apifyLogPath);
                if (!string.IsNullOrEmpty(apifyLogDirectory))
                    Directory.CreateDirectory(apifyLogDirectory);

                await File.WriteAllTextAsync(
                    apifyLogPath,
                    JsonSerializer.Serialize(posts, new JsonSerializerOptions { WriteIndented = true }));

                Console.WriteLine($"Apify fetch results saved to {apifyLogPath}");
            }

            // 3. Common email template sent to every unique matched recruiter
            var lastWorkingDay = "23-Oct-2026";
            var commonSubject = "Job Application: Full-Stack .NET Engineer | Gaurav Deopa";
            var commonBody =
                $"""
    Hi,

    I hope you are doing well.

    I came across your post regarding hiring opportunities and wanted to reach out directly, as I am currently looking for new opportunities.

    I am a Full-Stack Engineer with over 4 years of experience specializing in .NET, ASP.NET Core, C#, SQL Server, Oracle PL/SQL, and Azure. My last working day at my current organization is {lastWorkingDay}.

    I have attached my resume for your review. I would appreciate it if you could consider my profile for any suitable openings or kindly share it within your network.

    Thank you for your time and consideration. I look forward to hearing from you.

    Best regards,
    Gaurav Deopa
    +91 7983540549
    gauravdeopa4@gmail.com
    """;

            // 4. Analyze posts against resume + create Gmail drafts for unique, un-contacted recruiters
            var resumeTextPath = Path.Combine("Data", "resume.txt");
            var draftLogPath = Path.Combine("Output", $"drafts-log_{timestamp}.json");
            var sentRecruitersPath = Path.Combine("Data", "sent-recruiters.json");

            using var ollamaHttpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:11434"),
                Timeout = TimeSpan.FromMinutes(5)
            };
            var ollamaService = new OllamaService(ollamaHttpClient);

            using var gmailHttpClient = new HttpClient();
            var gmailDraftService = new GmailDraftService(gmailHttpClient);

            var sentRecruitersStore = new SentRecruitersStore(sentRecruitersPath);
            Console.WriteLine($"Previously contacted recruiters on file: {sentRecruitersStore.Count}");

            var processor = new JobProcessor(ollamaService, gmailDraftService);

            await processor.ProcessAsync(
                posts,
                resumeTextPath,
                accessToken,
                fromAddress,
                draftLogPath,
                resumePdfPath,
                commonSubject,
                commonBody,
                sentRecruitersStore);
        }
    }
}