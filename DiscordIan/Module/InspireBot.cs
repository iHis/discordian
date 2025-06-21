using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Commands;
using DiscordIan.Service;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DiscordIan.Module
{
    public class InspireBot : BaseModule
    {
        private readonly IDistributedCache _cache;
        private readonly FetchService _fetchService;
        private readonly Model.Options _options;
        private TimeSpan apiTiming = new TimeSpan();

        public InspireBot(IDistributedCache cache,
            FetchService fetchService,
            IOptionsMonitor<Model.Options> optionsAccessor)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _fetchService = fetchService
                ?? throw new ArgumentNullException(nameof(fetchService));
            _options = optionsAccessor.CurrentValue
                ?? throw new ArgumentNullException(nameof(optionsAccessor));
        }

        [Command("inspire", RunMode = RunMode.Async)]
        [Summary("Generate inspirational quote.")]
        [Alias("i")]
        public async Task GenerateInspirationalQuote()
        {
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "DiscorIan Discord bot" }
            };
            var uri = new Uri(_options.InspiroBotEndpoint);
            var response = await _fetchService.GetAsync<string>(uri, headers);
            apiTiming += response.Elapsed;

            if (response.IsSuccessful)
            {
                await ReplyAsync(response.Data);
            }
            else
            {
                await ReplyAsync($"API call unsuccessful: {response.Data}");
            }

            HistoryAdd(_cache, GetType().Name, null, apiTiming);
        }
    }
}
