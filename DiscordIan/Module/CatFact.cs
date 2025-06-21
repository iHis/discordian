using System;
using System.Threading.Tasks;
using Discord.Commands;
using DiscordIan.Helper;
using DiscordIan.Service;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DiscordIan.Module
{
    public class CatFact : BaseModule
    {
        private readonly FetchService _fetchService;
        private readonly Model.Options _options;
        private readonly IDistributedCache _cache;
        private TimeSpan apiTiming = new TimeSpan();

        public CatFact(FetchService fetchService,
            IOptionsMonitor<Model.Options> optionsAccessor,
            IDistributedCache cache)
        {
            _fetchService = fetchService
                ?? throw new ArgumentNullException(nameof(fetchService));
            _options = optionsAccessor.CurrentValue
                ?? throw new ArgumentNullException(nameof(optionsAccessor));
            _cache = cache
                ?? throw new ArgumentNullException(nameof(cache));
        }

        [Command("catfact", RunMode = RunMode.Async)]
        [Alias("cats", "cat")]
        [Summary("Returns an interesting fact about a member of family Felidae")]
        public async Task CatFactAsync()
        {
            if (string.IsNullOrWhiteSpace(_options.IanCatFactEndpoint))
            {
                await ReplyAsync("You must configure cat facts to obtain cat facts!");
                return;
            }

            var catFactResult = await _fetchService
                .GetAsync<Model.CatFact>(new Uri(_options.IanCatFactEndpoint));
            apiTiming += catFactResult.Elapsed;

            if (catFactResult.IsSuccessful)
            {
                await ReplyAsync(catFactResult.Data.Fact.WordSwap(_cache));
            }
            else
            {
                await ReplyAsync($"Cat fact failure: {catFactResult.Message}");
            }

            HistoryAdd(_cache, GetType().Name, "n/a", apiTiming);
        }
    }
}
