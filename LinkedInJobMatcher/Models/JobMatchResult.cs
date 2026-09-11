using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinkedInJobMatcher.Models
{
    public class JobMatchResult
    {
        public string? PostId { get; set; }
        public string? LinkedinUrl { get; set; }

        public string? AuthorName { get; set; }

        public bool IsRelevant { get; set; }


        public string? Reason { get; set; }

        public List<string> Emails { get; set; } = [];

        public string? Content { get; set; }
    }
}
