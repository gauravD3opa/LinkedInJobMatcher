using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinkedInJobMatcher.Models
{
    internal class JobWithoutEmail
    {
        public string? PostId { get; set; }

        public string? LinkedinUrl { get; set; }

        public string? AuthorName { get; set; }

        public string? Reason { get; set; }
    }
}
