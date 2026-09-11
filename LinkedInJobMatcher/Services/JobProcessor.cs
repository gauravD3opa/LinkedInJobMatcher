using LinkedInJobMatcher.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LinkedInJobMatcher.Services
{

    public class JobProcessor
    {
        private readonly OllamaService _ollamaService;
        private readonly GmailDraftService _gmailDraftService;

        private static readonly Regex EmailRegex =
            new(
                @"[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)+",
                RegexOptions.Compiled);

        public JobProcessor(OllamaService ollamaService, GmailDraftService gmailDraftService)
        {
            _ollamaService = ollamaService;
            _gmailDraftService = gmailDraftService;
        }

        /// <summary>
        /// Processes LinkedIn posts, extracts recruiter emails from relevant posts,
        /// and creates ONE common draft (with resume attached) per unique recruiter
        /// email — skipping anyone already contacted per sentStore.
        /// </summary>
        public async Task ProcessAsync(
            List<LinkedInPost> posts,
            string resumePath,
            string accessToken,
            string fromAddress,
            string draftLogPath,
            string resumePdfPath,
            string commonSubject,
            string commonBody,
            SentRecruitersStore sentStore)
        {
            if (posts == null || posts.Count == 0)
            {
                Console.WriteLine("No posts to process.");
                return;
            }

            var resume = await File.ReadAllTextAsync(resumePath);

            Console.WriteLine($"Processing {posts.Count} LinkedIn posts.");

            int matchCount = 0;
            int sentCount = 0;
            int skippedDuplicateCount = 0;
            int failedCount = 0;

            var draftLog = new List<DraftLogEntry>();
            var seenThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var post in posts)
            {
                if (string.IsNullOrWhiteSpace(post.Content))
                    continue;

                Console.WriteLine();
                Console.WriteLine("--------------------------------");
                Console.WriteLine($"Post: {post.Id}");
                Console.WriteLine(
                    post.Content[..Math.Min(150, post.Content.Length)]);

                try
                {
                    var analysis = await _ollamaService.AnalyzeJobAsync(
                        resume,
                        post.Content);

                    if (analysis == null)
                    {
                        Console.WriteLine("Ollama returned no result.");
                        continue;
                    }

                    Console.WriteLine(
                        $"Relevant: {analysis.IsRelevant}, " +
                        $"Reason: {analysis.Reason}");

                    if (!analysis.IsRelevant)
                        continue;

                    var emails = EmailRegex
                        .Matches(post.Content)
                        .Select(x => x.Value)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (emails.Count == 0)
                    {
                        Console.WriteLine("Relevant job, but no email found.");
                        continue;
                    }

                    matchCount++;

                    foreach (var email in emails)
                    {
                        var logEntry = new DraftLogEntry
                        {
                            PostId = post.Id,
                            LinkedinUrl = post.LinkedinUrl,
                            AuthorName = post.Author?.Name,
                            Reason = analysis.Reason,
                            Email = email,
                            Subject = commonSubject
                        };

                        // Skip if already emailed in a previous run, or already
                        // handled earlier in this same run (dedupe across posts too)
                        if (sentStore.HasSent(email) || !seenThisRun.Add(email))
                        {
                            Console.WriteLine($"Skipping {email} — already contacted.");
                            logEntry.Status = "skipped-duplicate";
                            draftLog.Add(logEntry);
                            skippedDuplicateCount++;
                            continue;
                        }

                        try
                        {
                            var result = await _gmailDraftService.CreateDraftWithAttachmentAsync(
                                accessToken: accessToken,
                                toAddress: email,
                                fromAddress: fromAddress,
                                subject: commonSubject,
                                bodyText: commonBody,
                                attachmentFilePath: resumePdfPath);

                            sentCount++;
                            Console.WriteLine($"Draft created for {email}.");

                            logEntry.Status = "sent";
                            try
                            {
                                using var doc = JsonDocument.Parse(result);
                                if (doc.RootElement.TryGetProperty("id", out var idProp))
                                    logEntry.DraftId = idProp.GetString();
                            }
                            catch
                            {
                                logEntry.DraftId = "created-id-unparsed";
                            }

                            // Mark + persist immediately so a crash mid-run
                            // doesn't cause re-sends on the next execution.
                            sentStore.MarkSent(email);
                            await sentStore.SaveAsync();
                        }
                        catch (Exception draftEx)
                        {
                            Console.WriteLine(
                                $"Failed to create draft for {email}: {draftEx.Message}");
                            logEntry.Status = "failed";
                            logEntry.ErrorMessage = draftEx.Message;
                            failedCount++;
                        }

                        draftLog.Add(logEntry);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {post.Id}: {ex.Message}");
                }
            }

            var logDirectory = Path.GetDirectoryName(draftLogPath);
            if (!string.IsNullOrEmpty(logDirectory))
                Directory.CreateDirectory(logDirectory);

            var logJson = JsonSerializer.Serialize(
                draftLog,
                new JsonSerializerOptions { WriteIndented = true });

            await File.WriteAllTextAsync(draftLogPath, logJson);

            Console.WriteLine();
            Console.WriteLine(
                $"Finished. {matchCount} matching posts, {sentCount} drafts created, " +
                $"{skippedDuplicateCount} skipped (duplicates), {failedCount} failed.");
            Console.WriteLine($"Draft log written to {draftLogPath}");
            Console.WriteLine($"Total unique recruiters contacted (all-time): {sentStore.Count}");
        }
    }
}