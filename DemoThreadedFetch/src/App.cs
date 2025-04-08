using System.ComponentModel;
using Microsoft.AspNetCore.Components.Forms;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

ThreadedFetch data = new();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/wikiApiFetch", (HttpRequest req) => 
{    
    var apiInfo = data.FetchData();
    return apiInfo;
})
.WithName("GetWikiApiInfo");

app.Run();
