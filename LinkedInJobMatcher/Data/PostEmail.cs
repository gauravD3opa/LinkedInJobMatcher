namespace LinkedInJobMatcher.Data
{
    public class PostEmail
    {
        public string PostId { get; set; } = null!;
 public int RecruiterId { get; set; }

        // Navigation properties
        public Post? Post { get; set; }
        public Recruiter? Recruiter { get; set; }
    }
}
