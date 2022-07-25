using System;

namespace DiscordIan.Model.Weather
{
    public class CommonWeatherModel
    {
        public string Region { get; set; }
        public CommonWeatherModel_Current Current { get; set; }
        public CommonWeatherModel_Wind Wind { get; set; }
        public CommonWeatherModel_Forecast Forecast { get; set; }
        public string Footer { get; set; }
    }

    public class CommonWeatherModel_Current
    {
        public string Condition { get; set; }
        public string IconUrl { get; set; }
        public string Temp { get; set; }
        public string FeelsLike { get; set; }
        public string Humidity { get; set; }
    }

    public class CommonWeatherModel_Wind
    {
        public string Speed { get; set; }
        public string Direction { get; set; }
    }

    public class CommonWeatherModel_Forecast
    {
        public string Condition { get; set; }
        public string TempHigh { get; set; }
        public string TempLow { get; set; }
        public string PrecipChance { get; set; }
    }
}
