﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using DiscordIan.Helper;
using DiscordIan.Model.MapQuest;
using DiscordIan.Model.OpenWeatherMap;
using DiscordIan.Service;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DiscordIan.Module
{
    public class OpenWeather : BaseModule
    {
        private readonly IDistributedCache _cache;
        private readonly FetchService _fetchService;
        private readonly Model.Options _options;
        private TimeSpan apiTiming = new TimeSpan();

        public OpenWeather(IDistributedCache cache,
            FetchService fetchService,
            IOptionsMonitor<Model.Options> optionsAccessor)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _fetchService = fetchService
                ?? throw new ArgumentNullException(nameof(fetchService));
            _options = optionsAccessor.CurrentValue
                ?? throw new ArgumentNullException(nameof(optionsAccessor));
        }

        [Command("wz", RunMode = RunMode.Async)]
        [Summary("Look up current weather for a provided address.")]
        [Alias("weather", "w", "wx")]
        public async Task CurrentAsync([Remainder]
            [Summary("The address or location for current weather conditions")] string location = null)
        {
            if (string.IsNullOrEmpty(location))
            {
                var defaultLoc = SqliteHelper.SelectWeatherDefault(Context.User.Id.ToString());
                if (string.IsNullOrEmpty(defaultLoc))
                {
                    await ReplyAsync("No location provided and no default found.  Provide a location or set a default first using !wset or !ws.");
                    return;
                }

                location = defaultLoc;
            }

            string coords = await _cache.GetStringAsync(location);
            string locale = null;
            if (!string.IsNullOrEmpty(coords))
            {
                locale = await _cache.GetStringAsync(coords);
            }

            if (string.IsNullOrEmpty(coords) || string.IsNullOrEmpty(locale))
            {
                try
                {
                    (coords, locale) = await GeocodeAddressAsync(location);

                    if (!string.IsNullOrEmpty(coords) && !string.IsNullOrEmpty(locale))
                    {
                        await _cache.SetStringAsync(location, coords);

                        await _cache.SetStringAsync(coords, locale);
                    }
                }
                catch (Exception)
                {
                    //Geocode failed, revert to OpenWeatherMap's built-in resolve.
                }
            }

            string message;

            if (string.IsNullOrEmpty(coords) || string.IsNullOrEmpty(locale))
            {
                message = await GetWeatherResultAsync(location);
            }
            else
            {
                message = await GetWeatherResultAsync(coords, locale);
            }

            if (string.IsNullOrEmpty(message))
            {
                await ReplyAsync("No weather found, sorry!");
            }
            else
            {
                await ReplyAsync(message.WordSwap(_cache), false, null);
            }

            HistoryAdd(_cache, GetType().Name, location, apiTiming);
        }

        [Command("w2", RunMode = RunMode.Async)]
        [Summary("Look up current weather for a provided address.")]
        public async Task NewCurrentAsync([Remainder]
            [Summary("The address or location for current weather conditions")] string location = null)
        {
            if (string.IsNullOrEmpty(location))
            {
                var defaultLoc = SqliteHelper.SelectWeatherDefault(Context.User.Id.ToString());
                if (string.IsNullOrEmpty(defaultLoc))
                {
                    await ReplyAsync("No location provided and no default found.  Provide a location or set a default first using !wset or !ws.");
                    return;
                }

                location = defaultLoc;
            }

            var embed = await GetNewWeatherResultsAsync(location);

            if (embed == null)
            {
                await ReplyAsync("No weather found, sorry!");
            }
            else
            {
                await ReplyAsync(null, false, embed);
            }

            HistoryAdd(_cache, GetType().Name, location, apiTiming);
        }

        [Command("wset", RunMode = RunMode.Async)]
        [Summary("Set your default weather location.")]
        [Alias("ws")]
        public async Task SetWeatherCode([Summary("The address or location for current weather conditions")] string location = null)
        { 
            if (string.IsNullOrEmpty(location))
            {
                await ReplyAsync("Please include a location to set.");
                return;
            }

            location = location.ToLower();

            SqliteHelper.InsertWeather(Context.User.Id.ToString(), Context.User.Username, location);

            await ReplyAsync($"Default weather location for {Context.User.Username} set to {location}.");

            HistoryAdd(_cache, GetType().Name, location, apiTiming);
        }

        [Command("wpeek", RunMode = RunMode.Async)]
        [Summary("See your default weather location.")]
        public async Task PeekWeatherCode([Summary("User to peek, blank for yourself")] string user = null)
        {
            var userId = user ?? Context.User.Id.ToString();
            var defaultLoc = SqliteHelper.SelectWeatherDefault(userId);

            if (string.IsNullOrEmpty(defaultLoc))
            {
                await ReplyAsync("No default location found.");
                return;
            }

            await ReplyAsync($"{Context.User.Username}: {defaultLoc}");
            return;
        }

        private async Task<string>
            GetWeatherResultAsync(string coordinates, string location)
        {
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "DiscorIan Discord bot" }
            };

            var uriCurrent = new Uri(string.Format(_options.IanOpenWeatherMapEndpointCoords,
                HttpUtility.UrlEncode(coordinates.Split(",")[0]),
                HttpUtility.UrlEncode(coordinates.Split(",")[1]),
                _options.IanOpenWeatherKey));

            var uriForecast = new Uri(string.Format(_options.IanOpenWeatherMapEndpointForecast,
                HttpUtility.UrlEncode(coordinates.Split(",")[0]),
                HttpUtility.UrlEncode(coordinates.Split(",")[1]),
                _options.IanOpenWeatherKey));

            var responseCurrent = await _fetchService
                .GetAsync<WeatherCurrent.Current>(uriCurrent, headers);
            apiTiming += responseCurrent.Elapsed;

            if (responseCurrent.IsSuccessful)
            {
                string message;
                var currentData = responseCurrent.Data;

                var responseForecast = await _fetchService
                    .GetAsync<WeatherForecast.Forecast>(uriForecast, headers);
                apiTiming += responseForecast.Elapsed;

                if (responseForecast.IsSuccessful)
                {
                    var forecastData = responseForecast.Data;

                    if (forecastData.Daily == null || forecastData.Daily.Length == 0)
                        throw new Exception("Today doesn't exist?!");

                    var today = forecastData.Daily[0];

                    message = FormatResults(currentData, location, today);
                }
                else
                {
                    message = FormatResults(currentData, location);
                }

                return message;
            }

            return null;
        }

        private async Task<string> GetWeatherResultAsync(string input)
        {
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "DiscorIan Discord bot" }
            };

            var uri = new Uri(string.Format(_options.IanOpenWeatherMapEndpointQ,
                HttpUtility.UrlEncode(input),
                _options.IanOpenWeatherKey));

            var response = await _fetchService
                .GetAsync<WeatherCurrent.Current>(uri, headers);
            apiTiming += response.Elapsed;

            if (response.IsSuccessful)
            {
                var data = response.Data;
                var locale = string.Format("{0}, {1}", data.City.Name, data.City.Country);
                var latlong = string.Format("{0},{1}", data.City.Coord.Lat, data.City.Coord.Lon);

                await _cache.SetStringAsync(input,
                    latlong,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4)
                    });

                await _cache.SetStringAsync(latlong,
                    locale,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4)
                    });

                string message = FormatResults(data, locale);

                return message;
            }

            return null;
        }

        private async Task<(string, string)> GeocodeAddressAsync(string location)
        {
            if (string.IsNullOrEmpty(_options.IanMapQuestEndpoint)
                || string.IsNullOrEmpty(_options.IanMapQuestKey))
            {
                throw new Exception("Geocoding is not configured.");
            }

            var uri = new Uri(string.Format(_options.IanMapQuestEndpoint,
                HttpUtility.UrlEncode(location),
                _options.IanMapQuestKey));

            var response = await _fetchService.GetAsync<MapQuest>(uri);
            apiTiming += response.Elapsed;

            if (response.IsSuccessful)
            {
                if (response?.Data?.Results != null)
                {
                    var loc = response?.Data?.Results[0]?.Locations[0];
                    string precision = loc.GeocodeQuality;

                    string geocode = string.Format("{0},{1}", loc.LatLng.Lat, loc.LatLng.Lng);
                    string locale;

                    if (precision == "COUNTRY")
                    {
                        locale = loc.AdminArea1;
                    }
                    else if (precision == "STATE")
                    {
                        locale = string.Format("{0}, {1}", loc.AdminArea3, loc.AdminArea1);
                    }
                    else if (precision == "CITY" && string.IsNullOrEmpty(loc.AdminArea3))
                    {
                        locale = string.Format("{0}, {1}", loc.AdminArea5, loc.AdminArea1);
                    }
                    else if (string.IsNullOrEmpty(loc.AdminArea5))
                    {
                        locale = string.Format("{0}, {1}", loc.AdminArea3, loc.AdminArea1);
                    }
                    else
                    {
                        locale = string.Format("{0}, {1}", loc.AdminArea5, loc.AdminArea3);
                    }

                    return (geocode, locale);
                }
            }
            else
            {
                throw new Exception(response.Message);
            }

            return (null, null);
        }

        private async Task<Embed> GetNewWeatherResultsAsync(string input)
        {
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "DiscorIan Discord bot" }
            };

            var uri = new Uri(string.Format(_options.WeatherApiEndpoint,
                _options.WeatherApiKey,
                HttpUtility.UrlEncode(input)));

            var response = await _fetchService
                .GetAsync<WeatherApiModel>(uri, headers);
            apiTiming += response.Elapsed;

            if (response.IsSuccessful)
            {
                return FormatResultsNew(response.Data);
            }

            return null;
        }

        private string FormatResults(WeatherCurrent.Current data,
            string location,
            WeatherForecast.Daily forecastData = null)
        {
            var sb = new StringBuilder()
                    .AppendFormat(">>> **{0}:** {1} {2}",
                        location,
                        data.Weather.Value.ToTitleCase(),
                        WeatherIcon(data.Weather.Icon))
                    .AppendLine()

                    .AppendFormat("**Temp:** {0}°{1} **Feels Like:** {2}°{3} **Humidity:** {4}{5}",
                        data.Temperature.Value,
                        data.Temperature.Unit.ToUpper()[0],
                        data.Feels_like.Value,
                        data.Feels_like.Unit.ToUpper()[0],
                        data.Humidity.Value,
                        data.Humidity.Unit)
                    .AppendLine()

                    .AppendFormat("**Wind:** {0} **Speed:** {1}{2} {3}",
                        data.Wind.Speed.Name.ToTitleCase(),
                        data.Wind.Speed.Value,
                        data.Wind.Speed.Unit,
                        data.Wind.Direction.Code);

            if (!string.IsNullOrEmpty(data.Wind.Gusts))
            {
                sb.AppendFormat(" **Gusts:** {0}{1}",
                        data.Wind.Gusts,
                        data.Wind.Speed.Unit);
            }

            if (forecastData != null)
            {
                sb.AppendLine()
                    .AppendFormat("**High:** {0}°{1} **Low:** {2}°{3}",
                        forecastData.Temp.Max.ToString(),
                        data.Temperature.Unit.ToUpper()[0],
                        forecastData.Temp.Min.ToString(),
                        data.Temperature.Unit.ToUpper()[0])
                    .AppendLine()
                    .AppendFormat("**Forecast:** {0} {1}",
                        forecastData.Weather[0].Description.ToTitleCase(),
                        WeatherIcon(forecastData.Weather[0].Icon));
            }

            return sb.ToString().Trim();
        }

        private Embed FormatResultsNew(WeatherApiModel data)
        {
            return new EmbedBuilder()
            {
                Color = Color.Blue,
                Title = string.Format("{0}, {1}", data.Location.Name, data.Location.Region),
                ThumbnailUrl = $"http:{data.Current.Condition.Icon}",
                Fields = new List<EmbedFieldBuilder>() {
                    EmbedHelper.MakeField($"Condition: **{data.Current.Condition.Text}**",
                        $"  **Temp:** {data.Current.TempF}F / {data.Current.TempC}C"
                        + "\n" +
                        $"  **Feels Like:** {data.Current.FeelslikeF}F / {data.Current.FeelslikeC}C"
                        + "\n" +
                        $"  **Humidity:** {data.Current.Humidity}%"),
                    EmbedHelper.MakeField("Wind:",
                        $"  **Speed:** {data.Current.WindMph}mph / {data.Current.WindKph}kph"
                        + "\n" +
                        $"  **Direction:** {data.Current.WindDir}"),
                    EmbedHelper.MakeField($"Forecast: **{data.Forecast.Forecastday[0].Day.Condition.Text}**",
                        $"  **High:** {data.Forecast.Forecastday[0].Day.MaxtempF}F / {data.Forecast.Forecastday[0].Day.MaxtempC}C"
                        + "\n" +
                        $"  **Low:** {data.Forecast.Forecastday[0].Day.MintempF}F / {data.Forecast.Forecastday[0].Day.MintempC}C")
                }
            }.Build();
        }

        private string WeatherIcon(string iconCode)
        {
            switch (iconCode)
            {
                case "01d":
                    return ":sunny:";
                case "01n":
                    return ":first_quarter_moon_with_face:";
                case "02d":
                    return ":white_sun_cloud:";
                case "02n":
                case "03d":
                case "03n":
                case "04d":
                case "04n":
                    return ":cloud:";
                case "09d":
                case "09n":
                case "10n":
                    return ":cloud_rain:";
                case "10d":
                    return ":white_sun_rain_cloud:";
                case "11d":
                case "11n":
                    return ":thunder_cloud_rain:";
                case "13d":
                case "13n":
                    return ":snowflake:";
                case "50d":
                case "50n":
                    return ":sweat_drops:";
                default:
                    return string.Empty;
            }
        }
    }
}
