using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public class ThreadedFetch : ControllerBase
{
    public ThreadedFetch(int maxTaskCount, string limit) {
        maxCount = maxTaskCount;
        limitThreads = limit;
    }
    private static string? limitThreads;
    private static int maxCount;
    private static readonly HttpClient client = new HttpClient();
    private static readonly Stopwatch sw = new();
    private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(3);
    public async Task<IActionResult> FetchData() {
        var counter = 1;
        /*
        This is how you connect to any database
        DatabaseConnector.Database();
        */
        
        sw.Reset();
        sw.Start();

        // TODO allow for custom links
        var urls = new List<string>
        {
            "https://en.wikipedia.org/w/api.php?action=query&format=json&prop=revisions&titles=frank_zappa&rvprop=timestamp|user&rvlimit=27&redirects",
            "https://en.wikipedia.org/w/api.php?action=query&format=json&prop=revisions&titles=frank&rvprop=timestamp|user&rvlimit=27&redirects",
            "https://en.wikipedia.org/w/api.php?action=query&format=json&prop=revisions&titles=hotdog&rvprop=timestamp|user&rvlimit=27&redirects",
            "https://en.wikipedia.org/w/api.php?action=query&format=json&prop=revisions&titles=hotdog&rvprop=timestamp|user&rvlimit=27&redirects",
            "https://en.wikipedia.org/w/api.php?action=query&format=json&prop=revisions&titles=hotdog&rvprop=timestamp|user&rvlimit=27&redirects",
        };
        
        var tasks = new List<Task<IActionResult>>();
        
        if(maxCount != 0) {
            // meant for static url calls
            for (int i = 0; i < maxCount; i++) {
                tasks.Add(GetRequest("https://en.wikipedia.org/w/api.php?action=query&format=json&prop=revisions&titles=hotdog&rvprop=timestamp|user&rvlimit=27&redirects", i + 1));
            }
        } else {
            // this is for a set of urls that need to be processed. Demo uses WIKIAPI.
            foreach (var url in urls) {
                tasks.Add(GetRequest(url, counter));
                counter += 1;
            }
        }
        IActionResult[] responses = await Task.WhenAll(tasks);

        var jsonStrings = responses.OfType<OkObjectResult>().ToList();
        TimeSpan timeTaken = sw.Elapsed;
        Console.WriteLine($"Time Elapsed: {timeTaken.ToString(@"m\:ss\.fff")}");
        
        return Ok(jsonStrings);
    }
    
    private async Task<IActionResult> GetRequest(string url, int counter){
        if(limitThreads == "yes"){
            await semaphore.WaitAsync();
        }
        try{
            Console.WriteLine($"Task{counter}");
            var response = await client.GetAsync(url);
            if(response.IsSuccessStatusCode) {
                var content = new {
                    data = new{
                        rewrites = await response.Content.ReadAsStringAsync(),
                        task = counter,
                        startTime = DateTime.UtcNow,
                        finishTime = DateTime.UtcNow
                        }
                };
                Console.WriteLine($"Task{counter} finsihed");
                return Ok(content);
            } else {
                var content = new {
                    data = new{
                        failure = "Failure",
                        task = counter,
                        startTime = DateTime.UtcNow,
                        finishTime = DateTime.UtcNow
                        }
                };
                Console.WriteLine($"Task{counter} finished");
                return Ok(content);
            }
        } catch (Exception HttpRequest){
            return BadRequest($"Bad HTTP Link: {HttpRequest}");
        }
        finally {
            if(limitThreads == "yes"){
            semaphore.Release();
            }
        }
    }
}