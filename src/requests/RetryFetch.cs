namespace DemoThreadedFetch.src.requests
{
    public class RetryFetch
    {
        public async Task<HttpResponseMessage> Retry(string url, MultipartFormDataContent formData, HttpClient client, TimeSpan timeSpan, int tryCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tryCount);
            while (true)
            {
                try
                {
                    return await client.PostAsync(url, formData);
                }
                catch
                {
                    if (--tryCount == 0)
                        throw;
                    await Task.Delay(timeSpan);
                }
            }
        }

        // overload method for retrying for blob images
        public async Task<byte[]> Retry(string imgURL, HttpClient client, TimeSpan timeSpan, int tryCount)
        {

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tryCount);
            while (true)
            {
                try
                {
                    byte[] blobData = await client.GetByteArrayAsync(imgURL);
                    return blobData;
                }
                catch
                {
                    if (--tryCount == 0)
                        throw;
                    await Task.Delay(timeSpan);
                }
            }
        }
    }
}