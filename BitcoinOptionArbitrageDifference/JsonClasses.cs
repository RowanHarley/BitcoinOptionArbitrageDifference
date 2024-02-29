using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinOptionArbitrageDifference.Classes
{
    public class BinanceExchangeInfo
    {
        public List<BinanceOptionSymbol> optionSymbols { get; set; }
    }
    public class BinanceOptionSymbol
    {
        public long expiryDate { get; set; }
        public string symbol { get; set; }
        public int strikePrice { get; set; }
        public string side { get; set; }
    }
    public class DeribitExchangeInfo
    {
        public List<DeribitOption> result { get; set; }
    }
    public class DeribitPriceInfo
    {
        public List<DeribitOptionPrice> result { get; set; }
    }
    public class DeribitOptionPrice
    {
        public string instrument_name { get; set; }
        public double? bid_price { get; set; }
        public double? ask_price { get; set; }

        public double GetMarginRequirement(double Underlying, double LongOption = 0)
        {
            double Strike = double.Parse(instrument_name.Split('-')[^2]);
            double OtmAmount = instrument_name.Split('-')[^1] == "C" ? Underlying - Strike : Strike - Underlying;
            double? Margin = Math.Max(0.15 - OtmAmount / Underlying, 0.1);  //+ (bid_price + ask_price) / 2; -- Seems like Deribit don't include this
            Margin *= Underlying;
            Margin += LongOption;
            return Margin.GetValueOrDefault(0);
        }
    }
    public class BybitV5Api
    {
        public BybitPriceInfo result { get; set; }
    }
    public class BybitPriceInfo
    {
        public List<BybitOptionPrice> list { get; set; }
    }
    public class BybitOptionPrice
    {
        public string symbol { get; set; }
        public double bid1Price { get; set; }
        public double ask1Price { get; set; }
        public double bid1Size { get; set; }
        public double ask1Size { get; set; }
        public double ask1Iv { get; set; }
        public double underlyingPrice { get; set; }

    }
    public class DeribitOption
    {
        public string instrument_name { get; set; }
        public double strike { get; set; }
        public string option_type { get; set; }
        public long expiration_timestamp { get; set; }
    }
    public class BybitExchangeInfo
    {
        public BybitExchangeResult result { get; set; }
    }
    public class BybitExchangeResult
    {
        public string nextPageCursor { get; set; }
        public List<BybitOption> list { get; set; }
        
    }
    public class BybitOption
    {
        private string _s = "";
        public int Strike { get; private set; }
        public string symbol { get { return _s; } 
            set 
            { 
                _s = value;
                Strike = int.Parse(_s.Split('-')[2]);
            } 
        }
        public string optionsType;
        public long deliveryTime;
    }
}
