using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordIan.Helper;
using DiscordIan.Key;
using DiscordIan.Model;
using DiscordIan.Service;
using Microsoft.Extensions.Caching.Distributed;

namespace DiscordIan.Module
{
    public class History : BaseModule
    {
        private readonly IDistributedCache _cache;
        private readonly CommandHandlingService _cmdService;

        public History(IDistributedCache cache, CommandHandlingService cmdService)
        {
            _cache = cache
                ?? throw new ArgumentNullException(nameof(cache));
            _cmdService = cmdService;
        }

        [Command("history", RunMode = RunMode.Async)]
        [Summary("Returns previous 10 calls.")]
        public async Task HistoryAsync([Remainder]
        [Summary("User to filter by")] string user = null)
        {
            var cache = await _cache.Deserialize<HistoryModel>(Cache.History);

            if (cache == default)
            {
                await ReplyAsync("No API history records found.");
                return;
            }

            var response = string.Empty;

            var sb = new StringBuilder();
            var items = 0;

            if (!string.IsNullOrEmpty(user))
            {
                var guildUser = await Context.Channel.GetUser(user);
                user = guildUser?.Nickname ?? guildUser?.Username ?? user;
            }

            foreach (var item in cache.HistoryList.OrderByDescending(h => h.AddDate))
            {
                if (items == 10)
                {
                    break;
                }

                if (string.IsNullOrEmpty(user) || item.UserName == user)
                {
                    sb.AppendLine($"**User:** {item.UserName} **Channel:** {item.ChannelName} **Date:** {DateHelper.UTCtoEST(item.AddDate, "MM/dd hh:mm tt")} **Service:** {item.Service} **Input:** {item.Input} **Timing:** {item.Timing}");
                    items++;
                }
            }

            var reversed = string.Join("\r\n", sb.ToString().Trim().Split('\r', '\n').Reverse());

            if (!string.IsNullOrEmpty(reversed))
            {
                await ReplyAsync(">>> " + reversed);
            }
            else
            {
                await ReplyAsync("No API history records found.");
            }
        }

        [Command("again", RunMode = RunMode.Async)]
        [Summary("Repeat last command")]
        public async Task RunAgain()
        {
            var msgBytes = await _cache.GetAsync(string.Format(Cache.PreviousCommand, Context.Channel.Id));

            if (msgBytes.Length != 0)
            {
                if (BitConverter.ToUInt64(msgBytes, 0) is ulong msgId && msgId != 0)
                {
                    var msg = Context.Channel.GetCachedMessage(msgId);

                    await _cmdService.MessageReceivedAsync(msg);
                }
            }            
        }
    }
}
