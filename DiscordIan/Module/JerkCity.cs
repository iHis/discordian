using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using DiscordIan.Helper;
using DiscordIan.Key;
using DiscordIan.Model.JerkCity;
using DiscordIan.Service;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DiscordIan.Module
{
    public class JerkCity : BaseModule
    {
        private readonly FetchService _fetchService;
        private readonly Model.Options _options;
        private readonly IDistributedCache _cache;
        private TimeSpan apiTiming = new TimeSpan();

        private string CacheKey
        {
            get
            {
                return string.Format(Cache.JerkCity, Context.User.Id);
            }
        }

        public JerkCity(FetchService fetchService,
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

        [Command("jerk", RunMode = RunMode.Async)]
        [Summary("Returns Jerk City quotes.")]
        [Alias("j")]
        public async Task JerkCityAsync([Remainder]
            [Summary("Search Criteria")] string input = null)
        {
            if (string.IsNullOrEmpty(input))
            {
                await JerkCityRandom();
                return;
            }

            if (string.IsNullOrWhiteSpace(_options.IanJerkCityEndpoint))
            {
                await ReplyAsync("You must configure Jerk City to obtain your jerks!");
                return;
            }

            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "DiscorIan Discord bot" }
            };

            var uri = new Uri(string.Format(_options.IanJerkCityEndpoint,
                HttpUtility.UrlEncode(input)
                ));

            var jerkResult = await _fetchService
                .GetAsync<JerkCityModel.JerkResponse>(uri, headers);
            apiTiming += jerkResult.Elapsed;

            if (jerkResult.IsSuccessful)
            {
                var response = jerkResult.Data;

                if (response == null)
                {
                    await ReplyAsync("Error in Jerk City response.");
                    return;
                }
                if (response.episodes.Count() == 0)
                {
                    await ReplyAsync("No Jerks Found.");
                    return;
                }

                await _cache.RemoveAsync(CacheKey);
                await _cache.SetStringAsync(CacheKey,
                    JsonSerializer.Serialize(new CachedJerks
                    {
                        CreatedAt = DateTime.Now,
                        JerkList = jerkResult.Data.episodes.Select(j =>
                            uri.BaseUrl() + j.image).ToArray(),
                        SearchString = input
                    }));

                var url = uri.BaseUrl() + response.episodes[0].image;

                await ReplyAsync(null, 
                    false,
                    FormatJerks(input, 1, response.episodes.Count(), url));
            }
            else
            {
                await ReplyAsync($"Jerk City failure: {jerkResult.Message}");
            }

            HistoryAdd(_cache, GetType().Name, input, apiTiming);
        }

        [Command("jerknext", RunMode = RunMode.Async)]
        [Summary("Returns next cached Jerk City quote.")]
        [Alias("jn")]
        public async Task JerkCityNextAsync()
        {
            var cachedString = await _cache.GetStringAsync(CacheKey);

            if (cachedString?.Length == 0)
            {
                await ReplyAsync($"I've got nothing for you, {Context.User.Username}");
                return;
            }
            else
            {
                var cached = JsonSerializer.Deserialize<CachedJerks>(cachedString);
                cached.LastViewedJerk++;

                if (string.IsNullOrEmpty(cached.JerkList[cached.LastViewedJerk]))
                {
                    await ReplyAsync($"I've got nothing for you, {Context.User.Username}");
                    return;
                }

                if (cached.JerkList.Length > cached.LastViewedJerk)
                {
                    await _cache.RemoveAsync(CacheKey);
                    await _cache.SetStringAsync(CacheKey,
                        JsonSerializer.Serialize(cached));

                    await ReplyAsync(null,
                        false,
                        FormatJerks(cached.SearchString, 
                            cached.LastViewedJerk + 1, 
                            cached.JerkList.Length, 
                            cached.JerkList[cached.LastViewedJerk]));
                }
            }
        }

        [Command("jerkzewt", RunMode = RunMode.Async)]
        [Summary("Returns single pane from Jerk City comic.")]
        [Alias("jz")]
        public async Task JerkCitySingleCell([Remainder]
            [Summary("Input is comic episode number, comic pane layout (ex: 3x3), and cell selection (ex: 2,1)")] string input = null)
        {
            if (string.IsNullOrEmpty(input))
            {
                await ReplyAsync("You must give an input, you jerk!");
                return;
            }

            var args = input.Split(" ");

            if (args.Length == 3
                && int.TryParse(args[0], out int episode)
                && Regex.IsMatch(args[1], @"^[0-9]x[0-9]$")
                && Regex.IsMatch(args[2], @"^[0-9]$"))
            {
                var layoutInput = args[1].Split("x");
                var selectionInput = args[2];

                var jerkUri = new Uri(_options.IanJerkCityEndpoint);
                var url = $"{jerkUri.Scheme + Uri.SchemeDelimiter + jerkUri.Host}/{episode}.gif";

                var imageResponse = await _fetchService.GetImageAsync(new Uri(url));
                apiTiming += imageResponse.Elapsed;

                var layout = new Tuple<int, int>(Convert.ToInt32(layoutInput[0]), Convert.ToInt32(layoutInput[1]));
                var singleCell = ImageHelper.ClipComicSection(imageResponse.Data, layout, Convert.ToInt32(selectionInput));

                using (var stream = new MemoryStream())
                {
                    singleCell.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                    stream.Seek(0, SeekOrigin.Begin);
                    singleCell.Dispose();
                    await Context.Channel.SendFileAsync(stream, "image.jpeg", string.Empty);
                }
            }
            else
            {
                await ReplyAsync("Format your input correctly, jerk!");
                return;
            }

            HistoryAdd(_cache, GetType().Name, input, apiTiming);
        }

        private async Task JerkCityRandom()
        {
            await _cache.RemoveAsync(CacheKey);

            if (string.IsNullOrWhiteSpace(_options.IanJerkCityEndpoint))
            {
                await ReplyAsync("You must configure Jerk City to obtain your jerks!");
                return;
            }

            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "DiscorIan Discord bot" }
            };

            var uri = new Uri(_options.IanJerkCityRandomEndpoint);

            var jerkRandom = await _fetchService
                .GetAsync<JerkCityModel.JerkResponse>(uri, headers);
            apiTiming += jerkRandom.Elapsed;

            if (jerkRandom.IsSuccessful)
            {
                var response = jerkRandom.Data;

                if (response == null)
                {
                    await ReplyAsync("Error in Jerk City response.");
                    return;
                }
                if (response.episodes.Count() == 0)
                {
                    await ReplyAsync("No Jerks Found.");
                    return;
                }

                var url = uri.BaseUrl() + response.episodes[0].image;

                await ReplyAsync(null,
                    false,
                    FormatJerks(string.Format("Episode {0}", response.episodes[0].episode),
                        0,
                        0,
                        url));
            }
            else
            {
                await ReplyAsync($"Jerk City failure: {jerkRandom.Message}");
            }

            HistoryAdd(_cache, GetType().Name, "n/a", apiTiming);
        }

        private Embed FormatJerks(string title, int index, int total, string url)
        {
            var ep = Regex.Match(url, @"(?<=\/)[^\/\n]+(?=\.)", RegexOptions.RightToLeft);
            if (index > 0 && total > 0)
            {
                title = string.Format("{0}: ({1}/{2})\nEpisode: {3}",
                    title,
                    index,
                    total,
                    ep);
            }

            return new EmbedBuilder()
            {
                Description = title,
                ImageUrl = url
            }.Build();
        }
    }
}
