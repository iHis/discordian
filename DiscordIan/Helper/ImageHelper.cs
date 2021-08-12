using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscordIan.Helper
{
    public static class ImageHelper
    {
        public async static Task<Bitmap> GetImageFromURI(Uri uri)
        {
            Bitmap response;

            var data = await GetImageData(uri);

            var streamBitmap = new MemoryStream(data);
            response = new Bitmap(Image.FromStream(streamBitmap));

            return response;
        }

        public static Bitmap ClipComicSection(Bitmap image, Tuple<int, int> layout, int selection)
        {
            if (selection > (layout.Item1 * layout.Item2))
            {
                throw new Exception("Selection out of bounds, idiot.");
            }

            var cellSize = new Size()
            {
                Width = image.Width / layout.Item1,
                Height = image.Height / layout.Item2
            };

            var startPos = DetermineStartPoint(layout.Item1, selection, cellSize);

            var rect = new Rectangle(startPos, cellSize);

            return image.CropImage(rect).TrimWhiteSpace();
        }

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
