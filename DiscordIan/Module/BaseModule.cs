using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using DiscordIan.Helper;
using DiscordIan.Key;
using DiscordIan.Model;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

namespace DiscordIan.Module
{
    public abstract class BaseModule : ModuleBase<SocketCommandContext>
    {
        private const int MaxReplyLength = 2000;
        private const string ForgetIt = "\u2026 never mind, I'm tired of typing";
        public const ulong ImgChannel = 1052342379643940864;
        public const ulong QuakeChannel = 633650348195577857;

        protected override async Task<IUserMessage> ReplyAsync(string message = null,
            bool isTTS = false,
            Embed embed = null,
            RequestOptions options = null,
            AllowedMentions allowedMentions = null,
            MessageReference messageReference = null)
        {
            string response = message;

            if (!string.IsNullOrWhiteSpace(response) && response.Length > 2000)
            {
                await base.ReplyAsync(message.Substring(0, MaxReplyLength - 1) + "\u2026",
                    isTTS,
                    embed,
                    options);

                if (message.Length <= MaxReplyLength * 2)
                {
                    response = message.Substring(MaxReplyLength - 1);
                }
                else
                {
                    response = message.Substring(MaxReplyLength - 1,
                        MaxReplyLength - 1 - ForgetIt.Length)
                        + ForgetIt;
                }
            }

            return await base.ReplyAsync(response, isTTS, embed, options);
        }

        public async void HistoryAdd(IDistributedCache _cache, string service, string input, TimeSpan time)
        {
            var user = await Context.Channel.GetUserByID(Context.User.Id);

            var historyItem = new HistoryItem
            {
                ChannelName = Context.Channel.Name,
                UserName = user.Nickname ?? user.Username,
                Service = service,
                Input = input,
                Timing = string.Format("{0}.{1}s", time.Seconds, time.Milliseconds),
                AddDate = DateTime.Now
            };

            var cache = await _cache.Deserialize<HistoryModel>(Cache.History);

            if (cache == default)
            {
                var history = new HistoryModel
                {
                    HistoryList = new List<HistoryItem> { historyItem }
                };

                await _cache.SetStringAsync(Cache.History, JsonConvert.SerializeObject(history));
            }
            else
            {
                cache.HistoryList.Add(historyItem);

                var pastUserHist = cache.HistoryList.Where(h => h.UserName == user.Nickname || h.UserName == user.Nickname);

                if (pastUserHist.Count() > 10)
                {
                    cache.HistoryList.RemoveAt(0);
                }

                await _cache.SetStringAsync(Cache.History, JsonConvert.SerializeObject(cache));
            }
        }
    }
}
