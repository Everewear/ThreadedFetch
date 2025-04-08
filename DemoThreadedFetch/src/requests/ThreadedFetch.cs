using Microsoft.AspNetCore.Mvc;

public class ThreadedFetch : ControllerBase
{
    private static readonly HttpClient client = new HttpClient();
    private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(3);
    public async Task<IActionResult> FetchData() {
        var counter = 1;
        var urls = new List<string>
        {
            "https://en.wikipedia.org/w/api.php?action=query&format=json&prop=revisions&titles=frank_zappa&rvprop=timestamp|user&rvlimit=27&redirects",
            "https://en.wikipedia.org/w/api.php?action=query&format=json&prop=revisions&titles=frank&rvprop=timestamp|user&rvlimit=27&redirects",
            "https://en.wikipedia.org/w/api.php?action=query&format=json&prop=revisions&titles=hotdog&rvprop=timestamp|user&rvlimit=27&redirects",
            "https://en.wikipedia.org/w/api.php?action=query&format=json&prop=revisions&titles=hotdog&rvprop=timestamp|user&rvlimit=27&redirects",
            "https://en.wikipedia.org/w/api.php?action=query&format=json&prop=revisions&titles=hotdog&rvprop=timestamp|user&rvlimit=27&redirects",
        };
        
        var tasks = new List<Task<IActionResult>>();

        foreach (var url in urls) {
            tasks.Add(limitRequest(url, counter));
            counter += 1;
        }
        IActionResult[] responses = await Task.WhenAll(tasks);

        var jsonStrings = responses.OfType<OkObjectResult>().ToList();
        return Ok(jsonStrings);
    }

    private async Task<IActionResult> limitRequest(string url, int counter){
        await semaphore.WaitAsync();
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
        }
        finally {
            semaphore.Release();
        }
    }
}