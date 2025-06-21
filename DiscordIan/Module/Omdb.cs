using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using DiscordIan.Helper;
using DiscordIan.Key;
using DiscordIan.Model.Omdb;
using DiscordIan.Service;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DiscordIan.Module
{
    public class Omdb : BaseModule
    {
        private readonly IDistributedCache _cache;
        private readonly FetchService _fetchService;
        private readonly Model.Options _options;
        private TimeSpan apiTiming = new TimeSpan();

        private string CacheKey
        {
            get
            {
                return string.Format(Cache.OmdbStubs, Context.Channel.Id);
            }
        }

        public Omdb(IDistributedCache cache,
            FetchService fetchService,
            IOptionsMonitor<Model.Options> optionsAccessor)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _fetchService = fetchService
                ?? throw new ArgumentNullException(nameof(fetchService));
            _options = optionsAccessor.CurrentValue
                ?? throw new ArgumentNullException(nameof(optionsAccessor));
        }

        [Command("rtexact", RunMode = RunMode.Async)]
        [Summary("Look up movie/tv ratings, exact title")]
        [Alias("rte")]
        public async Task ExactAsync([Remainder]
            [Summary("Exact name of movie/show")] string input)
        {
            var movieResponse = await GetExactMovieAsync(input);

            await ReplyAsync(null, false, FormatOmdbResponse(movieResponse));

            HistoryAdd(_cache, GetType().Name, input, apiTiming);
        }

        [Command("rt", RunMode = RunMode.Async)]
        [Summary("Look up movie/tv ratings")]
        public async Task CurrentAsync([Remainder]
            [Summary("Name of movie/show")] string input)
        {
            var movieResponse = Regex.IsMatch(input, "\\([0-9][0-9][0-9][0-9]\\)$") ?
                await GetExactMovieAsync(input) ?? await SearchMovieAsync(input)
                : await SearchMovieAsync(input);

            if (movieResponse != null)
            {
                await ReplyAsync(null, false, FormatOmdbResponse(movieResponse));

                HistoryAdd(_cache, GetType().Name, input, apiTiming);
            }
        }

        [Command("rtnext", RunMode = RunMode.Async)]
        [Summary("Look up next movie/tv ratings")]
        [Alias("rtn")]
        public async Task NextAsync()
        {
            var cache = await _cache.Deserialize<CachedMovies>(CacheKey);

            if (cache == default)
            {
                await ReplyAsync("No movies queued.");
            }
            else
            {
                cache.LastViewedMovie++;

                if (cache.MovieStubs.Search.Length > cache.LastViewedMovie)
                {
                    await _cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(cache));

                    var movieResponse = await CallOMDB(cache.MovieStubs.Search[cache.LastViewedMovie].imdbID, _options.IanOmdbEndpoint);

                    await ReplyAsync(null, false, FormatOmdbResponse(movieResponse));
                }
                else
                {
                    await ReplyAsync("No more results, idiot.");
                    return;
                }

                HistoryAdd(_cache, GetType().Name, "n/a", apiTiming);
            }
        }

        private async Task<Movie> GetExactMovieAsync(string input)
        {
            var cache = await _cache.Deserialize<Movie>(string.Format(Cache.Omdb, input.Trim()));

            Movie movieResponse;
            var year = ParseInputForYear(ref input);

            if (cache == default)
            {
                var endpoint = _options.IanOmdbExactEndpoint;

                if (!string.IsNullOrEmpty(year))
                {
                    endpoint += $"&y={year}";
                }

                movieResponse = await CallOMDB(input, endpoint);
            }
            else
            {
                movieResponse = cache;
            }

            return movieResponse;
        }

        private async Task<Movie> SearchMovieAsync(string input)
        {
            var cache = await _cache.Deserialize<CachedMovies>(string.Format(Cache.OmdbStubs, input.Trim()));

            OmdbStub stubResponse;

            if (cache == default)
            {
                try
                {
                    stubResponse = await GetStubsAsync(input);
                }
                catch (Exception ex)
                {
                    await ReplyAsync($"Error! {ex.Message}");
                    return null;
                }
            }
            else
            {
                stubResponse = cache.MovieStubs;
            }

            if (stubResponse?.Response != "True")
            {
                if (!string.IsNullOrEmpty(stubResponse?.Error))
                {
                    await ReplyAsync(stubResponse.Error);
                    return null;
                }
                else
                {
                    await ReplyAsync("Invalid response data.");
                    return null;
                }
            }
            else
            {
                Movie movieResponse;
                var imdbID = stubResponse.Search[0].imdbID;

                var cachedMovie = await _cache.Deserialize<Movie>(string.Format(Cache.Omdb, imdbID.Trim()));

                if (cachedMovie == default)
                {
                    try
                    {
                        movieResponse = await CallOMDB(imdbID, _options.IanOmdbEndpoint);
                    }
                    catch (Exception ex)
                    {
                        await ReplyAsync($"Error! {ex.Message}");
                        return null;
                    }
                }
                else
                {
                    movieResponse = cachedMovie;
                }

                return movieResponse;
            }
        }

        private async Task<OmdbStub> GetStubsAsync(string input)
        {
            var year = ParseInputForYear(ref input);

            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "DiscorIan Discord bot" }
            };

            var endpoint = string.Format(_options.IanOmdbSearchEndpoint,
                HttpUtility.UrlEncode(input),
                _options.IanOmdbKey);

            if (!string.IsNullOrEmpty(year))
            {
                endpoint += $"&y={year}";
            }

            var uri = new Uri(endpoint);

            var response = await _fetchService.GetAsync<OmdbStub>(uri, headers);
            apiTiming += response.Elapsed;

            if (response.IsSuccessful)
            {
                var data = response.Data;

                if (data == null)
                {
                    throw new Exception("Invalid response data.");
                }

                if (data.Response == "False")
                {
                    return data;
                }

                data.Search =
                    data.Search
                    .Where(m => m.Title.ToLower() == input.ToLower())
                    .Concat(data.Search.Where(m => m.Title.ToLower() != input.ToLower()))
                    .ToArray();

                var stubCache = new CachedMovies
                {
                    LastViewedMovie = 0,
                    MovieStubs = data
                };

                await _cache.SetStringAsync(CacheKey,
                    JsonSerializer.Serialize(stubCache),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4)
                    });

                return data;
            }

            return null;
        }

        private async Task<Movie> CallOMDB(string criteria, string endpoint)
        {
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "DiscorIan Discord bot" }
            };

            var uri = new Uri(string.Format(endpoint,
                HttpUtility.UrlEncode(criteria),
                _options.IanOmdbKey));

            var response = await _fetchService.GetAsync<Movie>(uri, headers);
            apiTiming += response.Elapsed;

            if (response.IsSuccessful)
            {
                var data = response.Data;

                if (data == null)
                {
                    throw new Exception("Invalid response data.");
                }

                await _cache.SetStringAsync(
                    string.Format(Cache.Omdb, criteria.Trim()),
                    JsonSerializer.Serialize(data),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4)
                    });

                return data;
            }

            return null;
        }

        private Embed FormatOmdbResponse(Movie response)
        {
            string titleUrl = string.Empty;
            var ratings = new StringBuilder("*none*");

            if (response.Ratings?.Length > 0)
            {
                ratings.Clear();

                foreach (var rating in response.Ratings)
                {
                    ratings.AppendFormat("{0}: {1}",
                            rating.Source.Replace("Internet Movie Database", "IMDB"),
                            rating.Value)
                        .AppendLine();
                }
            }

            if (!string.IsNullOrEmpty(response.ImdbId))
            {
                titleUrl = string.Format(_options.IanImdbIdUrl,
                        response.ImdbId);
            }

            return new EmbedBuilder
            {
                Author = EmbedHelper.MakeAuthor(response.Title.WordSwap(_cache), titleUrl),
                Description = response.Plot.WordSwap(_cache),
                ThumbnailUrl = response?.Poster.ValidateUri(),
                Fields = new List<EmbedFieldBuilder>()
                    {
                        EmbedHelper.MakeField("Released:",
                            DateHelper.ToWesternDate(response.Released)),
                        EmbedHelper.MakeField("Actors:", response.Actors.WordSwap(_cache)),
                        EmbedHelper.MakeField("Ratings:", ratings.ToString().Trim())
                    }
            }.Build();
        }

        private string ParseInputForYear(ref string input)
        {
            var splitInput = input.Split(" ");

            if (splitInput.Length == 1)
            {
                return string.Empty;
            }

            var last = splitInput.Last();

            if (Regex.IsMatch(last, "^\\([0-9][0-9][0-9][0-9]\\)$"))
            {
                input = input.Remove(input.IndexOf(last)).Trim();

                return last[1..^1];
            }

            return string.Empty;
        }
    }
}
