namespace DemoThreadedFetch
{
    public class Startup
    {
        public IConfiguration Configuration {get;}
        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
        }

        public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Configure the HTTP request pipeline.
            if (env.IsDevelopment())
            {
                
            }

            app.UseRouting();
            app.UseHttpsRedirection();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapPost("/wikiApiFetch", async (HttpRequest req) =>
                    {
                        string? input = req.Query["maxTaskCount"];
                        string? limit = req.Query["limit"];
                        string? body = await new StreamReader(req.Body).ReadToEndAsync();
                        int maxTaskCount;
                        
                        // defaults to limiting threads if not defined.
                        limit = limit switch
                        {
                            "no" => "no",
                            "yes" => "yes",
                            _ => "yes"
                        };

                        if (!int.TryParse(input, out maxTaskCount))
                        {
                            maxTaskCount = 0;
                        }

                        ThreadedFetch data = new(maxTaskCount, limit, body);
                        var apiInfo = await data.FetchData();
                        return apiInfo;
                    });
            });
        }
    }
}