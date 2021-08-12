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
using DiscordIan.Model;
using DiscordIan.Model.Omdb;
using DiscordIan.Service;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DiscordIan.Module
{
    public class Books : BaseModule
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

        public Books(IDistributedCache cache,
            FetchService fetchService,
            IOptionsMonitor<Model.Options> optionsAccessor)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _fetchService = fetchService
                ?? throw new ArgumentNullException(nameof(fetchService));
            _options = optionsAccessor.CurrentValue
                ?? throw new ArgumentNullException(nameof(optionsAccessor));
        }

        [Command("book", RunMode = RunMode.Async)]
        [Summary("Look up book info")]
        public async Task CurrentAsync([Remainder]
            [Summary("Name of book")] string input)
        {
            string cachedResponse = await _cache.GetStringAsync(
                string.Format(Key.Cache.BookList, input.Trim()));
            BookList listResponse;

            if (string.IsNullOrEmpty(cachedResponse))
            {
                try
                {
                    listResponse = await GetBooksAsync(input);
                }
                catch (Exception ex)
                {
                    await ReplyAsync($"Error! {ex.Message}");
                    return;
                }
            }
            else
            {
                var cacheBooks = JsonSerializer.Deserialize<CachedBooks>(
                    cachedResponse,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                listResponse = cacheBooks.BookList;
            }

            if (listResponse?.TotalItems == 0)
            {
                await ReplyAsync("No results found, sorry!");
            }
            else
            {
                var book = listResponse.Items[0];

                await ReplyAsync(null,
                    false,
                    FormatBookResponse(book));
            }

            HistoryAdd(_cache, GetType().Name, input, apiTiming);
        }

        [Command("bknext", RunMode = RunMode.Async)]
        [Summary("Look up next book")]
        public async Task NextAsync()
        {
            var cachedResponse = await _cache.GetStringAsync(CacheKey);

            if (cachedResponse?.Length == 0)
            {
                await ReplyAsync("No books queued.");
            }
            else
            {
                var cached = JsonSerializer.Deserialize<CachedBooks>(cachedResponse);
                cached.LastViewedBook++;

                if (cached.BookList.TotalItems > cached.LastViewedBook)
                {
                    await _cache.RemoveAsync(CacheKey);
                    await _cache.SetStringAsync(CacheKey,
                        JsonSerializer.Serialize(cached));

                    await ReplyAsync(null,
                        false,
                        FormatBookResponse(cached.BookList.Items[cached.LastViewedBook]));
                }
                else
                {
                    await ReplyAsync("No more results, idiot.");
                    return;
                }

                HistoryAdd(_cache, GetType().Name, "n/a", apiTiming);
            }
        }

        private async Task<BookList> GetBooksAsync(string input)
        {
            var author = ParseInputForAuthor(ref input);

            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "DiscorIan Discord bot" }
            };

            if (!string.IsNullOrEmpty(author))
            {
                input += $"+inauthor:{author}";
            }

            var endpoint = string.Format(_options.IanBooksEndpoint,
                HttpUtility.UrlEncode(input),
                _options.IanBooksKey);

            var uri = new Uri(endpoint);

            var response = await _fetchService.GetAsync<BookList>(uri, headers);
            apiTiming += response.Elapsed;

            if (response.IsSuccessful)
            {
                var data = response.Data;

                if (data == null)
                {
                    throw new Exception("Invalid response data.");
                }

                var bookCache = new CachedBooks
                {
                    LastViewedBook = 0,
                    BookList = data
                };

                await _cache.SetStringAsync(CacheKey,
                    JsonSerializer.Serialize(bookCache),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4)
                    });

                return data;
            }

            return null;
        }

        private Embed FormatBookResponse(Item response)
        {
            string titleUrl = Extensions.IsNullOrEmptyReplace(
                response.VolumeInfo.PreviewLink.ValidateUri(), 
                string.Empty);

            string description = Extensions.IsNullOrEmptyReplace(
                response.VolumeInfo.Description, 
                "No description available.");

            string authors = response.VolumeInfo.Authors != null ?
                string.Join(", ", response.VolumeInfo.Authors)
                : "N/A";

            string publishDate = Extensions.IsNullOrEmptyReplace(
                response.VolumeInfo.PublishedDate,
                "N/A");

            string thumb = Extensions.IsNullOrEmptyReplace(
                response.VolumeInfo.ImageLinks.SmallThumbnail.ValidateUri(),
                string.Empty);

            return new EmbedBuilder
            {
                Author = EmbedHelper.MakeAuthor(response.VolumeInfo.Title.WordSwap(_cache), titleUrl),
                Description = description.WordSwap(_cache),
                ThumbnailUrl = thumb,
                Fields = new List<EmbedFieldBuilder>()
                    {
                        EmbedHelper.MakeField("Author:", 
                            authors.WordSwap(_cache)),
                        EmbedHelper.MakeField("Published:", 
                            publishDate)
                    }
            }.Build();
        }

        private string ParseInputForAuthor(ref string input)
        {
            var splitInput = input.Split(" ");

            if (splitInput.Length == 1)
            {
                return string.Empty;
            }

            var last = splitInput.Last();

            if (Regex.IsMatch(last, "^\\([a-zA-Z]+\\)$"))
            {
                input = input.Remove(input.IndexOf(last)).Trim();

                return last[1..^1];
            }

            return string.Empty;
        }
    }
}
