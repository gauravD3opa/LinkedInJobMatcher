using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinkedInJobMatcher.Models
{
    public class DraftLogEntry
    {
        public string? PostId { get; set; }
        public string? LinkedinUrl { get; set; }
        public string? AuthorName { get; set; }
        public string? Reason { get; set; }
        public string? Email { get; set; }
        public string? Subject { get; set; }
        public string? Status { get; set; } // "sent", "skipped-duplicate", "failed"
        public string? DraftId { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
