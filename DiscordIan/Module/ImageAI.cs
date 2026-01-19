using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Encodings.Web;
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
using Newtonsoft.Json.Linq;

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

            var image = new List<IAttachment>()
                .Concat(Context.Message.ReferencedMessage?.Attachments ?? [])
                .Concat(Context.Message.Attachments)
                .FirstOrDefault();

            if (!prompt.Contains("-image") && image != null)
            {
                prompt += $" -image {image.Url}";
            }

            var model = ParseCommandArgs(prompt);

            try
            {
                await CallImageService(model, Context.Message);
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
                        msg.Request.Model = UtilityExtensions.IsNullOrEmptyReplace(model, msg.Request.Model);

                        await CallImageService(msg.Request, Context.Message);
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
                    || messageRef.Reference != null))
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
                    || m.Reference != null
                    || m.Content.StartsWith("Mirrored from"));

                model.ChannelId = message?.Channel?.Id ?? 0;
                model.MessageId = message?.Id ?? 0;
            }

            if (model != null && model.MessageId != 0)
            {
                var channel = _client.GetChannel(model.ChannelId) as ITextChannel;
                var message = await channel.GetMessageAsync(model.MessageId);

                if (message != null)
                {
                    await SendToChannel(_options.NopeImageChannel, message);

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
                if (messageRef.Author.Id == _client.CurrentUser.Id && messageRef.ReferencedMessage != null)
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
                        await CallImageService(model, Context.Message, QuakeChannel);
                    }
                }
            }
        }

        [Command("imgbalance", RunMode = RunMode.Async)]
        [Summary("Get account credit balance.")]
        [Alias("ib")]
        public async Task ImageBalance([Summary("Model")] string model = null)
        {
            try
            {
                var header = new Dictionary<string, string> { { "Authorization", $"Bearer {_options.PollinationsAIKey}" } };
                var balanceResponse = await _fetchService.GetAsync<JObject>(new Uri(_options.PollinationsAIBalanceEndpoint), header);

                if (balanceResponse.IsSuccessful
                    && balanceResponse.Data is JObject obj
                    && obj["balance"] != null)
                {
                    var balance = obj["balance"].ToObject<decimal>();
                    var modelResponse = await _fetchService.GetAsync<List<PollinationModels>>(new Uri(_options.PollinationsAIModelsEndpoint));
                    var models = string.IsNullOrEmpty(model)
                        ? [.. _options.PollinationsAIBalanceModels.Split(',')]
                        : new List<string> { model };
                    var refreshTime = DateTime.Today.AddHours(23).AddMinutes(45);
                    var remaining = (refreshTime - DateTime.UtcNow).TotalSeconds > 0
                        ? (refreshTime - DateTime.UtcNow).TotalSeconds
                        : (refreshTime.AddDays(1) - DateTime.UtcNow).TotalSeconds;
                    var untilRefresh = TimeSpan.FromSeconds(remaining);

                    if (modelResponse.IsSuccessful
                         && modelResponse.Data != null)
                    {
                        var modelInfo = modelResponse.Data
                            .Where(m => models.Contains(m.name, StringComparer.OrdinalIgnoreCase))
                            .OrderBy(m => m.pricing.completionImageTokens)
                            .ThenBy(m => m.name)
                            .Select(m => $"{m.name}: {Math.Floor(balance / (decimal)m.pricing.completionImageTokens)} images")
                            .ToList();

                        await ReplyAsync($"Pollinations credit balance: {Math.Floor(balance * 100)}%\n" +
                            string.Join("\n", modelInfo) +
                            "\n\n" +
                            $"Credits refresh in {untilRefresh.Hours}h {untilRefresh.Minutes}m");
                    }
                    else
                    {
                        await ReplyAsync($"Account balance: {Math.Floor(balance * 100)}%\n" +
                            "Could not retrieve model information.");
                    }
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync(ex.Message);
            }
        }

        private async Task CallImageService(ImgRequestModel request, SocketUserMessage origMsg, ulong? channelId = null)
        {
            var url = string.Format(_options.PollinationsAIEndpoint,
                Uri.EscapeDataString(request.Prompt),
                request.Model,
                request.Seed,
                _options.PollinationsAIKey);

            if (!string.IsNullOrEmpty(request.ImageUrl))
            {
                url += $"&image={request.ImageUrl}";
            }

            var header = new Dictionary<string, string> { { "Authorization", $"Bearer {_options.PollinationsAIKey}" } };

            var response = await _fetchService.GetImageAsync(new Uri(url), header);
            apiTiming += response.Elapsed;

            if (response.IsSuccessful)
            {
                var channel = channelId != null
                    ? _client.GetChannel((ulong)channelId) as ISocketMessageChannel
                    : Context.Channel;
                var user = await Context.Channel.GetUserByName(Context.User.Username);
                
                using var stream = new MemoryStream(response.Data);

                var messageReference = new MessageReference(Context.Message.Id);
                var message = messageReference.ChannelId != 0
                    ? await channel.SendFileAsync(
                        stream,
                        "image.jpeg",
                        $"Prompt: {request.Prompt}\nModel: {request.Model}{(channelId != null ? $"\nSender: {user.Nickname ?? user.Username}" : "")}")
                    : await channel.SendFileAsync(stream, "image.jpeg", messageReference: messageReference);
                
                await ImgCache(_cache, Context.User.Id, channel.Id, message.Id, request);
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
            var modelMatch = new Regex("-model ([a-zA-Z0-9]+)", RegexOptions.IgnoreCase).Match(prompt);
            var imageMatch = new Regex("-image ([^\\s]+)", RegexOptions.IgnoreCase).Match(prompt);

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

            if (imageMatch.Success)
            {
                model.ImageUrl = WebUtility.UrlEncode(imageMatch.Value.Split(' ')[1]);
                model.Prompt = model.Prompt.Replace(imageMatch.Value, string.Empty).Trim();
            }

            if (model.Prompt.Contains("-model") || model.Prompt.Contains("-seed"))
            {
                throw new ArgumentException("Invalid prompt format. Please use -model and -seed flags correctly.");
            }

            return model;
        }

        private async Task ImgCache(IDistributedCache _cache, ulong userId, ulong channelId, ulong messageId, ImgRequestModel request)
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

        private async Task SendToChannel(string channelName, IMessage message)
        {
            var channel = _client.Guilds
                .First(g => g.Id == Context.Guild.Id)
                .Channels
                .FirstOrDefault(c => c.Name.Equals(channelName, StringComparison.OrdinalIgnoreCase));
            if (channel is ISocketMessageChannel msgChannel)
            {
                await SendToChannel(msgChannel.Id, message);
            }
        }

        private async Task SendToChannel(ulong channelId, IMessage message)
        {
            if (_client.GetChannel(channelId) is ISocketMessageChannel channel
                && Context.Channel.Id != channel.Id)
            {
                var msgUrl = message.Attachments?.FirstOrDefault()?.Url;

                if (!string.IsNullOrEmpty(msgUrl))
                {
                    var bytes = await ImageHelper.GetImageFromURI(new Uri(msgUrl));

                    using var stream = new MemoryStream(bytes);
                    await channel.SendFileAsync(stream, "image.jpeg", $"Mirrored from <#{Context.Channel.Id}> (requested by {Context.User.Mention})");
                }
            }
        }
    }
}
