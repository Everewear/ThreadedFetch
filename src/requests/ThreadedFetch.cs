using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using dotenv.net;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

public class ThreadedFetch : ControllerBase
{
    private static string? limitThreads;
    private static int maxCount;
    private static string? recievedBody;
    private static readonly HttpClient client = new HttpClient();
    private static readonly Stopwatch sw = new();
    private static readonly SemaphoreSlim semaphore = new(3);
    public ThreadedFetch(int maxTaskCount, string limit, string body) {
        maxCount = maxTaskCount;
        limitThreads = limit;
        recievedBody = body;
    }
    // 
    
    public async Task<IActionResult> FetchData() {
        /*
        This is how you connect to any database
        DatabaseConnector.Database();
        */
        var envVars = DotEnv.Read();
        sw.Reset();
        sw.Start();

        // TODO Modify String body and then send to AI
        
        var tasks = new List<Task<IActionResult>>();
        
        if(maxCount != 0) {
            // meant for static url calls
            for (int i = 0; i < maxCount; i++) {
                tasks.Add(GetRequest(envVars["EVEREWEAR_AI_URL"], i + 1)); // this will use AI instead.
            }
        }

        IActionResult[] responses = await Task.WhenAll(tasks);

        var jsonStrings = responses.OfType<OkObjectResult>().ToList();
        TimeSpan timeTaken = sw.Elapsed;
        Console.WriteLine($"Time Elapsed: {timeTaken:m\\:ss\\.fff}");
        using var formData = new MultipartFormDataContent();
        var formContent = new StringContent(JsonConvert.SerializeObject(jsonStrings), Encoding.UTF8, "application/json");
        formData.Add(formContent, "response");
        var bodyData = await client.PutAsync("http://localhost:3000/retrieveInfo", formContent);
        return Ok(jsonStrings);
    }

    private async Task<IActionResult> PostRequest(string url, HttpContent content) {
        var response = await client.PostAsync(url, content);
            if(response.IsSuccessStatusCode) {
                return Ok(content);
            } 
            else {
                return Ok(content);
            }
    }
    
    private async Task<IActionResult> GetRequest(string url, int counter) {
        var envVars = DotEnv.Read();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        ParsedData? parsedData = System.Text.Json.JsonSerializer.Deserialize<ParsedData>(recievedBody, options);
        var infoToSend = new {
            prompts = new Dictionary<string, string>(),
            files = new List<Blob>(),
        };

        if (parsedData?.Prompts != null)
        {
            foreach (var prompt in parsedData.Prompts)
            {
                infoToSend.prompts[prompt.attribute] = prompt.prompt;
            }
        }

        if (parsedData?.ImageData != null) {
            foreach (var img in parsedData.ImageData) {
                if (parsedData.VendorData != null) {
                    var newUrl = parsedData.VendorData.imagesURL + img.url;
                    var response = await fetchImageBlob(newUrl);
                    if (response != null)
                    {
                        infoToSend?.files.Add(new Blob { FileName = img.url, Data = response }); // call to a function with a return value that is a blob.
                    }
                }
            }
        }
        if (limitThreads == "yes"){
            await semaphore.WaitAsync();
        }
        try{
            using var formData = new MultipartFormDataContent();
            if (infoToSend?.files != null && infoToSend.files.Count != 0)
            {
                foreach (var file in infoToSend.files)
                {
                    var fileContent = new ByteArrayContent(file.Data);
                    fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg"); // Adjust the content type as needed
                    formData.Add(fileContent, "images", file.FileName); // "images" is the field name the AI will likely expect
                }
            }

            var formContent = new StringContent(JsonConvert.SerializeObject(infoToSend?.prompts), Encoding.UTF8, "application/json");
            formData.Add(formContent, "prompts");
            Console.WriteLine($"Task{counter}");
            client.DefaultRequestHeaders.Add("ApiKey", envVars["EVEREWEAR_API_KEY"]);
            var response = await client.PostAsync(url, formData);
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