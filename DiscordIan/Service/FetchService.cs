using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Serialization;
using DiscordIan.Helper;
using DiscordIan.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DiscordIan.Service
{
    public class FetchService
    {
        private const string json = "application/json";
        private const string xml = "application/xml";
        private const string text = "text/plain";
        private const string html = "text/html";

        private readonly ILogger<FetchService> _logger;
        private readonly HttpClient _httpClient;

        public FetchService(ILogger<FetchService> logger,
            HttpClient httpClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<Response<T>> GetAsync<T>(Uri requestUri,
            IDictionary<string, string> headers = null) where T : class
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new Response<T>();
            try
            {
                if (headers?.Count > 0)
                {
                    foreach (var header in headers)
                    {
                        _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }
                var httpResult = await _httpClient.GetAsync(requestUri);
                //var test = httpResult.Content.ReadAsStringAsync().Result;
                if (!httpResult.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Fetch failed with HTTP status {HttpStats} on request {RequestURI}",
                        httpResult.StatusCode,
                        requestUri);
                    response.IsSuccessful = false;
                    response.Elapsed = stopwatch.Elapsed;
                    response.Message = $"HTTP status code {httpResult.StatusCode}";

                    return response;
                }

                response.Data = await DeserializeObjectAsync<T>(
                    httpResult.Content);
                response.IsSuccessful = true;
                response.Elapsed = stopwatch.Elapsed;
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fetch errored trying to fetch {RequestURI}: {ErrorMessage}",
                    requestUri,
                    ex.Message);
                response.IsSuccessful = false;
                response.Message = ex.Message;
                response.Elapsed = stopwatch.Elapsed;
                return response;
            }
        }

        public async Task<Response<Bitmap>> GetImageAsync(Uri requestUri)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new Response<Bitmap>();
            try
            {
                var imageResult = await ImageHelper.GetImageFromURI(requestUri);
                if (imageResult == null)
                {
                    _logger.LogWarning("Image Fetch failed.");
                    response.IsSuccessful = false;
                    response.Elapsed = stopwatch.Elapsed;
                    response.Message = "Image Fetch failed.";
                }
                //var test = httpResult.Content.ReadAsStringAsync().Result;
                response.Data = imageResult;
                response.IsSuccessful = true;
                response.Elapsed = stopwatch.Elapsed;
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Image Fetch errored trying to fetch {RequestURI}: {ErrorMessage}",
                    requestUri,
                    ex.Message);
                response.IsSuccessful = false;
                response.Message = ex.Message;
                response.Elapsed = stopwatch.Elapsed;
                return response;
            }
        }

        public async Task<T> DeserializeObjectAsync<T>(HttpContent content) where T : class
        {
            if (content?.Headers?.ContentType?.MediaType == json)
            {
                var data = await content.ReadAsStringAsync();
                var obj = JsonConvert.DeserializeObject<T>(data);
                return obj;
            }

            if (content?.Headers?.ContentType?.MediaType == xml)
            {
                var contentString = content.ReadAsStringAsync().Result;
                using TextReader reader = new StringReader(contentString);
                var xmlSerializer = new XmlSerializer(typeof(T));
                return xmlSerializer.Deserialize(reader) as T;
            }

            if (content?.Headers?.ContentType?.MediaType == text
                || content?.Headers?.ContentType?.MediaType == html)
            {
                return content.ReadAsStringAsync().Result as T;
            }

            throw new Exception($"Unknown response type: {content?.Headers?.ContentType?.MediaType}");
        }
    }
}
