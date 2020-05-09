﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Discord.Commands;
using DiscordIan.Model.MapQuest;
using DiscordIan.Model.OpenWeatherMap;
using DiscordIan.Service;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.CompilerServices;

namespace DiscordIan.Module
{
    public class OpenWeather : BaseModule
    {
        private readonly IDistributedCache _cache;
        private readonly FetchService _fetchService;
        private readonly Model.Options _options;

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
        [Alias("weather", "w")]
        public async Task CurrentAsync([Remainder]
            [Summary("The address or location for current weather conditions")] string location)
        {
            string coords = await _cache.GetStringAsync(location);
            string locale = null;
            if (!String.IsNullOrEmpty(coords))
                locale = await _cache.GetStringAsync(coords);

            if (String.IsNullOrEmpty(coords) && String.IsNullOrEmpty(location))
            {
                await ReplyAsync("Please provide a location.");
                return;
            }

            if (String.IsNullOrEmpty(coords) || String.IsNullOrEmpty(locale))
            {
                try
                {
                    (coords, locale) = await GeocodeAddressAsync(location);

                    if (!String.IsNullOrEmpty(coords) && !String.IsNullOrEmpty(locale))
                    {
                        await _cache.SetStringAsync(location, coords,
                            new DistributedCacheEntryOptions { 
                                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4) });

                        await _cache.SetStringAsync(coords, locale,
                            new DistributedCacheEntryOptions { 
                                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4) });
                    }
                }
                catch (Exception ex)
                {
                    //Geocode failed, revert to OpenWeatherMap's built-in resolve.
                }
            }

            string message;
            Discord.Embed embed;

            if (String.IsNullOrEmpty(coords) || String.IsNullOrEmpty(locale))
                (message, embed) = await GetWeatherResultAsync(location);
            else
                (message, embed) = await GetWeatherResultAsync(coords, locale);


            if (String.IsNullOrEmpty(message))
            {
                await ReplyAsync("No weather found, sorry!");
            }
            else
            {
                await ReplyAsync(message, false, embed);
            }
        }

        private async Task<(string, Discord.Embed)>
            GetWeatherResultAsync(string coordinates, string location)
        {
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "DiscorIan Discord bot" }
            };

            var uriCurrent = new Uri(String.Format(_options.IanOpenWeatherMapEndpointCoords,
                HttpUtility.UrlEncode(coordinates.Split(",")[0]),
                HttpUtility.UrlEncode(coordinates.Split(",")[1]),
                _options.IanOpenWeatherKey));

            var uriForecast = new Uri(String.Format(_options.IanOpenWeatherMapEndpointForecast,
                HttpUtility.UrlEncode(coordinates.Split(",")[0]),
                HttpUtility.UrlEncode(coordinates.Split(",")[1]),
                _options.IanOpenWeatherKey));

            var responseCurrent = await _fetchService
                .GetAsync<WeatherCurrent.Current>(uriCurrent, headers);

            if (responseCurrent.IsSuccessful)
            {
                string message;
                var currentData = responseCurrent.Data;

                var responseForecast = await _fetchService
                    .GetAsync<WeatherForecast.Forecast>(uriForecast, headers);

                if (responseForecast.IsSuccessful)
                {
                    var forecastData = responseForecast.Data;

                    if (forecastData.Daily == null || forecastData.Daily.Length == 0)
                        throw new Exception("Today doesn't exist?!");

                    var today = forecastData.Daily[0];

                    message = FormatResults(currentData, location, today);
                }
                else 
                    message = FormatResults(currentData, location);

                return (message, null);
            }

            return (null, null);
        }

        private async Task<(string, Discord.Embed)> GetWeatherResultAsync(string input)
        {
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "DiscorIan Discord bot" }
            };

            var uri = new Uri(String.Format(_options.IanOpenWeatherMapEndpointQ,
                HttpUtility.UrlEncode(input),
                _options.IanOpenWeatherKey));

            var response = await _fetchService
                .GetAsync<WeatherCurrent.Current>(uri, headers);

            if (response.IsSuccessful)
            {
                var data = response.Data;
                var locale = String.Format("{0}, {1}", data.City.Name, data.City.Country);
                string latlong = String.Format("{0},{1}", data.City.Coord.Lat, data.City.Coord.Lon);

                await _cache.SetStringAsync(input, latlong,
                    new DistributedCacheEntryOptions { 
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4) });

                await _cache.SetStringAsync(latlong, locale,
                    new DistributedCacheEntryOptions { 
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4) });

                string message = FormatResults(data, locale);

                return (message, null);
            }

            return (null, null);
        }

        private async Task<(string, string)> GeocodeAddressAsync(string location)
        {
            if (String.IsNullOrEmpty(_options.IanMapQuestEndpoint)
                || String.IsNullOrEmpty(_options.IanMapQuestKey))
            {
                throw new Exception("Geocoding is not configured.");
            }

            var uri = new Uri(String.Format(_options.IanMapQuestEndpoint,
                HttpUtility.UrlEncode($"{location}"),
                _options.IanMapQuestKey));

            var response = await _fetchService.GetAsync<MapQuest>(uri);

            if (response.IsSuccessful)
            {
                if (response?.Data?.Results != null)
                {
                    var loc = response?.Data?.Results[0]?.Locations[0];
                    string precision = loc.GeocodeQuality;

                    string geocode = String.Format("{0},{1}", loc.LatLng.Lat, loc.LatLng.Lng);
                    string locale;

                    if (precision == "COUNTRY")
                        locale = loc.AdminArea1;
                    else if (precision == "STATE")
                        locale = String.Format("{0}, {1}", loc.AdminArea3, loc.AdminArea1);
                    else if (precision == "CITY" && String.IsNullOrEmpty(loc.AdminArea3))
                        locale = String.Format("{0}, {1}", loc.AdminArea5, loc.AdminArea1);
                    else if (String.IsNullOrEmpty(loc.AdminArea5))
                        locale = String.Format("{0}, {1}", loc.AdminArea3, loc.AdminArea1);
                    else
                        locale = String.Format("{0}, {1}", loc.AdminArea5, loc.AdminArea3);

                    return (geocode, locale);
                }
            }
            else
            {
                throw new Exception(response.Message);
            }

            return (null, null);
        }

        private string FormatResults(WeatherCurrent.Current data, string location, WeatherForecast.Daily foreData = null)
        {
            var sb = new StringBuilder()
                    .AppendLine(String.Format(">>> **{0}:** {1} {2}",
                        location,
                        TitleCase(data.Weather.Value),
                        WeatherIcon(data.Weather.Icon)))

                    .AppendLine(String.Format("**Temp:** {0}°{1} **Feels Like:** {2}°{3} **Humidity:** {4}{5}",
                        data.Temperature.Value,
                        data.Temperature.Unit.ToUpper()[0],
                        data.Feels_like.Value,
                        data.Feels_like.Unit.ToUpper()[0],
                        data.Humidity.Value,
                        data.Humidity.Unit))

                    .Append(String.Format("**Wind:** {0} **Speed:** {1}{2} {3}",
                        data.Wind.Speed.Name,
                        data.Wind.Speed.Value,
                        data.Wind.Speed.Unit,
                        data.Wind.Direction.Code));

            if (!String.IsNullOrEmpty(data.Wind.Gusts))
            {
                sb.Append(String.Format(" **Gusts:** {0}{1}",
                        data.Wind.Gusts,
                        data.Wind.Speed.Unit));
            }

            if (foreData != null)
            {
                sb.AppendLine()
                    .AppendLine(String.Format("**High:** {0}°{1} **Low:** {2}°{3}",
                        foreData.Temp.Max.ToString(),
                        data.Temperature.Unit.ToUpper()[0],
                        foreData.Temp.Min.ToString(),
                        data.Temperature.Unit.ToUpper()[0]))
                    .Append(String.Format("**Forecast:** {0} {1}",
                        TitleCase(foreData.Weather[0].Description),
                        WeatherIcon(foreData.Weather[0].Icon)));
            }

            return sb.ToString().Trim();
        }

        private string TitleCase(string str)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str);
        }

        private string WeatherIcon(string iconCode)
        {
            string result;

            switch (iconCode)
            {
                case "01d":
                    result = ":sunny:";
                    break;
                case "01n":
                    result = ":first_quarter_moon_with_face:";
                    break;
                case "02d":
                    result = ":white_sun_cloud:";
                    break;
                case "02n":
                case "03d":
                case "03n":
                case "04d":
                case "04n":
                    result = ":cloud:";
                    break;
                case "09d":
                case "09n":
                case "10n":
                    result = ":cloud_rain:";
                    break;
                case "10d":
                    result = ":white_sun_rain_cloud:";
                    break;
                case "11d":
                case "11n":
                    result = ":thunder_cloud_rain:";
                    break;
                case "13d":
                case "13n":
                    result = ":snowflake:";
                    break;
                case "50d":
                case "50n":
                    result = ":sweat_drops:";
                    break;
                default:
                    result = string.Empty;
                    break;
            }
            
            return result;    
        }
    }
}
