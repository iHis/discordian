using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using DiscordIan.Helper;
using DiscordIan.Model.GameReview;
using DiscordIan.Service;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DiscordIan.Module
{
    public class GameReview : BaseModule
    {
        private readonly FetchService _fetchService;
        private readonly Model.Options _options;
        private readonly IDistributedCache _cache;
        private TimeSpan apiTiming = new TimeSpan();

        public GameReview(FetchService fetchService,
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

        [Command("game", RunMode = RunMode.Async)]
        [Summary("Returns video game information.")]
        public async Task GameReviewAsync([Remainder]
            [Summary("Search Criteria")] string input)
        {
            if (string.IsNullOrEmpty(_options.IanGameSummaryEndpoint) ||
                string.IsNullOrEmpty(_options.IanGameDetailEndpoint))
            {
                await ReplyAsync("You must configure game review endpoint.");
                return;
            }

            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "DiscorIan Discord bot" }
            };

            var uriSummary = new Uri(string.Format(_options.IanGameSummaryEndpoint,
                HttpUtility.UrlEncode(input),
                _options.IanGameKey
                ));

            var summaryResult = await _fetchService
                .GetAsync<GameSummaryModel.Summary>(uriSummary, headers);
            apiTiming += summaryResult.Elapsed;

            if (summaryResult.IsSuccessful)
            {
                var summaryData = summaryResult.Data;

                if (summaryData == null)
                {
                    await ReplyAsync("Error in response.");
                    return;
                }
                if (summaryData.Results.Count() == 0)
                {
                    await ReplyAsync("No Games Found.");
                    return;
                }
                var uriDetail = new Uri(string.Format(_options.IanGameDetailEndpoint,
                    HttpUtility.UrlEncode(summaryData.Results[0].Slug),
                    _options.IanGameKey
                    ));

                var detailResult = await _fetchService
                    .GetAsync<GameDetailModel.Detail>(uriDetail, headers);
                apiTiming += detailResult.Elapsed;

                if (detailResult.IsSuccessful)
                {
                    var detailData = detailResult.Data;

                    if (detailData == null)
                    {
                        await ReplyAsync("Error in response.");
                        return;
                    }

                    await ReplyAsync(null, false,
                        FormatResponse(summaryData, detailData));
                }
                else
                {
                    await ReplyAsync($"Game summary call failure: {detailResult.Message}");
                }
            }
            else
            {
                await ReplyAsync($"Game summary call failure: {summaryResult.Message}");
            }

            HistoryAdd(_cache, GetType().Name, input, apiTiming);
        }

        private Embed FormatResponse(GameSummaryModel.Summary summary, GameDetailModel.Detail details)
        {
            var gameSummary = summary.Results[0];
            var description = details.Description_Raw
                            .IsNullOrEmptyReplace(details.Description.StripHTML());

            if (description.Length > 750)
            {
                description = description.Substring(0, 747) + "...";
            }

            return new EmbedBuilder()
            {
                Author = EmbedHelper.MakeAuthor(gameSummary.Name, details.Website.ValidateUri()),
                Description = description,
                ThumbnailUrl = gameSummary.Background_Image.ValidateUri(),
                Fields = new List<EmbedFieldBuilder>
                {
                    EmbedHelper.MakeField("Metacritic",
                        string.Format("{0}/100",
                            gameSummary.Metacritic?.ToString() ?? "null"),
                        true),
                    EmbedHelper.MakeField("Released",
                        DateHelper.ToWesternDate(gameSummary.Released?.ToString()),
                        true),
                    EmbedHelper.MakeField("Developer",
                        details.Developers[0]?.Name ?? "null",
                        true)
                }
            }.Build();
        }
    }
}
