namespace WebApi429
{
    public class Program
    {
        private const int MAX_ENDPOINTS = 6;
        private const int MAX_REQUESTS = 5;
        private const int RETRY_AFTER_SECONDS = 3;
        private const int RESET_COUNTER_AFTER_SECONDS = 60;

        public class Api429
        {
            public int Count { get; set; } = 0;
            public DateTime Reset429 { get; set; } = DateTime.UtcNow;
            public DateTime LastRequest { get; set; } = DateTime.UtcNow;
        }

        /// <summary>
        /// Generates API endpoints that return 429 status codes when the maximum number of requests is exceeded. The 429 logic is simplified through a counter and irrespective of requests over time.
        /// If the counter is at the maximum, the endpoint will return a 429 status code and reset the counter after a specified time.
        /// </summary>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();

            var Tracker = new List<Api429>(MAX_ENDPOINTS);

            for (int i = 0; i < MAX_ENDPOINTS; i++)
            {
                Tracker.Add(new Api429());
            }

            app.MapGet("/api/{index}", (HttpContext context, string index) =>
            {
                if (!Int32.TryParse(index, out int i))
                {
                    return Results.NotFound();
                }

                // Ensure that the requested endpoint exists.
                if (i < 0 || i >= MAX_ENDPOINTS)
                {
                    return Results.NotFound();
                }
                else
                {   // Ensure that the existing endpoint can accept requests.
                    if (Tracker[i].Reset429 > DateTime.UtcNow)
                    {
                        context.Response.Headers.Append("Retry-After", Math.Ceiling((Tracker[i].Reset429 - DateTime.UtcNow).TotalSeconds).ToString());
                        return Results.StatusCode(429);
                    }
                    else
                    {
                        // If the last request was more than a minute ago, reset the counter. This ensures a fresh start for the counter after a period of inactivity.
                        if (Tracker[i].LastRequest.AddSeconds(RESET_COUNTER_AFTER_SECONDS) < DateTime.UtcNow)
                        {
                            Tracker[i].Count = 0;
                        }

                        // Increment the counter for the successful request.
                        if (Tracker[i].Count < MAX_REQUESTS)
                        {
                            Tracker[i].Count++;
                            Tracker[i].LastRequest = DateTime.UtcNow;
                            return Results.Ok(i.ToString());
                        }
                        else // If the counter is at the maximum, return a 429.
                        {
                            Tracker[i].Count = 0;
                            Tracker[i].Reset429 = DateTime.UtcNow.AddSeconds(RETRY_AFTER_SECONDS);

                            context.Response.Headers.Append("Retry-After", RETRY_AFTER_SECONDS.ToString());
                            return Results.StatusCode(429);
                        }
                    }
                }
            });

            app.MapFallback(async (context) =>
            {
                context.Response.Redirect("/api/0");
                await Task.CompletedTask;
            });

            app.Run();
        }
    }
}
