using LinkedInJobMatcher.Data;
using LinkedInJobMatcher.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LinkedInJobMatcher.Services
{
    public class ImportStage
    {
 private readonly AppDbContext _dbContext;

        public ImportStage(AppDbContext dbContext)
 {
     _dbContext = dbContext;
        }

        public async Task ExecuteAsync()
        {
            Console.WriteLine("=== IMPORT STAGE ===");
     Console.WriteLine();

     // Import posts from Data/apify-posts*.json
     await ImportPostsAsync();

     // Import sent recruiters from Data/sent-recruiters.json
     await ImportSentRecruitersAsync();

     Console.WriteLine();
        }

        private async Task ImportPostsAsync()
        {
     var dataPath = "Data";
            var files = Directory.GetFiles(dataPath, "apify-posts*.json");

     Console.WriteLine($"Found {files.Length} Apify post files to import.");

     int newCount = 0;
     int skippedCount = 0;

     foreach (var file in files)
     {
  Console.WriteLine($"Importing from {file}...");

  var json = await File.ReadAllTextAsync(file);
  var posts = JsonSerializer.Deserialize<List<LinkedInPost>>(
      json,
      new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
      ?? new List<LinkedInPost>();

         foreach (var post in posts)
  {
      if (string.IsNullOrWhiteSpace(post.Id))
          continue;

      var existing = await _dbContext.Posts.FindAsync(post.Id);
      if (existing != null)
      {
          skippedCount++;
          continue;
      }

      var dbPost = new Post
      {
          PostId = post.Id,
          LinkedinUrl = post.LinkedinUrl,
   AuthorName = post.Author?.Name,
   Content = post.Content,
   FetchedAt = DateTime.UtcNow,
          Status = PostStatus.Unprocessed,
   RetryCount = 0
      };

      _dbContext.Posts.Add(dbPost);
      newCount++;
         }
     }

     await _dbContext.SaveChangesAsync();
     Console.WriteLine($"{newCount} new posts imported.");
     Console.WriteLine($"{skippedCount} posts skipped (already exist).");
     Console.WriteLine();
        }

 private async Task ImportSentRecruitersAsync()
 {
     var sentPath = Path.Combine("Data", "sent-recruiters.json");
     if (!File.Exists(sentPath))
     {
  Console.WriteLine($"No sent-recruiters file found at {sentPath}.");
         return;
     }

            Console.WriteLine($"Importing from {sentPath}...");

     var json = await File.ReadAllTextAsync(sentPath);
     var sentDict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
  ?? new Dictionary<string, string>();

     int newCount = 0;
            int skippedCount = 0;

     foreach (var entry in sentDict)
     {
  var email = entry.Key.Trim().ToLowerInvariant();

  var existing = await _dbContext.Recruiters
      .FirstOrDefaultAsync(r => r.Email == email);

  if (existing != null)
         {
      skippedCount++;
      continue;
  }

  var recruiter = new Recruiter
  {
      Email = email,
      Status = RecruiterStatus.Drafted,
      ContactedAt = DateTime.UtcNow
  };

  _dbContext.Recruiters.Add(recruiter);
  newCount++;
     }

     await _dbContext.SaveChangesAsync();
     Console.WriteLine($"{newCount} new sent recruiters imported.");
     Console.WriteLine($"{skippedCount} recruiters skipped (already exist).");
     Console.WriteLine();
        }
    }
}
