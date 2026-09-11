using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinkedInJobMatcher.Models
{
    public class LinkedInPost
    {
        public string? Content { get; set; }
        public string? Id { get; set; }
        public string? LinkedinUrl { get; set; }
        public Author? Author { get; set; }
    }
}
