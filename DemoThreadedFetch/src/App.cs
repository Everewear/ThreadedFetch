using System.ComponentModel;
using Microsoft.AspNetCore.Components.Forms;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/wikiApiFetch", (HttpRequest req) => 
{
    string? input = req.Query["maxTaskCount"];
    string? limit = req.Query["limit"];
    int maxTaskCount;
    // defaults to limiting threads if not defined.
    if(limit != "yes" || limit != "no"){
        limit = "yes";
    }
    if (!int.TryParse(input, out maxTaskCount)) {
        maxTaskCount = 0;
    }
    
    ThreadedFetch data = new(maxTaskCount, limit);
    var apiInfo = data.FetchData();
    return apiInfo;
})
.WithName("GetWikiApiInfo");

app.Run();
