﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using DiscordIan.Helper;
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

        [Command("rt", RunMode = RunMode.Async)]
        [Summary("Look up movie/tv ratings")]
        public async Task CurrentAsync([Remainder]
            [Summary("Name of movie/show")] string input)
        {
            string cachedResponse = await _cache.GetStringAsync(
                string.Format(Key.Cache.Omdb, input.Trim()));
            Movie omdbResponse;

            if (string.IsNullOrEmpty(cachedResponse))
            {
                try
                {
                    omdbResponse = await GetMovieAsync(input);
                }
                catch (Exception ex)
                {
                    await ReplyAsync($"Error! {ex.Message}");
                    return;
                }
            }
            else
            {
                omdbResponse = JsonSerializer.Deserialize<Movie>(
                    cachedResponse,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }

            if (omdbResponse?.Response != "True")
            {
                await ReplyAsync("No result found, sorry!");
            }
            else
            {
                await ReplyAsync(null,
                    false,
                    FormatOmdbResponse(omdbResponse));
            }
        }

        private async Task<Movie> GetMovieAsync(string input)
        {
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "DiscorIan Discord bot" }
            };

            var uri = new Uri(string.Format(_options.IanOmdbEndpoint,
                HttpUtility.UrlEncode(input),
                _options.IanOmdbKey));

            var response = await _fetchService.GetAsync<Movie>(uri, headers);

            if (response.IsSuccessful)
            {
                var data = response.Data;

                if (data == null)
                {
                    throw new Exception("Invalid response data.");
                }

                await _cache.SetStringAsync(
                    string.Format(Key.Cache.Omdb, input.Trim()),
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
                Author = EmbedFormat.MakeAuthor(response.Title, titleUrl),
                Description = response.Plot,
                ThumbnailUrl = response?.Poster.ValidateUri(),
                Fields = new List<EmbedFieldBuilder>()
                    {
                        EmbedFormat.MakeField("Released:", 
                            DateFormat.ToWesternDate(response.Released)),
                        EmbedFormat.MakeField("Actors:", response.Actors),
                        EmbedFormat.MakeField("Ratings:", ratings.ToString().Trim())
                    }
            }.Build();
        }        
    }
}
