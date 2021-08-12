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
        public async Task ExactAsync([Remainder]
            [Summary("Exact name of movie/show")] string input)
        {
            string cachedMovie = await _cache.GetStringAsync(
                string.Format(Cache.Omdb, input.Trim()));
            Movie movieResponse;
            var year = ParseInputForYear(ref input);

            if (string.IsNullOrEmpty(cachedMovie))
            {
                try
                {
                    var endpoint = _options.IanOmdbExactEndpoint;

                    if (!string.IsNullOrEmpty(year))
                    {
                        endpoint += $"&y={year}";
                    }

                    movieResponse = await GetMovieAsync(input, endpoint);
                }
                catch (Exception ex)
                {
                    await ReplyAsync($"Error! {ex.Message}");
                    return;
                }
            }
            else
            {
                movieResponse = JsonSerializer.Deserialize<Movie>(
                    cachedMovie,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }

            await ReplyAsync(null,
                false,
                FormatOmdbResponse(movieResponse));

            HistoryAdd(_cache, GetType().Name, input, apiTiming);
        }

        [Command("rt", RunMode = RunMode.Async)]
        [Summary("Look up movie/tv ratings")]
        public async Task CurrentAsync([Remainder]
            [Summary("Name of movie/show")] string input)
        {
            string cachedResponse = await _cache.GetStringAsync(
                string.Format(Cache.OmdbStubs, input.Trim()));
            OmdbStub stubResponse;

            if (string.IsNullOrEmpty(cachedResponse))
            {
                try
                {
                    stubResponse = await GetStubsAsync(input);
                }
                catch (Exception ex)
                {
                    await ReplyAsync($"Error! {ex.Message}");
                    return;
                }
            }
            else
            {
                var cacheStub = JsonSerializer.Deserialize<CachedMovies>(
                    cachedResponse,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                stubResponse = cacheStub.MovieStubs;
            }

            if (stubResponse?.Response != "True")
            {
                if (!string.IsNullOrEmpty(stubResponse?.Error))
                {
                    await ReplyAsync(stubResponse.Error);
                }
                else
                {
                    await ReplyAsync("No results found, sorry!");
                }
            }
            else
            {
                var imdbID = stubResponse.Search[0].imdbID;
                Movie movieResponse;

                string cachedMovie = await _cache.GetStringAsync(
                    string.Format(Cache.Omdb, imdbID.Trim()));

                if (string.IsNullOrEmpty(cachedMovie))
                {
                    try
                    {
                        movieResponse = await GetMovieAsync(imdbID, _options.IanOmdbEndpoint);
                    }
                    catch (Exception ex)
                    {
                        await ReplyAsync($"Error! {ex.Message}");
                        return;
                    }
                }
                else
                {
                    movieResponse = JsonSerializer.Deserialize<Movie>(
                        cachedMovie,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                }

                await ReplyAsync(null,
                    false,
                    FormatOmdbResponse(movieResponse));
            }

            HistoryAdd(_cache, GetType().Name, input, apiTiming);
        }

        [Command("rtnext", RunMode = RunMode.Async)]
        [Summary("Look up next movie/tv ratings")]
        public async Task NextAsync()
        {
            var cachedResponse = await _cache.GetStringAsync(CacheKey);

            if (cachedResponse?.Length == 0)
            {
                await ReplyAsync("No movies queued.");
            }
            else
            {
                var cached = JsonSerializer.Deserialize<CachedMovies>(cachedResponse);
                cached.LastViewedMovie++;

                if (cached.MovieStubs.Search.Length > cached.LastViewedMovie)
                {
                    await _cache.RemoveAsync(CacheKey);
                    await _cache.SetStringAsync(CacheKey,
                        JsonSerializer.Serialize(cached));

                    var movieResponse = await GetMovieAsync(cached.MovieStubs.Search[cached.LastViewedMovie].imdbID, _options.IanOmdbEndpoint);

                    await ReplyAsync(null,
                        false,
                        FormatOmdbResponse(movieResponse));
                }
                else
                {
                    await ReplyAsync("No more results, idiot.");
                    return;
                }

                HistoryAdd(_cache, GetType().Name, "n/a", apiTiming);
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

        private async Task<Movie> GetMovieAsync(string criteria, string endpoint)
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
