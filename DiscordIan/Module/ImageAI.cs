using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using DiscordIan.Helper;
using DiscordIan.Key;
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
        public async Task GetAnotherImage([Summary("Model")] string model = null)
        {
            if (Context.User.IsNaughty())
            {
                await ReplyAsync("Prick.");
                return;
            }

            var cache = await _cache.Deserialize<List<ImgCacheModel>>(string.Format(Cache.ImgAi, Context.Channel.Id));

            if (cache != default)
            {
                var userId = Context.User.Id;
                var channelId = Context.Channel.Id;

                var msg = cache.OrderByDescending(l => l.Timestamp).FirstOrDefault(l => l.ChannelId == channelId);

                if (msg != null)
                {
                    try
                    {
                        msg.Request.Seed = new Random().Next(1, 99999).ToString();
                        msg.Request.Model = model == "flux" || model == "turbo"
                            ? model
                            : msg.Request.Model;

                        await CallImageService(msg.Request);
                    }
                    catch (Exception ex)
                    {
                        await ReplyAsync(ex.Message);
                    }
                }
            }
        }

        [Command("imgdel", RunMode = RunMode.Async)]
        [Summary("Delete last AI image.")]
        [Alias("nope")]
        public async Task DeleteLastImage()
        {
            var messageRef = Context.Message.ReferencedMessage;
            var model = new ImgCacheModel();

            if (messageRef != null)
            {
                if (messageRef.Author.Id == _client.CurrentUser.Id
                    && ((messageRef.Embeds.Any()
                        && (messageRef.Embeds.First().Title?.StartsWith("Prompt:") ?? false))
                    || messageRef.Content.StartsWith("Prompt:")))
                {
                    model = new ImgCacheModel { ChannelId = messageRef.Channel.Id, MessageId = messageRef.Id };
                }
            }
            else
            {
                var messages = await Context.Channel
                    .GetMessagesAsync()
                    .ToListAsync();
                var message = messages
                    .FirstOrDefault()
                    .FirstOrDefault(m =>
                    m.Author.Id == _client.CurrentUser.Id
                    && (m.Embeds.Any()
                        && (m.Embeds.First().Title?.StartsWith("Prompt:") ?? false))
                    || m.Content.StartsWith("Prompt:"));

                model.ChannelId = message.Channel.Id;
                model.MessageId = message.Id;
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
                    var user = await Context.Channel.GetUserByID(Context.User.Id);

                    var prompt = $"{messageRef.Content}\nSender: {user.Nickname ?? user.Username}";
                    var embed = new EmbedBuilder
                    {
                        Title = prompt,
                        ImageUrl = messageRef.Attachments.First().Url
                    };

                    var quake = _client.GetChannel(QuakeChannel) as ISocketMessageChannel;

                    await quake.SendMessageAsync("", false, embed.Build());

                    return;
                }
            }
            else
            {
                var cache = await _cache.Deserialize<List<ImgCacheModel>>(string.Format(Cache.ImgAi, Context.Channel.Id));

                if (cache != default)
                {
                    var model = cache.FirstOrDefault(l => l.UserId == Context.User.Id && l.ChannelId == Context.Channel.Id)?.Request;

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
                var user = await Context.Channel.GetUserByName(Context.User.Username);
                
                using var stream = new MemoryStream();
                response.Data.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                stream.Seek(0, SeekOrigin.Begin);
                response.Data.Dispose();
                var message = await channel.SendFileAsync(
                    stream,
                    "image.jpeg",
                    $"Prompt: {request.Prompt}\nModel: {request.Model}{(channelId != null ? $"\nSender: {user.Nickname ?? user.Username}" : "")}");

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
            var model = new ImgRequestModel { Prompt = prompt };
            var seedMatch = new Regex("-seed [0-9]{1,10}", RegexOptions.IgnoreCase).Match(prompt);
            var modelMatch = new Regex("-model (flux|turbo)", RegexOptions.IgnoreCase).Match(prompt);

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

        private async void ImgCache(IDistributedCache _cache, ulong userId, ulong channelId, ulong messageId, ImgRequestModel request)
        {
            var cache = await _cache.Deserialize<List<ImgCacheModel>>(string.Format(Cache.ImgAi, channelId));
            var item = new ImgCacheModel
            {
                Timestamp = DateTime.Now,
                UserId = userId,
                ChannelId = channelId,
                MessageId = messageId,
                Request = request
            };

            if (cache == default)
            {
                var list = new List<ImgCacheModel> { item };

                await _cache.SetStringAsync(string.Format(Cache.ImgAi, channelId),
                    JsonConvert.SerializeObject(list));
            }
            else
            {
                cache.RemoveAll(l => l.ChannelId == channelId && l.UserId == userId);
                cache.Add(item);

                await _cache.SetStringAsync(string.Format(Cache.ImgAi, channelId),
                    JsonConvert.SerializeObject(cache));
            }
        }
    }
}
