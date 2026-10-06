using LinkedInJobMatcher.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LinkedInJobMatcher.Services
{
    public class OllamaService
    {
        private readonly HttpClient _httpClient;

        private const string Model = "qwen3.5:4b" ;
        //private const string Model = "deepseek-r1:1.5b";

        public OllamaService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<JobAnalysis?> AnalyzeJobAsync(
            string resume,
            string jobContent,
            CancellationToken cancellationToken = default)
        {

            var prompt = """
You are a strict job matching and email extraction system.

Compare the candidate's resume against the LinkedIn job post.

CANDIDATE RESUME:
""" + resume + """

JOB POST:
""" + jobContent + """

Evaluate the job using ALL of the following criteria.

========================================
1. LOCATION
========================================

The job MUST be located in India.

- Jobs in India are acceptable.
- A remote job is acceptable ONLY when the job can reasonably
  be performed from India.
- Jobs located in USA, Canada, UK, Europe, Middle East,
  or any country outside India are NOT relevant.
- If the location is missing or unclear, return false.

========================================
2. TECHNICAL SKILLS
========================================

Compare the technologies required by the job with the candidate's
actual resume.

Pay particular attention to:

- C#
- .NET
- .NET Core
- ASP.NET Core
- Web API
- REST APIs
- SQL Server
- Entity Framework
- Azure
- Microservices
- AI / LLM
- Authentication / Authorization
- Backend development

The candidate does NOT need to have every single technology,
but the core technical requirements should match the resume.

If the job requires a completely different technology stack,
return false.

========================================
3. EXPERIENCE
========================================

Compare the experience required by the job with the candidate's
experience in the resume.

Do NOT assume experience that is not present in the resume.

If the job clearly requires substantially more experience,
return false.

========================================
4. EMAIL EXTRACTION
========================================

Extract ALL email addresses that appear in the LinkedIn job post.

Look specifically for:

- Recruiter emails
- HR emails
- Hiring emails
- Recruitment team emails
- Application emails
- Company career emails

Examples:

hr@company.com
recruiter@company.com
jobs@company.com
careers@company.com

IMPORTANT:

- Only return emails that actually appear in the job post.
- Do NOT invent or guess an email address.
- Do NOT generate an email based on the company domain.
- Do NOT extract an email from the candidate resume.
- If there are no emails in the job post, return an empty array.
- Remove duplicate emails.
- Return the exact email address found in the post.

========================================
5. OVERALL DECISION
========================================

Return true ONLY when:

- Location is acceptable
- Technical skills are reasonably aligned
- Experience/seniority is reasonably aligned

If any major mandatory requirement is clearly not satisfied,
return false.

If you are not sure, return false.

DO NOT GUESS.

========================================
OUTPUT
========================================

Return ONLY valid JSON.

Required format:

{
    "isRelevant": false,
    "reason": "Short explanation",
    "emails": []
}

Example:

{
    "isRelevant": true,
    "reason": "India-based .NET backend role requiring 4+ years of experience.",
    "emails": [
        "recruiter@company.com",
        "jobs@company.com"
    ]
}
""";

            var request = new
            {
                model = Model,
                prompt,
                stream = false,
                format = "json",
                think = false
            };

            using var response = await _httpClient.PostAsJsonAsync(
                "/api/generate",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var rawResponse = await response.Content.ReadAsStringAsync(
    cancellationToken);

            Console.WriteLine("OLLAMA RAW RESPONSE:");
            Console.WriteLine(rawResponse);
            Console.WriteLine("--------------------------------");

            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                Console.WriteLine("Ollama returned an empty response.");
                return null;
            }

            var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(
                rawResponse,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (string.IsNullOrWhiteSpace(ollamaResponse?.Response))
            {
                Console.WriteLine("Ollama response field is empty.");
                return null;
            }

            Console.WriteLine("OLLAMA MODEL RESPONSE:");
            Console.WriteLine(ollamaResponse.Response);

            try
            {
                return JsonSerializer.Deserialize<JobAnalysis>(
                    ollamaResponse.Response,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }
            catch (JsonException ex)
            {
                Console.WriteLine("Invalid JSON returned by Ollama.");
                Console.WriteLine($"Response: {ollamaResponse.Response}");
                Console.WriteLine($"Error: {ex.Message}");

                return null;
            }
        }
    }
}
