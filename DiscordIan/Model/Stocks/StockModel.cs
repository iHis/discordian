using System;
using System.Text.Json.Serialization;

namespace DiscordIan.Model.Stocks
{
    public class StockModel
    {
        public string Symbol { get; set; }
        public StockCompany Company { get; set; }
        public StockQuote StockQuote { get; set; }
    }

    public class StockQuote
    {
        [JsonPropertyName("c")]
        public double Current { get; set; }

        [JsonPropertyName("h")]
        public double High { get; set; }

        [JsonPropertyName("l")]
        public double Low { get; set; }

        [JsonPropertyName("o")]
        public double Open { get; set; }

        [JsonPropertyName("pc")]
        public double PreviousClose { get; set; }

        [JsonPropertyName("t")]
        public double Timestamp { get; set; }
    }

    public class StockCompany
    {
        public string Country { get; set; }
        public string Currency { get; set; }
        public string Exchange { get; set; }
        public string FinnhubIndustry { get; set; }
        public string Ipo { get; set; }
        public Uri Logo { get; set; }
        public double MarketCapitalization { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public double ShareOutstanding { get; set; }
        public string Ticker { get; set; }
        public Uri Weburl { get; set; }
    }
}
