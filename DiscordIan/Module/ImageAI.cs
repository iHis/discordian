using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordIan.Helper;
using DiscordIan.Model.ImageAI;
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

            var model = ParseCommandArgs(prompt);

            try
            {
                await CallImageService(model);
            }
            catch (Exception ex)
            {
                await ReplyAsync(ex.Message);
            }
        }

        [Command("imgnext", RunMode = RunMode.Async)]
        [Summary("Run again with new seed.")]
        public async Task GetAnotherImage()
        {
            if (Context.User.IsNaughty())
            {
                await ReplyAsync("Prick.");
                return;
            }

            var cachedString = await _cache.GetStringAsync(ImgKey);

            if (!string.IsNullOrEmpty(cachedString))
            {
                var userId = Context.User.Id;
                var channelId = Context.Channel.Id;
                var list = JsonConvert.DeserializeObject<List<ImgCacheModel>>(cachedString);

                if (list != null)
                {
                    var msg = list.OrderByDescending(l => l.Timestamp).FirstOrDefault(l => l.ChannelId == channelId);

                    if (msg != null)
                    {
                        try
                        {
                            msg.Request.Seed = new Random().Next(1, 99999).ToString();
                            await CallImageService(msg.Request);
                        }
                        catch (Exception ex)
                        {
                            await ReplyAsync(ex.Message);
                        }
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
            var messageRef = Context.Message.ReferencedMessage;
            var model = new ImgCacheModel();

            if (string.IsNullOrEmpty(cachedString) && messageRef == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(cachedString) && messageRef == null)
            {
                var list = JsonConvert.DeserializeObject<List<ImgCacheModel>>(cachedString);
                model = list.FirstOrDefault(l => l.UserId == Context.User.Id && l.ChannelId == Context.Channel.Id);
            }

            if (messageRef != null)
            {
                if (messageRef.Author.Id == _client.CurrentUser.Id 
                    && messageRef.Embeds.Any()
                    && (messageRef.Embeds.First().Title?.StartsWith("Prompt:") ?? false))
                {
                    model = new ImgCacheModel { ChannelId = messageRef.Channel.Id, MessageId = messageRef.Id, UserId = messageRef.Author.Id };
                }
            }

            if (model != null && model.MessageId != 0)
            {
                var channel = _client.GetChannel(model.ChannelId) as ITextChannel;
                var message = await channel.GetMessageAsync(model.MessageId);
                
                if (message != null)
                {
                    await channel.DeleteMessageAsync(model.MessageId);
                }
            }
        }

        [Command("imgsend", RunMode = RunMode.Async)]
        [Summary("Send image from image channel to primary channel.")]
        public async Task ImageSend()
        {
            if (Context.User.IsNaughty())
            {
                await ReplyAsync("Prick.");
                return;
            }

            if (Context.Channel.Id != ImgChannel)
            {
                return;
            }

            var messageRef = Context.Message.ReferencedMessage;

            if (messageRef != null)
            {
                if (messageRef.Author.Id == _client.CurrentUser.Id && messageRef.Content.StartsWith("Prompt:"))
                {
                    var prompt = $"{messageRef.Content}\nSender: {Context.User.Username}";
                    var embed = new EmbedBuilder
                    {
                        Title = prompt,
                        ImageUrl = messageRef.Attachments.First().Url
                    };

                    var channel = _client.GetChannel(QuakeChannel) as ISocketMessageChannel;
                    
                    await channel.SendMessageAsync("", false, embed.Build());

                    return;
                }
            }
            else
            {
                var cachedString = await _cache.GetStringAsync(ImgKey);

                if (!string.IsNullOrEmpty(cachedString))
                {
                    var list = JsonConvert.DeserializeObject<List<ImgCacheModel>>(cachedString);

                    var model = list.FirstOrDefault(l => l.UserId == Context.User.Id && l.ChannelId == Context.Channel.Id)?.Request;

                    if (model != null && !string.IsNullOrEmpty(model.Prompt))
                    {
                        await CallImageService(model, QuakeChannel);
                    }
                }
            }
        }

        private async Task CallImageService(ImgRequestModel request, ulong? channelId = null)
        {
            var url = string.Format(_options.PollinationsAIEndpoint,
                Uri.EscapeDataString(request.Prompt),
                request.Model,
                request.Seed);

            var response = await _fetchService.GetImageAsync(new Uri(url));
            apiTiming += response.Elapsed;

            if (response.IsSuccessful)
            {
                var channel = channelId != null 
                    ? _client.GetChannel((ulong)channelId) as ISocketMessageChannel 
                    : Context.Channel;

                using var stream = new MemoryStream();
                response.Data.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                stream.Seek(0, SeekOrigin.Begin);
                response.Data.Dispose();
                var message = await channel.SendFileAsync(
                    stream, 
                    "image.jpeg", 
                    $"Prompt: {request.Prompt}\nModel: {request.Model}{(channelId != null ? $"\nSender: {Context.User.Username}" : "")}");

                ImgCache(_cache, Context.User.Id, channel.Id, message.Id, request);
            }
            else
            {
                await ReplyAsync($"API call unsuccessful: {response.Message}");
            }

            HistoryAdd(_cache, GetType().Name, request.Prompt, apiTiming);
        }

        private ImgRequestModel ParseCommandArgs(string prompt)
        {
            var args = new Dictionary<string, string> {
                { "seed", "-seed [0-9]{1,10}" },
                { "model", "-model (flux|turbo|gptimage)" }
            };

            var model = new ImgRequestModel { Prompt = prompt };
            var seedMatch = new Regex(args["seed"]).Match(prompt);
            var modelMatch = new Regex(args["model"]).Match(prompt);

            if (seedMatch.Success)
            {
                model.Seed = seedMatch.Value.Split(' ')[1];
                model.Prompt = model.Prompt.Replace(seedMatch.Value, string.Empty).Trim();
            }

            if (modelMatch.Success)
            {
                model.Model = modelMatch.Value.Split(' ')[1];
                model.Prompt = model.Prompt.Replace(modelMatch.Value, string.Empty).Trim();
            }

            if (model.Prompt.Contains("-model") || model.Prompt.Contains("-seed"))
            {
                throw new ArgumentException("Invalid prompt format. Please use -model and -seed flags correctly.");
            }

            return model;
        }
    }
}
