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
                        string? limit = req.Query["limit"];
                        string? body = await new StreamReader(req.Body).ReadToEndAsync();
                        
                        // defaults to limiting threads if not defined.
                        limit = limit switch
                        {
                            "no" => "no",
                            "yes" => "yes",
                            _ => "yes"
                        };

                        ThreadedFetch data = new(limit, body);
                        var apiInfo = await data.FetchData();
                        return apiInfo;
                    });
            });
        }
    }
}