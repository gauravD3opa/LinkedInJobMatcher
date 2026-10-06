using LinkedInJobMatcher.Data;
using LinkedInJobMatcher.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LinkedInJobMatcher.Services
{
    public class SendStage
    {
        private readonly GmailDraftService _gmailDraftService;
 private readonly GmailAuthService _gmailAuthService;
        private readonly AppDbContext _dbContext;
 private readonly string _fromAddress;
        private readonly string _resumePdfPath;
        private readonly string _commonSubject;
        private readonly string _commonBody;

        public SendStage(
     GmailDraftService gmailDraftService,
     GmailAuthService gmailAuthService,
     AppDbContext dbContext,
     string fromAddress,
     string resumePdfPath,
     string commonSubject,
     string commonBody)
 {
     _gmailDraftService = gmailDraftService;
            _gmailAuthService = gmailAuthService;
     _dbContext = dbContext;
     _fromAddress = fromAddress;
     _resumePdfPath = resumePdfPath;
     _commonSubject = commonSubject;
     _commonBody = commonBody;
 }

 public async Task ExecuteAsync()
 {
     Console.WriteLine("=== SEND STAGE ===");
     Console.WriteLine();

     // Check if there are any pending recruiters linked to relevant posts
     var pendingRecruiters = await _dbContext.Recruiters
  .Where(r => r.Status == RecruiterStatus.Pending &&
      r.PostEmails.Any(pe => pe.Post!.Status == PostStatus.RelevantWithEmail))
  .ToListAsync();

     if (pendingRecruiters.Count == 0)
     {
  Console.WriteLine("No pending recruiters to contact.");
         Console.WriteLine();
  return;
     }

     // Check for recruiters left in Drafting state (crashed run)
     var draftingRecruiters = await _dbContext.Recruiters
  .Where(r => r.Status == RecruiterStatus.Drafting)
         .ToListAsync();

     if (draftingRecruiters.Count > 0)
            {
         Console.WriteLine($"??  WARNING: {draftingRecruiters.Count} recruiter(s) left in 'Drafting' state from a previous run:");
         foreach (var recruiter in draftingRecruiters)
         {
      Console.WriteLine($"   - {recruiter.Email} (DraftId: {recruiter.DraftId})");
         }
         Console.WriteLine("Please check Gmail manually before continuing.");
  Console.WriteLine();
            }

     // Now acquire OAuth token
     Console.WriteLine("Acquiring Google OAuth token...");
            var accessToken = await _gmailAuthService.GetAccessTokenAsync();
     Console.WriteLine("Google OAuth token acquired.");
     Console.WriteLine();

     int sentCount = 0;
     int failedCount = 0;

     var draftLog = new List<DraftLogEntry>();
     var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");

            foreach (var recruiter in pendingRecruiters)
     {
         // Re-check that status hasn't changed
         var current = await _dbContext.Recruiters.FindAsync(recruiter.Id);
         if (current == null || current.Status != RecruiterStatus.Pending)
         {
      Console.WriteLine($"Skipping {recruiter.Email} — status changed.");
      continue;
         }

         // Get linked posts for logging
         var linkedPosts = await _dbContext.PostEmails
      .Where(pe => pe.RecruiterId == recruiter.Id &&
          pe.Post!.Status == PostStatus.RelevantWithEmail)
      .Include(pe => pe.Post)
      .ToListAsync();

  try
  {
      // Claim the recruiter
      current.Status = RecruiterStatus.Drafting;
      await _dbContext.SaveChangesAsync();

      Console.WriteLine($"Creating draft for {recruiter.Email}...");

      var result = await _gmailDraftService.CreateDraftWithAttachmentAsync(
          accessToken: accessToken,
          toAddress: recruiter.Email,
   fromAddress: _fromAddress,
          subject: _commonSubject,
   bodyText: _commonBody,
          attachmentFilePath: _resumePdfPath);

      // On success
      current.Status = RecruiterStatus.Drafted;
      current.ContactedAt = DateTime.UtcNow;

      try
      {
          using var doc = JsonDocument.Parse(result);
          if (doc.RootElement.TryGetProperty("id", out var idProp))
       current.DraftId = idProp.GetString();
      }
      catch
      {
          current.DraftId = "created-id-unparsed";
      }

      await _dbContext.SaveChangesAsync();
      sentCount++;
      Console.WriteLine($"Draft created for {recruiter.Email}.");

      // Log entry
      var firstPost = linkedPosts.FirstOrDefault()?.Post;
      var logEntry = new DraftLogEntry
      {
          PostId = firstPost?.PostId,
   LinkedinUrl = firstPost?.LinkedinUrl,
   AuthorName = firstPost?.AuthorName,
          Reason = firstPost?.Reason,
   Email = recruiter.Email,
          Subject = _commonSubject,
          Status = "sent",
   DraftId = current.DraftId,
          ProcessedAtUtc = DateTime.UtcNow
      };
      draftLog.Add(logEntry);
         }
  catch (Exception ex)
         {
      Console.WriteLine($"Failed to create draft for {recruiter.Email}: {ex.Message}");
      current.Status = RecruiterStatus.Failed;
      current.Error = ex.Message;
      await _dbContext.SaveChangesAsync();
      failedCount++;

      var firstPost = linkedPosts.FirstOrDefault()?.Post;
      var logEntry = new DraftLogEntry
      {
          PostId = firstPost?.PostId,
   LinkedinUrl = firstPost?.LinkedinUrl,
   AuthorName = firstPost?.AuthorName,
          Reason = firstPost?.Reason,
   Email = recruiter.Email,
          Subject = _commonSubject,
          Status = "failed",
   ErrorMessage = ex.Message,
          ProcessedAtUtc = DateTime.UtcNow
      };
      draftLog.Add(logEntry);
         }
     }

     // Write draft log
     var logPath = Path.Combine("Output", $"drafts-log_{timestamp}.json");
     var logDirectory = Path.GetDirectoryName(logPath);
     if (!string.IsNullOrEmpty(logDirectory))
         Directory.CreateDirectory(logDirectory);

            await File.WriteAllTextAsync(
         logPath,
  JsonSerializer.Serialize(draftLog, new JsonSerializerOptions { WriteIndented = true }));

     Console.WriteLine();
     Console.WriteLine($"Finished send stage: {sentCount} drafts created, {failedCount} failed.");
     Console.WriteLine($"Draft log written to {logPath}");
     Console.WriteLine();
        }
    }
}
