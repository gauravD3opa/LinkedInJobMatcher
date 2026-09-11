using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinkedInJobMatcher.Services
{
    public class LoggingHandler : DelegatingHandler
    {
        public LoggingHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Console.WriteLine();
            Console.WriteLine("========== HTTP REQUEST ==========");
            Console.WriteLine($"{request.Method} {request.RequestUri}");

            foreach (var header in request.Headers)
            {
                Console.WriteLine(
                    $"{header.Key}: {string.Join(",", header.Value)}");
            }

            var response = await base.SendAsync(
                request,
                cancellationToken);

            Console.WriteLine();
            Console.WriteLine("========== HTTP RESPONSE ==========");
            Console.WriteLine(
                $"{(int)response.StatusCode} {response.StatusCode}");

            foreach (var header in response.Headers)
            {
                Console.WriteLine(
                    $"{header.Key}: {string.Join(",", header.Value)}");
            }


            // ADD THIS
            var body = await response.Content.ReadAsStringAsync(
                cancellationToken);

            Console.WriteLine();
            Console.WriteLine("========== RESPONSE BODY ==========");
            Console.WriteLine(body);

            return response;
        }
    }
}
