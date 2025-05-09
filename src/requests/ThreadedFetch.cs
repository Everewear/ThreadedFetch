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
    private static string? recievedBody;
    private static readonly HttpClient client = new HttpClient();
    private static readonly Stopwatch sw = new();
    private static readonly SemaphoreSlim semaphore = new(1);
    private InfoToSend infoToSend = new InfoToSend();
    public ThreadedFetch(string limit, string body) {
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
        
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        ParsedData? parsedData = System.Text.Json.JsonSerializer.Deserialize<ParsedData>(recievedBody, options);
        var tasks = new List<Task<IActionResult>>();

        if (parsedData?.Prompts != null)
        {
            foreach (var prompt in parsedData.Prompts)
            {
                infoToSend.Prompts[prompt.attribute] = prompt.prompt;
            }
        }
        
        if(parsedData?.Items != null) {
            var i = 0;
            foreach (var item in parsedData.Items) {
                var itemID = item.Value.ItemID;
                    foreach (var image in item.Value.Images.Values)
                    {
                        string newUrl = item.Value.VendorInformation.imagesURL != null
                        ? item.Value.VendorInformation.imagesURL + image.url
                        : image.url;
                        var response = await fetchImageBlob(newUrl);
                        if (response != null)
                        {
                            infoToSend?.Files.Add(new Blob { FileName = image.url, Data = response });
                        }
                    }
                tasks.Add(PostRequest(envVars["EVEREWEAR_AI_URL"], i + 1, itemID));
                i++;
                }
            }
        
        IActionResult[] responses = await Task.WhenAll(tasks);

        var jsonStrings = responses.OfType<OkObjectResult>().ToList();
        TimeSpan timeTaken = sw.Elapsed;
        Console.WriteLine($"Time Elapsed: {timeTaken:m\\:ss\\.fff}");
        using var returnFormData = new MultipartFormDataContent();
        var returnFormContent = new StringContent(JsonConvert.SerializeObject(jsonStrings), Encoding.UTF8, "application/json");
        returnFormData.Add(returnFormContent, "response");
        var bodyData = await client.PutAsync($"{envVars["PRODUCTION_URL"]}/retrieveInfo", returnFormContent);
        return Ok(jsonStrings);
    }

    private async Task<IActionResult> PostRequest(string url, int counter, int? itemID) {
        var envVars = DotEnv.Read();
        var formData = new MultipartFormDataContent();
        
        if (infoToSend?.Files != null && infoToSend.Files.Count != 0) {
                    foreach (var file in infoToSend.Files) {
                            var fileContent = new ByteArrayContent(file.Data);
                            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg"); // Adjust the content type as needed
                            formData.Add(fileContent, "images", file.FileName); // "images" is the field name the AI will likely expect
                        }
                    }
        var formContent = new StringContent(JsonConvert.SerializeObject(infoToSend?.Prompts), Encoding.UTF8, "application/json");
        formData.Add(formContent, "prompts");

        if (limitThreads == "yes"){
            await semaphore.WaitAsync();
        }
        try{
            Console.WriteLine($"Task{counter}");
            client.DefaultRequestHeaders.Add("ApiKey", envVars["EVEREWEAR_API_KEY"]);
            var response = await Retry(url, formData, timeSpan: TimeSpan.FromSeconds(1000), tryCount: 3);
            if(response.IsSuccessStatusCode) {
                var content = new {
                    data = new{
                        rewrites = await response.Content.ReadAsStringAsync(),
                        task = counter,
                        item_id = itemID,
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
                        item_id = itemID,
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

    private async Task<HttpResponseMessage> Retry(string url, MultipartFormDataContent formData, TimeSpan timeSpan, int tryCount)
    {
        if (tryCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(tryCount));
        while (true) {
            try {
                return await client.PostAsync(url, formData);
            } catch {
                if (--tryCount ==0)
                    throw;
                await Task.Delay(timeSpan);
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

    public class InfoToSend {
        public Dictionary<string, string> Prompts { get; set; } = new();
        public List<Blob> Files { get; set; } = new();
    }
    public class Blob {
        public string FileName { get; set; }
        public byte[] Data { get; set; }
    }
    public class ParsedData {
        public List<PromptInfo>? Prompts {get; set;}
        public Dictionary<string, ItemInfo>? Items { get; set; }
        // public int ItemID {get; set;}
    }

    public class ItemInfo {
        public Dictionary<string, Image> Images {get; set;}
        public int ItemID {get; set;}
        public VendorInformation VendorInformation {get; set;}
    }
    
    public class PromptInfo {
        public string prompt {get; set;}
        public string attribute {get; set;}
    }

    public class Image {
        public string url {get; set;}
    }

    public class VendorInformation {
        public string? imagesURL { get; set; }

    }
}