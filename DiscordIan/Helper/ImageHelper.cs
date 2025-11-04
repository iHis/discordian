using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;

namespace DiscordIan.Helper
{
    public static class ImageHelper
    {
        public async static Task<byte[]> GetImageFromURI(Uri uri)
        {
            return await GetImageData(uri);
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

        private async static Task<byte[]> GetImageData(Uri uri)
        {
            byte[] imageBytes;

            try
            {
                using (var webClient = new WebClient())
                {
                    imageBytes = await webClient.DownloadDataTaskAsync(uri);
                }

                return imageBytes;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
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
