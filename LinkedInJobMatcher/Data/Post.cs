using LinkedInJobMatcher.Data;

namespace LinkedInJobMatcher.Data
{
    public class Post
    {
        public string PostId { get; set; } = null!;
 public string? LinkedinUrl { get; set; }
        public string? AuthorName { get; set; }
        public string? Content { get; set; }
        public DateTime FetchedAt { get; set; }
        public PostStatus Status { get; set; }
        public string? Reason { get; set; }
 public int RetryCount { get; set; }
        public DateTime? AnalyzedAt { get; set; }

 // Navigation property
        public ICollection<PostEmail> PostEmails { get; set; } = new List<PostEmail>();
    }
}
