namespace DemoThreadedFetch
{
    public class Startup(IConfiguration configuration)
    {
        public IConfiguration Configuration { get; } = configuration;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
        }

        public static void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime applicationLifetime)
        {

            app.UseRouting();
            app.UseHttpsRedirection();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapPost("/threadedCalls", async (HttpRequest req) =>
                    {
                        HttpClient client = new HttpClient();

                        string? limit = req.Query["limit"];
                        string? body = await new StreamReader(req.Body).ReadToEndAsync();
                        string? apiKey = Environment.GetEnvironmentVariable("EVEREWEAR_API_KEY");
                        if (!string.IsNullOrEmpty(apiKey))
                        {
                            client.DefaultRequestHeaders.Add("ApiKey", apiKey);
                        }
                        // defaults to limiting threads if not defined.
                        limit = limit switch
                        {
                            "no" => "no",
                            "yes" => "yes",
                            _ => "yes"
                        };

                        src.requests.ThreadedFetch data = new(limit, body, client);
                        //var apiInfo = await data.FetchData(data.GetOptions());
                        return 1;
                    });
            });
            applicationLifetime.ApplicationStopping.Register(() =>
            {
                Console.WriteLine("SIGTERM received - performing cleanup.");
                // Close DB connections, stop background tasks, etc.
            });
        }
    }
}