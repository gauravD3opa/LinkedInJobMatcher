using LinkedInJobMatcher.Data;
using LinkedInJobMatcher.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LinkedInJobMatcher.Services
{
    public class FetchStage
    {
        private readonly ApifyService _apifyService;
        private readonly AppDbContext _dbContext;

 public FetchStage(ApifyService apifyService, AppDbContext dbContext)
        {
     _apifyService = apifyService;
     _dbContext = dbContext;
        }

 public async Task ExecuteAsync(
     List<string> searchQueries,
     int maxPosts,
     string? postedLimit)
        {
     Console.WriteLine("=== FETCH STAGE ===");
            Console.WriteLine();

     var posts = await _apifyService.FetchPostsAsync(
         searchQueries: searchQueries,
  maxPosts: maxPosts,
         postedLimit: postedLimit);

     Console.WriteLine($"Fetched {posts.Count} posts from Apify.");

            // Save raw fetch results to archive
     var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
     var apifyLogPath = Path.Combine("Output", $"apify-posts_{timestamp}.json");
     var apifyLogDirectory = Path.GetDirectoryName(apifyLogPath);
     if (!string.IsNullOrEmpty(apifyLogDirectory))
  Directory.CreateDirectory(apifyLogDirectory);

     await File.WriteAllTextAsync(
         apifyLogPath,
  JsonSerializer.Serialize(posts, new JsonSerializerOptions { WriteIndented = true }));

     Console.WriteLine($"Apify fetch results saved to {apifyLogPath}");
     Console.WriteLine();

     // Upsert posts into database
     int newCount = 0;
     int existingCount = 0;

     foreach (var post in posts)
     {
  if (string.IsNullOrWhiteSpace(post.Id))
      continue;

         var existing = await _dbContext.Posts.FindAsync(post.Id);
         if (existing != null)
  {
      // Post already exists - do NOT overwrite or reset status
      existingCount++;
      continue;
  }

  // New post
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

     await _dbContext.SaveChangesAsync();

            Console.WriteLine($"{newCount} new posts added to database.");
     Console.WriteLine($"{existingCount} posts already in database.");
     Console.WriteLine();
        }
    }
}
