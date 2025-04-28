using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

public class ThreadedFetch : ControllerBase
{
    public ThreadedFetch(int maxTaskCount, string limit, string body) {
        maxCount = maxTaskCount;
        limitThreads = limit;
        recievedBody = body;
    }
    private static string? limitThreads;
    private static int maxCount;
    private static string? recievedBody;
    private static readonly HttpClient client = new HttpClient();
    private static readonly Stopwatch sw = new();
    private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(3);
    public async Task<IActionResult> FetchData() {
        /*
        This is how you connect to any database
        DatabaseConnector.Database();
        */
        
        sw.Reset();
        sw.Start();

        // TODO Modify String body and then send to AI
        
        var tasks = new List<Task<IActionResult>>();
        
        if(maxCount != 0) {
            // meant for static url calls
            for (int i = 0; i < maxCount; i++) {
                tasks.Add(GetRequest("http://localhost:5555/analyze?temperature=0.5&model=gpt-4o-2024-05-13&verbose=true&format=json", i + 1)); // this will use AI instead.
            }
        }

        IActionResult[] responses = await Task.WhenAll(tasks);

        var jsonStrings = responses.OfType<OkObjectResult>().ToList();
        TimeSpan timeTaken = sw.Elapsed;
        Console.WriteLine($"Time Elapsed: {timeTaken.ToString(@"m\:ss\.fff")}");
        
        return Ok(jsonStrings);
    }
    
    private async Task<IActionResult> GetRequest(string url, int counter){

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        ParsedData? parsedData = JsonSerializer.Deserialize<ParsedData>(recievedBody, options);
        var infoToSend = new {
            prompts = new Dictionary<string, string>(), // this just needs to be turned back into JSON.
            files = new List<Blob>(), // this needs to be blobs. might require some custom class, C# doesn't natively support blobs
        };
        //Console.WriteLine(JsonSerializer.Serialize(parsedData, new JsonSerializerOptions { WriteIndented = true }));

        if (parsedData?.Prompts != null)
        {
            foreach (var prompt in parsedData.Prompts)
            {
                infoToSend.prompts[prompt.attribute] = prompt.prompt;
                Console.WriteLine(infoToSend.prompts[prompt.attribute]);
            }
        }

        if (parsedData?.ImageData != null) {
            foreach (var img in parsedData.ImageData) {
                if (parsedData.VendorData != null) {
                    var newUrl = parsedData.VendorData.imagesURL + img.url;
                    var response = await fetchImageBlob(newUrl);
                    infoToSend?.files.Add(response); // call to a function with a return value that is a blob.
                }
            }
        }
        if (limitThreads == "yes"){
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
    /*fetch the blob of an image and write it to a byte array. 
      This should be passed to info to send per item. */
    private async Task<byte[]> fetchImageBlob(string imgURL) {
        try {
            byte[] blobData = await client.GetByteArrayAsync(imgURL);
            return blobData;
        }
        catch (Exception ex){
            Console.WriteLine($"Error fetching blob: {ex.Message}");
            return null;
        }
    }

    public class Blob {
        public string FileName { get; set; }
        public byte[] Data { get; set; }
    }
    public class ParsedData {
        public List<PromptInfo>? Prompts {get; set;}
        public VendorInformation? VendorData {get; set;}
        public List<ItemInfo>? ItemData {get; set;}
        public List<ImageInfo>? ImageData { get; set; }
    }
    public class ItemInfo {
        public int ItemID {get; set;}
    }
    public class ImageInfo {
        public string url {get; set;} 
    }
    public class VendorInformation {
        public string imagesURL {get; set;}
    }
    public class PromptInfo {
        public string prompt {get; set;}
        public string attribute {get; set;}
    }
}
