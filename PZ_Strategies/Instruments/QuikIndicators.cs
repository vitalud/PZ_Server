using System.Text.Json.Serialization;

namespace Strategies.Instruments
{
    /// <summary>
    /// Данные индикаторов интсрументов, приходящих со стороны Quik.
    /// </summary>
    public class QuikIndicators
    {
        [JsonPropertyName("sec_code")]
        public string SecCode { get; set; } = string.Empty;

        [JsonPropertyName("class_code")]
        public string ClassCode { get; set; } = string.Empty;

        [JsonPropertyName("interval")]
        public string Interval { get; set; } = string.Empty;

        [JsonPropertyName("time")]
        public int Time { get; set; }

        [JsonPropertyName("day")]
        public int Day { get; set; }


        [JsonPropertyName("open")]
        public decimal Open { get; set; }

        [JsonPropertyName("high")]
        public decimal High { get; set; }

        [JsonPropertyName("low")]
        public decimal Low { get; set; }

        [JsonPropertyName("close")]
        public decimal Close { get; set; }


        [JsonPropertyName("bid_20")]
        public decimal AllBids { get; set; }

        [JsonPropertyName("qty_bid")]
        public decimal BestBids { get; set; }

        [JsonPropertyName("sum_bid")]
        public decimal NumBids { get; set; }

        [JsonPropertyName("offer_20")]
        public decimal AllAsks { get; set; }

        [JsonPropertyName("qty_offer")]
        public decimal BestAsks { get; set; }

        [JsonPropertyName("sum_offer")]
        public decimal NumAsks { get; set; }


        [JsonPropertyName("trade_buy")]
        public decimal TradesBuy { get; set; }

        [JsonPropertyName("trade_sell")]
        public decimal TradesSell { get; set; }


        [JsonPropertyName("volume")]
        public decimal Volume { get; set; }
    }
}
