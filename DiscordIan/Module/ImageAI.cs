using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordIan.Helper;
using DiscordIan.Key;
using DiscordIan.Model;
using DiscordIan.Service;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace DiscordIan.Module
{
    public class ImageAI : BaseModule
    {
        private readonly IDistributedCache _cache;
        private readonly FetchService _fetchService;
        private readonly Model.Options _options;
        private readonly DiscordSocketClient _client;
        private TimeSpan apiTiming = new TimeSpan();

        public ImageAI(IDistributedCache cache,
            FetchService fetchService,
            IOptionsMonitor<Model.Options> optionsAccessor,
            DiscordSocketClient client)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _fetchService = fetchService
                ?? throw new ArgumentNullException(nameof(fetchService));
            _options = optionsAccessor.CurrentValue
                ?? throw new ArgumentNullException(nameof(optionsAccessor));
            _client = client
                ?? throw new ArgumentNullException(nameof(client));
        }

        [Command("imgai", RunMode = RunMode.Async)]
        [Summary("Generate AI image.")]
        [Alias("img")]
        public async Task AIGeneratedImage([Remainder]
            [Summary("Prompt")] string prompt)
        {
            if (Context.User.IsNaughty())
            {
                await ReplyAsync("Prick.");
                return;
            }
            
            var url = string.Format(_options.PollinationsAIEndpoint, 
                Uri.EscapeDataString(prompt), 
                new Random().Next(1, 9999));

            var response = await _fetchService.GetImageAsync(new Uri(url));
            apiTiming += response.Elapsed;

            if (response.IsSuccessful)
            {
                using var stream = new MemoryStream();
                response.Data.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                stream.Seek(0, SeekOrigin.Begin);
                response.Data.Dispose();
                var message = await Context.Channel.SendFileAsync(stream, "image.jpeg", $"Prompt: {prompt}");

                ImgCache(_cache, Context.User.Id, Context.Channel.Id, message.Id, prompt);
            }
            else
            {
                await ReplyAsync($"API call unsuccessful: {response.Message}");
            }

            HistoryAdd(_cache, GetType().Name, prompt, apiTiming);
        }

        [Command("imgnext", RunMode = RunMode.Async)]
        [Summary("Run again with new seed.")]
        public async Task GetAnotherImage()
        {
            var cachedString = await _cache.GetStringAsync(ImgKey);

            if (!string.IsNullOrEmpty(cachedString))
            {
                var userId = Context.User.Id;
                var channelId = Context.Channel.Id;
                var list = JsonConvert.DeserializeObject<List<ImgCacheModel>>(cachedString);

                if (list != null)
                {
                    var msg = list.FirstOrDefault(l => l.ChannelId == channelId);

                    if (msg != null)
                    {
                        await AIGeneratedImage(msg.Prompt);
                    }
                }
            }
        }

        [Command("imgdel", RunMode = RunMode.Async)]
        [Summary("Delete last AI image.")]
        [Alias("nope")]
        public async Task DeleteLastImage()
        {
            var cachedString = await _cache.GetStringAsync(ImgKey);

            if (!string.IsNullOrEmpty(cachedString))
            {
                var messageRef = Context.Message.ReferencedMessage;
                var list = JsonConvert.DeserializeObject<List<ImgCacheModel>>(cachedString);
                var msg = messageRef != null 
                    && messageRef.Author.Id == _client.CurrentUser.Id 
                    && messageRef.Content.StartsWith("Prompt:")
                    ? new ImgCacheModel { ChannelId = messageRef.Channel.Id, MessageId = messageRef.Id, UserId = messageRef.Author.Id }
                    : list.FirstOrDefault(l => l.UserId == Context.User.Id && l.ChannelId == Context.Channel.Id);

                if (msg != null && msg.MessageId != 0)
                {
                    var channel = _client.GetChannel(msg.ChannelId) as ITextChannel;
                    var message = await channel.GetMessageAsync(msg.MessageId);

                    if (message != null)
                    {
                        await channel.DeleteMessageAsync(msg.MessageId);
                    }

                    //list.Remove(msg);
                    //await _cache.SetStringAsync(ImgKey, JsonConvert.SerializeObject(list));
                }
            }
        }
    }
}
