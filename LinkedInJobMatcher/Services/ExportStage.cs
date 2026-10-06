using LinkedInJobMatcher.Data;
using LinkedInJobMatcher.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LinkedInJobMatcher.Services
{
    public class ExportStage
    {
 private readonly AppDbContext _dbContext;

        public ExportStage(AppDbContext dbContext)
 {
     _dbContext = dbContext;
        }

        public async Task ExecuteAsync()
        {
     Console.WriteLine("=== EXPORT NO-EMAIL STAGE ===");
     Console.WriteLine();

     // Get all posts that are relevant but have no email
     var postsWithoutEmail = await _dbContext.Posts
  .Where(p => p.Status == PostStatus.RelevantNoEmail)
  .ToListAsync();

     if (postsWithoutEmail.Count == 0)
     {
  Console.WriteLine("No posts without email found.");
  Console.WriteLine();
         return;
     }

     var jobsWithoutEmail = postsWithoutEmail.Select(p => new JobWithoutEmail
     {
         PostId = p.PostId,
  LinkedinUrl = p.LinkedinUrl,
         AuthorName = p.AuthorName,
         Reason = p.Reason
     }).ToList();

     var outputPath = Path.Combine("Output", "jobs_without_email.json");
     var outputDirectory = Path.GetDirectoryName(outputPath);
     if (!string.IsNullOrEmpty(outputDirectory))
  Directory.CreateDirectory(outputDirectory);

     var json = JsonSerializer.Serialize(
  jobsWithoutEmail,
  new JsonSerializerOptions { WriteIndented = true });

     await File.WriteAllTextAsync(outputPath, json);

     Console.WriteLine($"{jobsWithoutEmail.Count} jobs without email exported.");
     Console.WriteLine($"Output written to {outputPath}");
     Console.WriteLine();
        }
    }
}
