﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using DiscordIan.Model;
using Microsoft.Extensions.Caching.Distributed;

namespace DiscordIan.Module
{
    public abstract class BaseModule : ModuleBase<SocketCommandContext>
    {
        private const int MaxReplyLength = 2000;
        private const string ForgetIt = "\u2026 never mind, I'm tired of typing";
        private const string HistoryKey = "History";

        protected override async Task<IUserMessage> ReplyAsync(string message = null,
            bool isTTS = false,
            Embed embed = null,
            RequestOptions options = null)
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

        public async void HistoryAdd(IDistributedCache cache, string service, string input, TimeSpan time)
        {
            var user = Context.User.Username;

            var historyItem = new HistoryItem
            {
                UserName = user,
                Service = service,
                Input = input,
                Timing = string.Format("{0}.{1}s", time.Seconds, time.Milliseconds),
                AddDate = DateTime.Now
            };

            var cachedString = await cache.GetStringAsync(HistoryKey);

            if (string.IsNullOrEmpty(cachedString))
            {
                var history = new HistoryModel
                {
                    HistoryList = new List<HistoryItem> { historyItem }
                };

                await cache.SetStringAsync(HistoryKey,
                    JsonSerializer.Serialize(history));
            }
            else
            {
                var history = JsonSerializer.Deserialize<HistoryModel>(cachedString);
                history.HistoryList.Add(historyItem);

                var pastUserHist = history.HistoryList.Where(h => h.UserName == user);

                if (pastUserHist.Count() > 10)
                {
                    history.HistoryList.RemoveAt(0);
                }

                await cache.SetStringAsync(HistoryKey,
                    JsonSerializer.Serialize(history));
            }
        }
    }
}
