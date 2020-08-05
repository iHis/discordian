﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using DiscordIan.Model.Stocks;
using DiscordIan.Service;
using DiscordIan.Helper;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Distributed;

namespace DiscordIan.Module
{
    public class Stock : BaseModule
    {
        private readonly FetchService _fetchService;
        private readonly Model.Options _options;
        private readonly IDistributedCache _cache;
        private TimeSpan apiTiming = new TimeSpan();

        public Stock(FetchService fetchService,
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

        [Command("quote", RunMode = RunMode.Async)]
        [Summary("Look up stock quotes.")]
        [Alias("quot", "quo", "q")]
        public async Task CurrentAsync([Remainder]
            [Summary("Stock symbol.")] string input)
        {
            StockModel stockResponse;

            try
            {
                stockResponse = await GetQuoteAsync(input.ToUpper());
            }
            catch (Exception ex)
            {
                if (ex.Message == "Unexpected return message: Symbol not supported")
                {
                    await ReplyAsync("No result found, sorry!");
                    return;
                }

                await ReplyAsync($"Error! {ex.Message}");
                return;
            }

            if (stockResponse?.Company == null || stockResponse?.StockQuote == null)
            {
                await ReplyAsync("No result found, sorry!");
            }
            else
            {
                await ReplyAsync(null,
                    false,
                    FormatStockResponse(stockResponse));
            }

            HistoryAdd(_cache, GetType().Name, input, apiTiming);
        }

        private async Task<StockModel> GetQuoteAsync(string input)
        {
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "DiscorIan Discord bot" }
            };

            var uriCompany = new Uri(string.Format(_options.IanStockCompanyEndpoint,
                HttpUtility.UrlEncode(input),
                _options.IanStockKey));

            var uriQuote = new Uri(string.Format(_options.IanStockQuoteEndpoint,
                HttpUtility.UrlEncode(input),
                _options.IanStockKey));

            var startTime = DateTime.Now;
            var responseCompany = await _fetchService.GetAsync<StockCompany>(uriCompany, headers);
            apiTiming += DateTime.Now - startTime;

            if (responseCompany.IsSuccessful)
            {
                if (responseCompany.Data == null)
                {
                    throw new Exception("Invalid response data.");
                }

                var result = new StockModel
                {
                    Symbol = responseCompany.Data.Ticker,
                    Company = responseCompany.Data
                };

                if (string.IsNullOrEmpty(result.Symbol))
                {
                    result.Symbol = input;
                }

                startTime = DateTime.Now;
                var responseQuote = await _fetchService.GetAsync<StockQuote>(uriQuote, headers);
                apiTiming += DateTime.Now - startTime;

                if (responseQuote.IsSuccessful)
                {
                    if (responseQuote.Data == null)
                    {
                        throw new Exception("Invalid response data.");
                    }

                    result.StockQuote = responseQuote.Data;

                    return result;
                }
                else
                {
                    throw new Exception(responseQuote.Message);
                }
            }
            else
            {
                throw new Exception(responseCompany.Message);
            }
        }

        private Embed FormatStockResponse(StockModel response)
        {
            var Company = response.Company;
            var Quote = response.StockQuote;

            var changeString = new StringBuilder()
                .AppendLine("```diff")
                .AppendLine(PriceChangeToString(
                    Quote.Current, 
                    Quote.PreviousClose))
                .Append("```").ToString().Trim();

            return new EmbedBuilder()
            {
                Color = Color.DarkRed,
                Title = string.Format("{0} ({1})",
                            Company.Name,
                            response.Symbol),
                Description = Company.Exchange,
                Url = Company?.Weburl.ValidateUri(),
                ThumbnailUrl = Company?.Logo.ValidateUri(),
                Fields = new List<EmbedFieldBuilder>() {
                    EmbedHelper.MakeField("Price:", 
                        Quote.Current.ToString()),
                    EmbedHelper.MakeField("Prev Close:", 
                        Quote.PreviousClose.ToString(), 
                        true),
                    EmbedHelper.MakeField("Low:", 
                        Quote.Low.ToString(), 
                        true),
                    EmbedHelper.MakeField("High:", 
                        Quote.High.ToString(), 
                        true),
                    EmbedHelper.MakeField("Change:", 
                        changeString),
                    EmbedHelper.MakeField("Updated:", 
                        DateHelper.UnixTimeToDate(Quote.Timestamp))
                }
            }.Build();
        }
        private static string PriceChangeToString(double current, double previous)
        {
            string signage = string.Empty;
            string change = Math.Round((current - previous), 2).ToString();
            string changePerc;

            if (previous <= current)
            {
                signage = "+";
            }

            if (previous == 0)
            {
                changePerc = "inf";
            }
            else
            {
                changePerc = Math.Round((100 * ((current - previous) / previous)), 2).ToString();
            }

            return string.Format("{0}{1} ({0}{2}%)",
                signage,
                change,
                changePerc
                );
        }
    }
}
