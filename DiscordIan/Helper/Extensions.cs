using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using DiscordIan.Key;
using DiscordIan.Model;
using Microsoft.Extensions.Caching.Distributed;

namespace DiscordIan.Helper
{
    public static class Extensions
    {
        public static string IsNullOrEmptyReplace(this string str, string replace)
        {
            return (string.IsNullOrEmpty(str)) ? replace : str;            
        }
        public static string ToTitleCase(this string str)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str);
        }

        public static string StripHTML(this string str)
        {
            return Regex.Replace(str, "<.*?>", string.Empty);
        }

        public static string ValidateUri(this Uri uri)
        {
            return (uri?.IsAbsoluteUri ?? false) ? uri.AbsoluteUri : string.Empty;
        }

        public static string BaseUrl(this Uri uri)
        {
            if (uri?.IsAbsoluteUri ?? false)
            {
                return string.Format("{0}://{1}",
                    uri.Scheme,
                    uri.Host);
            }

            return string.Empty;
        }

        public static string WordSwap(this string str, IDistributedCache cache)
        {
            if (str == null)
            {
                return null;
            }

            var cachedSwaps = cache.GetString(Cache.WordSwap);

            if (string.IsNullOrEmpty(cachedSwaps))
            {
                cachedSwaps = WordSwapHelper.GetListFromFile();

                if (!string.IsNullOrEmpty(cachedSwaps))
                {
                    cache.SetStringAsync(Cache.WordSwap,
                        cachedSwaps);
                }
            }

            if (string.IsNullOrEmpty(cachedSwaps))
            {
                return str;
            }

            var wordSwaps = JsonSerializer.Deserialize<WordSwaps>(cachedSwaps);

            if (wordSwaps != null)
            {
                 foreach (var swap in wordSwaps.SwapList)
                    {
                        str = str.Replace(swap.inbound, swap.outbound);
                    }
            }

            return str;
        }

        public static Bitmap CropImage(this Bitmap image, Rectangle cropArea)
        {
            var newImage = new Bitmap(image);
            return newImage.Clone(cropArea, newImage.PixelFormat);
        }

        public static Bitmap TrimWhiteSpace(this Bitmap image)
        {
            int w = image.Width;
            int h = image.Height;

            bool IsAllWhiteRow(int row)
            {
                for (int i = 0; i < w; i++)
                {
                    if (image.GetPixel(i, row).R < 250)
                    {
                        return false;
                    }
                }
                return true;
            }

            bool IsAllWhiteColumn(int col)
            {
                for (int i = 0; i < h; i++)
                {
                    if (image.GetPixel(col, i).R < 250)
                    {
                        return false;
                    }
                }
                return true;
            }

            int leftMost = 0;
            for (int col = 0; col < w; col++)
            {
                if (IsAllWhiteColumn(col)) leftMost = col + 1;
                else break;
            }

            int rightMost = w - 1;
            for (int col = rightMost; col > 0; col--)
            {
                if (IsAllWhiteColumn(col)) rightMost = col - 1;
                else break;
            }

            int topMost = 0;
            for (int row = 0; row < h; row++)
            {
                if (IsAllWhiteRow(row)) topMost = row + 1;
                else break;
            }

            int bottomMost = h - 1;
            for (int row = bottomMost; row > 0; row--)
            {
                if (IsAllWhiteRow(row)) bottomMost = row - 1;
                else break;
            }

            if (rightMost == 0 && bottomMost == 0 && leftMost == w && topMost == h)
            {
                return image;
            }

            int croppedWidth = rightMost - leftMost + 1;
            int croppedHeight = bottomMost - topMost + 1;

            var rect = new Rectangle(leftMost, topMost, croppedWidth, croppedHeight);
            return image.CropImage(rect);
        }
    }
}
