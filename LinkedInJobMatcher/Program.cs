using LinkedInJobMatcher.Data;
using LinkedInJobMatcher.Models;
using LinkedInJobMatcher.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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

            // Set up database
            var connectionString = configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string 'Default' is missing.");

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
         .UseSqlite(connectionString)
                .Options;

            using var dbContext = new AppDbContext(dbOptions);

            // Run database migrations
            Console.WriteLine("Setting up database...");
            await dbContext.Database.MigrateAsync();
            Console.WriteLine("Database ready.");
            Console.WriteLine();

            // Parse command
            var command = args.Length > 0 ? args[0].ToLowerInvariant() : "all";

            // Get email template (used by send and all)
            var lastWorkingDay = "23-Oct-2026";
            var commonSubject = "Job Application: Full-Stack .NET Engineer | Gaurav Deopa";
            var commonBody =
                "Hi,\n\n" +
                "I hope you are doing well.\n\n" +
                "I came across your post regarding hiring opportunities and wanted to reach out directly, as I am currently looking for new opportunities.\n\n" +
         "I am a Full-Stack Engineer with over 4 years of experience specializing in .NET, ASP.NET Core, C#, SQL Server, Oracle PL/SQL, and Azure. My last working day at my current organization is " + lastWorkingDay + ".\n\n" +
                "I have attached my resume for your review. I would appreciate it if you could consider my profile for any suitable openings or kindly share it within your network.\n\n" +
                "Thank you for your time and consideration. I look forward to hearing from you.\n\n" +
                "Best regards,\n" +
                "Gaurav Deopa\n" +
         "+91 7983540549\n" +
         "gauravdeopa4@gmail.com\n";

            var fromAddress = configuration["Gmail:FromAddress"] ?? "me";
            var resumePdfPath = configuration["Resume:PdfPath"]
         ?? Path.Combine("Data", "Gaurav_Deopa_Resume.pdf");
            var resumeTextPath = Path.Combine("Data", "resume.txt");

            switch (command)
            {
                case "fetch":
                    await ExecuteFetchAsync(configuration, dbContext);
                    break;

                case "analyze":
                    await ExecuteAnalyzeAsync(configuration, dbContext, resumeTextPath);
                    break;

                case "send":
                    await ExecuteSendAsync(configuration, dbContext, fromAddress, resumePdfPath, commonSubject, commonBody);
                    break;

                case "all":
                    await ExecuteFetchAsync(configuration, dbContext);
                    await ExecuteAnalyzeAsync(configuration, dbContext, resumeTextPath);
                    await ExecuteSendAsync(configuration, dbContext, fromAddress, resumePdfPath, commonSubject, commonBody);
                    break;

                case "import":
                    await ExecuteImportAsync(dbContext);
                    break;

                case "export-no-email":
                    await ExecuteExportNoEmailAsync(dbContext);
                    break;

                default:
                    Console.WriteLine($"Unknown command: {command}");
                    Console.WriteLine();
                    Console.WriteLine("Available commands:");
                    Console.WriteLine("  dotnet run -- fetch       Fetch from Apify");
                    Console.WriteLine("  dotnet run -- analyze     Analyze posts with Ollama");
                    Console.WriteLine("  dotnet run -- send        Send drafts via Gmail");
                    Console.WriteLine("  dotnet run -- all         Run fetch, analyze, send");
                    Console.WriteLine("  dotnet run -- import      One-time import from JSON files");
                    Console.WriteLine("  dotnet run -- export-no-email    Export posts without email");
                    return;
            }
        }

        static async Task ExecuteFetchAsync(IConfiguration configuration, AppDbContext dbContext)
        {
            var apifyToken = configuration["Apify:ApiToken"]
                ?? throw new InvalidOperationException("Apify:ApiToken is missing from configuration.");

            using var apifyHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(6) };
            var apifyService = new ApifyService(apifyHttpClient, apifyToken);

            var fetchStage = new FetchStage(apifyService, dbContext);
            await fetchStage.ExecuteAsync(
         searchQueries: new List<string> { "Hiring dotnet india" },
         maxPosts: 200,
                postedLimit: "week");
        }

        static async Task ExecuteAnalyzeAsync(IConfiguration configuration, AppDbContext dbContext, string resumeTextPath)
        {
            if (!File.Exists(resumeTextPath))
                throw new FileNotFoundException($"Resume file not found: {resumeTextPath}");

            var resumeText = await File.ReadAllTextAsync(resumeTextPath);

            using var ollamaHttpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:11434"),
                Timeout = TimeSpan.FromMinutes(5)
            };
            var ollamaService = new OllamaService(ollamaHttpClient);

            var analyzeStage = new AnalyzeStage(ollamaService, dbContext, resumeText);
            await analyzeStage.ExecuteAsync();
        }

        static async Task ExecuteSendAsync(IConfiguration configuration, AppDbContext dbContext, string fromAddress, string resumePdfPath, string commonSubject, string commonBody)
        {
            if (!File.Exists(resumePdfPath))
                throw new FileNotFoundException($"Resume PDF not found: {resumePdfPath}");

            var gmailAuthService = new GmailAuthService(configuration, LoggerFactory.Create(b => b.AddConsole()));
            using var gmailHttpClient = new HttpClient();
            var gmailDraftService = new GmailDraftService(gmailHttpClient);

            var sendStage = new SendStage(
                gmailDraftService,
         gmailAuthService,
         dbContext,
         fromAddress,
         resumePdfPath,
                commonSubject,
                commonBody);

            await sendStage.ExecuteAsync();
        }

        static async Task ExecuteImportAsync(AppDbContext dbContext)
        {
            var importStage = new ImportStage(dbContext);
            await importStage.ExecuteAsync();
        }

        static async Task ExecuteExportNoEmailAsync(AppDbContext dbContext)
        {
            var exportStage = new ExportStage(dbContext);
            await exportStage.ExecuteAsync();
        }
    }
}
