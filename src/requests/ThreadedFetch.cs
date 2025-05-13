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
    private static readonly Stopwatch sw = new();
    private static readonly SemaphoreSlim semaphore = new(3);
    private InfoToSend SentInfo = new();
    private static HttpClient? client;
    private readonly RetryFetch retry = new RetryFetch();
    public ThreadedFetch(string limit, string body, HttpClient sentClient)
    {
        limitThreads = limit;
        recievedBody = body;
        client = sentClient;
    }

    public async Task<IActionResult> FetchData()
    {
        /*
        This is how you connect to any mysql database
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
                SentInfo.Prompts[prompt.Attribute] = prompt.Prompt;
            }
        }

        if (parsedData?.Items != null)
        {
            var i = 0;
            foreach (var item in parsedData.Items)
            {
                var itemID = item.Value.ItemID;
                foreach (var image in item.Value.Images.Values)
                {
                    string newUrl = item.Value.VendorInformation.ImagesURL != null
                    ? item.Value.VendorInformation.ImagesURL + "/" + image.Url
                    : image.Url;
                    var response = await FetchImageBlob(newUrl);
                    if (response != null)
                    {
                        SentInfo?.Files.Add(new Blob { FileName = image.Url, Data = response });
                    }
                }
                tasks.Add(PostRequest(envVars["EVEREWEAR_AI_URL"], i + 1, itemID));
                i++;
                SentInfo?.Files.Clear();
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

    private async Task<IActionResult> PostRequest(string url, int counter, int? itemID)
    {
        var formData = new MultipartFormDataContent();

        if (SentInfo?.Files != null && SentInfo.Files.Count != 0)
        {
            foreach (var file in SentInfo.Files)
            {
                var fileContent = new ByteArrayContent(file.Data);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg"); // Adjust the content type as needed
                formData.Add(fileContent, "images", file.FileName); // "images" is the field name the AI will likely expect
            }
        }
        var formContent = new StringContent(JsonConvert.SerializeObject(SentInfo?.Prompts), Encoding.UTF8, "application/json");
        formData.Add(formContent, "prompts");

        if (limitThreads == "yes")
        {
            await semaphore.WaitAsync();
        }
        try
        {
            Console.WriteLine($"Task{counter}");

            var response = await retry.Retry(url, formData, client, timeSpan: TimeSpan.FromSeconds(1000), tryCount: 3);

            if (response.IsSuccessStatusCode)
            {
                var content = new
                {
                    data = await response.Content.ReadAsStringAsync(),
                    task = counter,
                    item_id = itemID,
                    startTime = DateTime.UtcNow,
                    finishTime = DateTime.UtcNow
                };
                Console.WriteLine($"Task{counter} finsihed");
                return Ok(content);
            }
            else
            {
                var content = new
                {
                    failure = response,
                    task = counter,
                    item_id = itemID,
                    startTime = DateTime.UtcNow,
                    finishTime = DateTime.UtcNow
                };
                Console.WriteLine($"Task{counter} finished");
                return Ok(content);
            }
        }
        catch (Exception HttpRequest)
        {
            return BadRequest($"Bad HTTP Link: {HttpRequest}");
        }
        finally
        {
            if (limitThreads == "yes")
            {
                semaphore.Release();
                formContent.Dispose();
            }
        }
    }

    /*fetch the blob of an image and write it to a byte array. 
        This should be passed to info to send per item. */
    private async Task<byte[]?> FetchImageBlob(string imgURL)
    {
        try
        {
            return await retry.Retry(imgURL, client, timeSpan: TimeSpan.FromSeconds(1000), tryCount: 3);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching blob: {ex.Message}");
            return null;
        }
    }
    private class InfoToSend
    {
        public Dictionary<string, string> Prompts { get; set; } = new();
        public List<Blob> Files { get; set; } = new();
    }
    private class Blob
    {
        public required string FileName { get; set; }
        public required byte[] Data { get; set; }
    }
    private class ParsedData
    {
        public List<PromptInfo>? Prompts { get; set; }
        public Dictionary<string, ItemInfo>? Items { get; set; }

    }

    private class ItemInfo
    {
        public required Dictionary<string, Image> Images { get; set; }
        public int ItemID { get; set; }
        public required VendorInformation VendorInformation { get; set; }
    }

    private class PromptInfo
    {
        public required string Prompt { get; set; }
        public required string Attribute { get; set; }
    }

    private class Image
    {
        public required string Url { get; set; }
    }

    private class VendorInformation
    {
        public string? ImagesURL { get; set; }

    }
}