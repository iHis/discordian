using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace DiscordIan.Model.Weather
{
    public class MeteoSourceModel
    {
        [JsonProperty("lat")]
        public string Lat { get; set; }

        [JsonProperty("lon")]
        public string Lon { get; set; }

        [JsonProperty("elevation")]
        public double Elevation { get; set; }

        [JsonProperty("timezone")]
        public string Timezone { get; set; }

        [JsonProperty("units")]
        public string Units { get; set; }

        [JsonProperty("current")]
        public CurrentWeather Current { get; set; }

        [JsonProperty("hourly")]
        public Hourly Hourly { get; set; }

        [JsonProperty("daily")]
        public Daily Daily { get; set; }

        public string Locale { get; set; }
    }

    public class AllDay
    {
        [JsonProperty("weather")]
        public string Weather { get; set; }

        [JsonProperty("icon")]
        public int Icon { get; set; }

        [JsonProperty("temperature")]
        public double Temperature { get; set; }

        [JsonProperty("temperature_min")]
        public double TemperatureMin { get; set; }

        [JsonProperty("temperature_max")]
        public double TemperatureMax { get; set; }

        [JsonProperty("wind")]
        public Wind Wind { get; set; }

        [JsonProperty("cloud_cover")]
        public CloudCover CloudCover { get; set; }

        [JsonProperty("precipitation")]
        public Precipitation Precipitation { get; set; }
    }

    public class CloudCover
    {
        [JsonProperty("total")]
        public double Total { get; set; }
    }

    public class CurrentWeather
    {
        [JsonProperty("icon")]
        public string Icon { get; set; }

        [JsonProperty("icon_num")]
        public int IconNum { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("temperature")]
        public double Temperature { get; set; }

        [JsonProperty("wind")]
        public Wind Wind { get; set; }

        [JsonProperty("precipitation")]
        public Precipitation Precipitation { get; set; }

        [JsonProperty("cloud_cover")]
        public double CloudCover { get; set; }
    }

    public class Daily
    {
        [JsonProperty("data")]
        public List<Datum> Data { get; set; }
    }

    public class Datum
    {
        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("weather")]
        public string Weather { get; set; }

        [JsonProperty("icon")]
        public int Icon { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("temperature")]
        public double Temperature { get; set; }

        [JsonProperty("wind")]
        public Wind Wind { get; set; }

        [JsonProperty("cloud_cover")]
        public CloudCover CloudCover { get; set; }

        [JsonProperty("precipitation")]
        public Precipitation Precipitation { get; set; }

        [JsonProperty("day")]
        public string Day { get; set; }

        [JsonProperty("all_day")]
        public AllDay AllDay { get; set; }

        [JsonProperty("morning")]
        public object Morning { get; set; }

        [JsonProperty("afternoon")]
        public object Afternoon { get; set; }

        [JsonProperty("evening")]
        public object Evening { get; set; }
    }

    public class Hourly
    {
        [JsonProperty("data")]
        public List<Datum> Data { get; set; }
    }

    public class Precipitation
    {
        [JsonProperty("total")]
        public double Total { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public class Wind
    {
        [JsonProperty("speed")]
        public double Speed { get; set; }

        [JsonProperty("angle")]
        public double Angle { get; set; }

        [JsonProperty("dir")]
        public string Dir { get; set; }
    }
}
