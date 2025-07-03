using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordIan.Key;
using DiscordIan.Model;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

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

            var wordSwaps = JsonConvert.DeserializeObject<WordSwaps>(cachedSwaps);

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

        public static void Shuffle<T>(this IList<T> list)
        {
            var random = new Random();

            for (int i = list.Count - 1; i > 1; i--)
            {
                var rnd = random.Next(i + 1);

                (list[i], list[rnd]) = (list[rnd], list[i]);
            }
        }

        public static bool IsNaughty(this SocketUser user)
        {
            if (!(user is SocketGuildUser usr))
            {
                return false;
            }

            if (usr.Roles.Any(role => role.Name.Equals("Naughty", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        public static async Task<T> Deserialize<T>(this IDistributedCache cache, string key)
        {
            var cacheString = await cache.GetStringAsync(key);

            if (!string.IsNullOrEmpty(cacheString))
            {
                return JsonConvert.DeserializeObject<T>(cacheString);
            }

            return default;
        }

        public static async Task<IGuildUser> GetUserByName(this ISocketMessageChannel channel, string username)
        {
            var users = await channel.GetUsersAsync().ToListAsync();
            var user = users
                .SelectMany(x => x.Select(y => y as IGuildUser))
                .FirstOrDefault(u => u.Username == username || u.Nickname == username);

            return user;
        }

        public static async Task<IGuildUser> GetUserByID(this ISocketMessageChannel channel, ulong id)
        {
            var users = await channel.GetUsersAsync().ToListAsync();
            var user = users
                .SelectMany(x => x.Select(y => y as IGuildUser))
                .FirstOrDefault(u => u.Id == id);

            return user;
        }

        public static async Task<IGuildUser> GetUser(this ISocketMessageChannel channel, string input)
        {
            var mentionCheck = new Regex("^<@([0-9]+)>$").Match(input);
            IGuildUser guildUser;

            if (mentionCheck.Groups.Count > 1)
            {
                var id = ulong.Parse(mentionCheck.Groups[1]?.Value);
                guildUser = await channel.GetUserByID(id);
            }
            else
            {
                guildUser = await channel.GetUserByName(input);
            }

            if (!string.IsNullOrEmpty(guildUser?.Nickname))
            {
                return guildUser;
            }

            return null;
        }
    }
}
