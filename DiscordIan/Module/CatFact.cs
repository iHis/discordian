﻿using System;
using System.Threading.Tasks;
using Discord.Commands;
using DiscordIan.Service;
using Microsoft.Extensions.Options;

namespace DiscordIan.Module
{
    public class CatFact : BaseModule
    {
        private readonly FetchJsonService _fetchJsonService;
        private readonly Model.Options _options;

        public CatFact(FetchJsonService fetchJsonService,
            IOptionsMonitor<Model.Options> optionsAccessor)
        {
            _fetchJsonService = fetchJsonService
                ?? throw new ArgumentNullException(nameof(fetchJsonService));
            _options = optionsAccessor.CurrentValue
                ?? throw new ArgumentNullException(nameof(optionsAccessor));
        }

        [Command("catfact", RunMode = RunMode.Async)]
        [Summary("Returns an interesting fact about a member of family Felidae")]
        public async Task CatFactAsync()
        {
            if (string.IsNullOrWhiteSpace(_options.IanCatFactEndpoint))
            {
                await ReplyAsync("You must configure cat facts to obtain cat facts!");
                return;
            }

            var catFactResult = await _fetchJsonService
                .GetAsync<Model.CatFact>(new Uri(_options.IanCatFactEndpoint));

            if (catFactResult.IsSuccessful)
            {
                await ReplyAsync(catFactResult.Data.Fact);
            }
            else
            {
                await ReplyAsync($"Cat fact failure: {catFactResult.Message}");
            }
        }
    }
}
