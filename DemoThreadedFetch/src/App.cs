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
    
    int maxTaskCount;
    if (!int.TryParse(input, out maxTaskCount)) {
        maxTaskCount = 0;
    } else if (maxTaskCount > 5){
        maxTaskCount = 5;
    }
    
    ThreadedFetch data = new(maxTaskCount);
    var apiInfo = data.FetchData();
    return apiInfo;
})
.WithName("GetWikiApiInfo");

app.Run();
