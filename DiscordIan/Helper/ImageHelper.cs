using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Newtonsoft.Json.Linq;

namespace DiscordIan.Helper
{
    public static class ImageHelper
    {
        public async static Task<byte[]> GetImageFromURI(Uri uri,
            IDictionary<string, string> headers = null)
        {
            return await GetImageData(uri, headers);
        }

        //public static Bitmap ClipComicSection(Bitmap image, Tuple<int, int> layout, int selection)
        //{
        //    if (selection > (layout.Item1 * layout.Item2))
        //    {
        //        throw new Exception("Selection out of bounds, idiot.");
        //    }

        //    var cellSize = new Size()
        //    {
        //        Width = image.Width / layout.Item1,
        //        Height = image.Height / layout.Item2
        //    };

        //    var startPos = DetermineStartPoint(layout.Item1, selection, cellSize);

        //    var rect = new Rectangle(startPos, cellSize);

        //    return image.CropImage(rect).TrimWhiteSpace();
        //}

        private async static Task<byte[]> GetImageData(Uri uri,
            IDictionary<string, string> headers = null)
        {
            HttpResponseMessage response;

            using (var client = new HttpClient())
            {
                if (headers?.Count > 0)
                {
                    foreach (var header in headers)
                    {
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }

                response = await client.GetAsync(uri);
            }

            if (!response.IsSuccessStatusCode)
            {
                var responseStr = await response.Content.ReadAsStringAsync();

                var jobj = JObject.Parse(responseStr);

                if (jobj != null && jobj.Count > 0)
                {
                    var message = jobj?["error"]?["message"]?.ToString(); 
                    if (!string.IsNullOrEmpty(message) 
                        && JObject.Parse(message) is JObject innerMessage)
                    {
                        throw new Exception(innerMessage?["message"]?.ToString() ?? message);
                    }
                }

                throw new Exception(responseStr);
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        private static Point DetermineStartPoint(int rows, int selection, Size cellSize)
        {
            return new Point
            {
                X = ((selection - 1) % rows) * cellSize.Width,
                Y = ((selection - 1) / rows) * cellSize.Height
            };
        }
    }
}
