using LinkedInJobMatcher.Data;
using LinkedInJobMatcher.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace LinkedInJobMatcher.Services
{
    public class AnalyzeStage
    {
 private readonly OllamaService _ollamaService;
 private readonly AppDbContext _dbContext;
        private readonly string _resumeText;

        private static readonly Regex EmailRegex =
     new(
         @"[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)+",
         RegexOptions.Compiled);

        public AnalyzeStage(OllamaService ollamaService, AppDbContext dbContext, string resumeText)
        {
     _ollamaService = ollamaService;
     _dbContext = dbContext;
     _resumeText = resumeText;
        }

        public async Task ExecuteAsync()
        {
     Console.WriteLine("=== ANALYZE STAGE ===");
     Console.WriteLine();

     // Query posts that are either Unprocessed or Failed with retries < 3
     var postsToAnalyze = await _dbContext.Posts
         .Where(p => p.Status == PostStatus.Unprocessed || 
       (p.Status == PostStatus.Failed && p.RetryCount < 3))
  .ToListAsync();

     if (postsToAnalyze.Count == 0)
     {
         Console.WriteLine("No posts to analyze.");
         Console.WriteLine();
  return;
     }

     Console.WriteLine($"Analyzing {postsToAnalyze.Count} posts.");
     Console.WriteLine();

     int successCount = 0;
     int failedCount = 0;
     int relevantWithEmailCount = 0;
     int relevantNoEmailCount = 0;
     int notRelevantCount = 0;

     foreach (var post in postsToAnalyze)
     {
         if (string.IsNullOrWhiteSpace(post.Content))
         {
      post.Status = PostStatus.Failed;
      post.Reason = "Post content is empty";
      post.AnalyzedAt = DateTime.UtcNow;
      await _dbContext.SaveChangesAsync();
      failedCount++;
      continue;
         }

         Console.WriteLine("--------------------------------");
         Console.WriteLine($"Post: {post.PostId}");
  Console.WriteLine(post.Content[..Math.Min(150, post.Content.Length)]);

  try
  {
      var analysis = await _ollamaService.AnalyzeJobAsync(
          _resumeText,
   post.Content);

      if (analysis == null)
      {
   Console.WriteLine("Ollama returned no result.");
   post.Status = PostStatus.Failed;
          post.Reason = "Ollama returned null";
          post.RetryCount++;
   post.AnalyzedAt = DateTime.UtcNow;
          await _dbContext.SaveChangesAsync();
          failedCount++;
   continue;
      }

      Console.WriteLine(
          $"Relevant: {analysis.IsRelevant}, " +
   $"Reason: {analysis.Reason}");

      if (!analysis.IsRelevant)
      {
          post.Status = PostStatus.NotRelevant;
          post.Reason = analysis.Reason;
          post.AnalyzedAt = DateTime.UtcNow;
   await _dbContext.SaveChangesAsync();
   notRelevantCount++;
   continue;
      }

      // Extract and normalize emails
      var aiEmails = analysis.Emails?
   .Where(e => !string.IsNullOrWhiteSpace(e))
          .Select(e => e.Trim().ToLowerInvariant())
   .ToList()
   ?? new List<string>();

      var regexEmails = EmailRegex
          .Matches(post.Content)
          .Select(x => x.Value.Trim().ToLowerInvariant())
   .ToList();

      var emails = aiEmails
          .Concat(regexEmails)
   .Where(IsValidEmail)
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .ToList();

      Console.WriteLine(
          $"AI emails: {aiEmails.Count}, " +
          $"Regex emails: {regexEmails.Count}, " +
   $"Final emails: {emails.Count}");

      if (emails.Count == 0)
      {
          post.Status = PostStatus.RelevantNoEmail;
   post.Reason = analysis.Reason;
   post.AnalyzedAt = DateTime.UtcNow;
   await _dbContext.SaveChangesAsync();
          relevantNoEmailCount++;
          continue;
      }

      // Relevant with emails - upsert recruiters and PostEmail links
      post.Status = PostStatus.RelevantWithEmail;
      post.Reason = analysis.Reason;
      post.AnalyzedAt = DateTime.UtcNow;

      foreach (var email in emails)
      {
   // Upsert recruiter
          var existingRecruiter = await _dbContext.Recruiters
       .FirstOrDefaultAsync(r => r.Email == email);

   if (existingRecruiter == null)
   {
       existingRecruiter = new Recruiter
       {
           Email = email,
           Status = RecruiterStatus.Pending
       };
       _dbContext.Recruiters.Add(existingRecruiter);
       await _dbContext.SaveChangesAsync(); // Save to get the ID
          }

   // Add PostEmail link if not already present
   var existingLink = await _dbContext.PostEmails
       .FirstOrDefaultAsync(pe => pe.PostId == post.PostId && 
      pe.RecruiterId == existingRecruiter.Id);

          if (existingLink == null)
          {
       _dbContext.PostEmails.Add(new PostEmail
       {
    PostId = post.PostId,
    RecruiterId = existingRecruiter.Id
       });
   }
      }

      await _dbContext.SaveChangesAsync();
      relevantWithEmailCount++;
      successCount++;
         }
         catch (Exception ex)
  {
      Console.WriteLine($"Error analyzing post: {ex.Message}");
      post.Status = PostStatus.Failed;
      post.Reason = ex.Message;
      post.RetryCount++;
      post.AnalyzedAt = DateTime.UtcNow;
      await _dbContext.SaveChangesAsync();
      failedCount++;
         }
     }

     Console.WriteLine();
     Console.WriteLine($"Analysis complete: {successCount} successful, {failedCount} failed.");
     Console.WriteLine($"  - Relevant with email: {relevantWithEmailCount}");
     Console.WriteLine($"  - Relevant without email: {relevantNoEmailCount}");
     Console.WriteLine($"  - Not relevant: {notRelevantCount}");
     Console.WriteLine();
        }

        private static bool IsValidEmail(string email)
        {
     return EmailRegex.IsMatch(email);
        }
    }
}
