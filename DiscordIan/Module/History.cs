using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using DiscordIan.Helper;
using DiscordIan.Model;
using Microsoft.Extensions.Caching.Distributed;

namespace DiscordIan.Module
{
    public class History : BaseModule
    {
        private readonly IDistributedCache _cache;

        private const string CacheKey = "History";

        public History(IDistributedCache cache)
        {
            _cache = cache
                ?? throw new ArgumentNullException(nameof(cache));
        }

        [Command("history", RunMode = RunMode.Async)]
        [Summary("Returns previous 10 calls.")]
        public async Task HistoryAsync([Summary("User to filter by")]
            string user = null)
        {
            var cache = await _cache.Deserialize<HistoryModel>(CacheKey);

            if (cache == default)
            {
                await ReplyAsync("No API history records found.");
                return;
            }

            var response = string.Empty;

            var sb = new StringBuilder();
            int items = 0;

            foreach (var item in cache.HistoryList.OrderByDescending(h => h.AddDate))
            {
                if (items == 10)
                {
                    break;
                }

                if (string.IsNullOrEmpty(user) || item.UserName == user)
                {
                    sb.AppendLine($"**User:** {item.UserName} **Date:** {DateHelper.UTCtoEST(item.AddDate, "MM/dd hh:mm tt")} **Service:** {item.Service} **Input:** {item.Input} **Timing:** {item.Timing}");
                    items++;
                }
            }

            var reversed = string.Join("\r\n", sb.ToString().Trim().Split('\r', '\n').Reverse());

            await ReplyAsync(">>> " + reversed);
        }
    }
}
