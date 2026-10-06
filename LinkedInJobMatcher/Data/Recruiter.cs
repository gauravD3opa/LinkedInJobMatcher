using LinkedInJobMatcher.Data;

namespace LinkedInJobMatcher.Data
{
    public class Recruiter
    {
        public int Id { get; set; }
        public string Email { get; set; } = null!;
 public RecruiterStatus Status { get; set; }
        public string? DraftId { get; set; }
 public DateTime? ContactedAt { get; set; }
        public string? Error { get; set; }

 // Navigation property
        public ICollection<PostEmail> PostEmails { get; set; } = new List<PostEmail>();
    }
}
