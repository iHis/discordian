using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using DiscordIan.Helper;
using DiscordIan.Key;
using DiscordIan.Model.MapQuest;
using DiscordIan.Model.Weather;
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

        [Command("wold", RunMode = RunMode.Async)]
        [Summary("Look up current weather for a provided address.")]
        [Alias("w2")]
        public async Task OpenWeatherMapCurrentAsync([Remainder]
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

            var coords = await _cache.GetStringAsync(string.Format(Cache.Weather, location));
            var locale = string.Empty;

            if (!string.IsNullOrEmpty(coords))
            {
                locale = await _cache.GetStringAsync(string.Format(Cache.Weather, coords));
            }

            if (string.IsNullOrEmpty(coords) || string.IsNullOrEmpty(locale))
            {
                try
                {
                    (coords, locale) = await GeocodeAddressAsync(location);

                    if (!string.IsNullOrEmpty(coords) && !string.IsNullOrEmpty(locale))
                    {
                        await _cache.SetStringAsync(string.Format(Cache.Weather, location), coords);

                        await _cache.SetStringAsync(string.Format(Cache.Weather, coords), locale);
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
                message = await GetOpenWeatherMapResultAsync(location);
            }
            else
            {
                message = await GetOpenWeatherMapResultAsync(coords, locale);
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

        [Command("w", RunMode = RunMode.Async)]
        [Summary("Look up current weather for a provided address.")]
        [Alias("weather", "f", "forecast")]
        public async Task WeatherApiCurrentAsync([Remainder]
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

            var embed = await GetWeatherApiResultsAsync(location);

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

        [Command("w3", RunMode = RunMode.Async)]
        [Summary("Look up current weather for a provided address.")]
        public async Task MeteoSourceCurrentAsync([Remainder]
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

            var coords = await _cache.GetStringAsync(string.Format(Cache.Weather, location));
            var locale = string.Empty;

            if (!string.IsNullOrEmpty(coords))
            {
                locale = await _cache.GetStringAsync(string.Format(Cache.Weather, coords));
            }

            if (string.IsNullOrEmpty(coords) || string.IsNullOrEmpty(locale))
            {
                try
                {
                    (coords, locale) = await GeocodeAddressAsync(location);

                    if (!string.IsNullOrEmpty(coords) && !string.IsNullOrEmpty(locale))
                    {
                        await _cache.SetStringAsync(string.Format(Cache.Weather, location), coords);

                        await _cache.SetStringAsync(string.Format(Cache.Weather, coords), locale);
                    }
                }
                catch (Exception)
                {
                    //Geocode failed, revert to OpenWeatherMap's built-in resolve.
                }
            }

            if (string.IsNullOrEmpty(coords))
            {
                await ReplyAsync("Location not found.");
            }
            else
            {
                var (embed, filePath) = await GetMeteoSourceResultAsync(coords, locale ?? location);

                if (embed == null)
                {
                    await ReplyAsync("No weather found, sorry!");
                }
                else
                {
                    await Context.Channel.SendFileAsync(filePath, null, false, embed);
                }
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
            string location;

            if (string.IsNullOrEmpty(user))
            {
                location = SqliteHelper.SelectWeatherDefault(Context.User.Id.ToString());
            }
            else
            {
                location = SqliteHelper.SelectWeatherDefaultByName(user);
            }

            if (string.IsNullOrEmpty(location))
            {
                await ReplyAsync("No default location found.");
                return;
            }

            await ReplyAsync($"{user ?? Context.User.Username}: {location}");
            return;
        }

        private async Task<string> GetOpenWeatherMapResultAsync(string coordinates, string location)
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

        private async Task<string> GetOpenWeatherMapResultAsync(string input)
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

                await _cache.SetStringAsync(string.Format(Cache.Weather, input),
                    latlong,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4)
                    });

                await _cache.SetStringAsync(string.Format(Cache.Weather, latlong),
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

        private async Task<Embed> GetWeatherApiResultsAsync(string input)
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
                return Context.Message.Content.StartsWith("!f")
                    ? FormatDayForecast(response.Data)
                    : FormatResultsCommon(CommonWeatherDTOMap(response.Data));
            }

            return null;
        }

        private async Task<(Embed, string)> GetMeteoSourceResultAsync(string coords, string locale)
        {
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "DiscorIan Discord bot" }
            };

            var uri = new Uri(string.Format(_options.MeteoSourceEndpoint,
                coords.Split(',')[0],
                coords.Split(',')[1],
                _options.MeteoSourceKey));

            var response = await _fetchService
                .GetAsync<MeteoSourceModel>(uri, headers);
            apiTiming += response.Elapsed;

            if (response.IsSuccessful)
            {
                response.Data.Locale = locale;

                var embed = FormatResultsCommon(CommonWeatherDTOMap(response.Data));

                return (embed, $"Model/Weather/Icons/MeteoSource/set03/medium/{response.Data.Current.IconNum}.png");
            }

            return (null, null);
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

        private CommonWeatherModel CommonWeatherDTOMap<T>(T weather)
        {
            var matchType = typeof(T);
            if (matchType == typeof(WeatherApiModel))
            {
                var data = weather as WeatherApiModel;
                var response = new CommonWeatherModel
                {
                    Region = (data.Location.Country == "United States of America" || data.Location.Country == "USA")
                        ? $"{data.Location.Name}, {data.Location.Region}"
                        : $"{data.Location.Name}, {data.Location.Country}",
                    Current = new CommonWeatherModel_Current
                    {
                        Condition = data.Current.Condition.Text.ToTitleCase(),
                        IconUrl = new Uri($"http:{data.Current.Condition.Icon}").ValidateUri(),
                        Temp = $"{data.Current.TempF}F / {data.Current.TempC}C",
                        FeelsLike = $"{data.Current.FeelslikeF}F / {data.Current.FeelslikeC}C",
                        Humidity = $"{data.Current.Humidity}%"
                    },
                    Wind = new CommonWeatherModel_Wind
                    {
                        Speed = $"{data.Current.WindMph}mph / {data.Current.WindKph}kph",
                        Direction = data.Current.WindDir
                    },
                    Forecast = new CommonWeatherModel_Forecast
                    {
                        Condition = data.Forecast.Forecastday[0].Day.Condition.Text.ToTitleCase(),
                        TempHigh = $"{data.Forecast.Forecastday[0].Day.MaxtempF}F / {data.Forecast.Forecastday[0].Day.MaxtempC}C",
                        TempLow = $"{data.Forecast.Forecastday[0].Day.MintempF}F / {data.Forecast.Forecastday[0].Day.MintempC}C",
                        PrecipChance = $"Rain {data.Forecast.Forecastday[0].Day.DailyChanceOfRain}%, Snow {data.Forecast.Forecastday[0].Day.DailyChanceOfSnow}%"
                    },
                    Footer = $"Last Updated: {data.Current.LastUpdated}"
                };

                return response;
            }

            if (matchType == typeof(MeteoSourceModel))
            {
                var data = weather as MeteoSourceModel;
                var tempUnit = data.Units == "us" ? "F" : "C";
                var speedUnit = data.Units == "us" ? "mph" : "kph";
                var response = new CommonWeatherModel
                {
                    Region = data.Locale,
                    Current = new CommonWeatherModel_Current
                    {
                        Condition = data.Current.Summary.ToTitleCase(),
                        IconUrl = $"attachment://{data.Current.IconNum}.png",
                        Temp = $"{data.Current.Temperature}{tempUnit}"
                    },
                    Wind = new CommonWeatherModel_Wind
                    {
                        Speed = $"{data.Current.Wind.Speed}{speedUnit}",
                        Direction = data.Current.Wind.Dir
                    },
                    Forecast = new CommonWeatherModel_Forecast
                    {
                        Condition = data.Daily.Data[0].Summary.ToTitleCase().Split("Temperature")[0],
                        TempHigh = $"{data.Daily.Data[0].AllDay.TemperatureMax}{tempUnit}",
                        TempLow = $"{data.Daily.Data[0].AllDay.TemperatureMin}{tempUnit}",
                        PrecipChance = $"{data.Daily.Data[0].AllDay.Precipitation.Type.ToTitleCase()}" +
                            $"{(data.Daily.Data[0].AllDay.Precipitation.Type != "none" ? $" {100 * data.Daily.Data[0].AllDay.Precipitation.Total}%" : "")}"
                    }
                };

                return response;
            }

            return null;
        }

        private Embed FormatResultsCommon(CommonWeatherModel data)
        {
            var builder = new EmbedBuilder()
            {
                Color = Color.Blue,
                Title = data.Region,
                ThumbnailUrl = data.Current.IconUrl,
                Fields = new List<EmbedFieldBuilder>() {
                    EmbedHelper.MakeField($"Condition: **{data.Current.Condition}**",
                        $"\u200B    **Temp:** {data.Current.Temp}"
                        + (!string.IsNullOrEmpty(data.Current.FeelsLike) ? $"\n\u200B    **Feels Like:** {data.Current.FeelsLike}" : "")
                        + (!string.IsNullOrEmpty(data.Current.Humidity) ? $"\n\u200B    **Humidity:** {data.Current.Humidity}" : "")),
                    EmbedHelper.MakeField("Wind:",
                        $"\u200B    **Speed:** {data.Wind.Speed}"
                        + $"\n\u200B    **Direction:** {data.Wind.Direction}"),
                    EmbedHelper.MakeField($"Forecast: **{data.Forecast.Condition}**",
                        $"\u200B    **High:** {data.Forecast.TempHigh}"
                        + $"\n\u200B    **Low:** {data.Forecast.TempLow}"
                        + $"\n\u200B    **Precip. Chance:** {data.Forecast.PrecipChance}")
                }
            };

            if (!string.IsNullOrEmpty(data.Footer))
            {
                builder.Footer = new EmbedFooterBuilder() { Text = data.Footer };
            }

            return builder.Build();
        }

        private Embed FormatDayForecast(WeatherApiModel data)
        {
            return new EmbedBuilder()
            {
                Color = Color.Purple,
                Title = (data.Location.Country == "United States of America" || data.Location.Country == "USA")
                    ? $"{data.Location.Name}, {data.Location.Region}"
                    : $"{data.Location.Name}, {data.Location.Country}",
                Fields = new List<EmbedFieldBuilder>() {
                    EmbedHelper.MakeField($"**{DateTime.Parse(data.Forecast.Forecastday[0].Date).DayOfWeek}\n{data.Forecast.Forecastday[0].Date}**",
                        $"**{data.Forecast.Forecastday[0].Day.Condition.Text.ToTitleCase()}**"
                        + $"\n**High:** {data.Forecast.Forecastday[0].Day.MaxtempF}F / {data.Forecast.Forecastday[0].Day.MaxtempC}C"
                        + $"\n**Low:** {data.Forecast.Forecastday[0].Day.MintempF}F / {data.Forecast.Forecastday[0].Day.MintempC}C"
                        + $"\n**Chance of Rain:** {data.Forecast.Forecastday[0].Day.DailyChanceOfRain}%"
                        + (data.Forecast.Forecastday[0].Day.DailyChanceOfSnow > 0
                            ? $"\n**Chance of Snow:** {data.Forecast.Forecastday[0].Day.DailyChanceOfSnow}%"
                            : ""),
                        true),
                    EmbedHelper.MakeField($"**{DateTime.Parse(data.Forecast.Forecastday[1].Date).DayOfWeek}\n{data.Forecast.Forecastday[1].Date}**",
                        $"**{data.Forecast.Forecastday[1].Day.Condition.Text.ToTitleCase()}**"
                        + $"\n**High:** {data.Forecast.Forecastday[1].Day.MaxtempF}F / {data.Forecast.Forecastday[1].Day.MaxtempC}C"
                        + $"\n**Low:** {data.Forecast.Forecastday[1].Day.MintempF}F / {data.Forecast.Forecastday[1].Day.MintempC}C"
                        + $"\n**Chance of Rain:** {data.Forecast.Forecastday[1].Day.DailyChanceOfRain}%"
                        + (data.Forecast.Forecastday[1].Day.DailyChanceOfSnow > 0
                            ? $"\n**Chance of Snow:** {data.Forecast.Forecastday[1].Day.DailyChanceOfSnow}%"
                            : ""),
                        true),
                    EmbedHelper.MakeField($"**{DateTime.Parse(data.Forecast.Forecastday[2].Date).DayOfWeek}\n{data.Forecast.Forecastday[2].Date}**",
                        $"**{data.Forecast.Forecastday[2].Day.Condition.Text.ToTitleCase()}**"
                        + $"\n**High:** {data.Forecast.Forecastday[2].Day.MaxtempF}F / {data.Forecast.Forecastday[2].Day.MaxtempC}C"
                        + $"\n**Low:** {data.Forecast.Forecastday[2].Day.MintempF}F / {data.Forecast.Forecastday[2].Day.MintempC}C"
                        + $"\n**Chance of Rain:** {data.Forecast.Forecastday[2].Day.DailyChanceOfRain}%"
                        + (data.Forecast.Forecastday[2].Day.DailyChanceOfSnow > 0
                            ? $"\n**Chance of Snow:** {data.Forecast.Forecastday[2].Day.DailyChanceOfSnow}%"
                            : ""),
                        true)
                },
                Footer = new EmbedFooterBuilder() { Text = $"Last Updated: {data.Current.LastUpdated}" }
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
