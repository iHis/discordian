using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using DiscordIan.Service;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DiscordIan.Module
{
    public class ImageAI : BaseModule
    {
        private readonly IDistributedCache _cache;
        private readonly FetchService _fetchService;
        private readonly Model.Options _options;
        private TimeSpan apiTiming = new TimeSpan();

        public ImageAI(IDistributedCache cache,
            FetchService fetchService,
            IOptionsMonitor<Model.Options> optionsAccessor)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _fetchService = fetchService
                ?? throw new ArgumentNullException(nameof(fetchService));
            _options = optionsAccessor.CurrentValue
                ?? throw new ArgumentNullException(nameof(optionsAccessor));
        }

        [Command("imgai", RunMode = RunMode.Async)]
        [Summary("Generate AI image.")]
        [Alias("img")]
        public async Task GenerateInspirationalQuote([Remainder]
            [Summary("Prompt")] string prompt)
        {
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
                await Context.Channel.SendFileAsync(stream, "image.jpeg", string.Empty);
            }
            else
            {
                await ReplyAsync($"API call unsuccessful: {response.Message}");
            }

            HistoryAdd(_cache, GetType().Name, null, apiTiming);
        }
    }
}
