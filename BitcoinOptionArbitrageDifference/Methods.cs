using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitcoinOptionArbitrageDifference.Classes;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace BitcoinOptionArbitrageDifference.Methods
{
    public class Exchanges
    {
        public async static Task<List<BinanceOptionSymbol>> GetBinanceOptions()
        {
            HttpClient client = new();
            // Send a GET request to the specified URL
            HttpResponseMessage response = await client.GetAsync("https://eapi.binance.com/eapi/v1/exchangeInfo");

            // Print the response content
            string responseBody = await response.Content.ReadAsStringAsync();

            BinanceExchangeInfo info = JsonConvert.DeserializeObject<BinanceExchangeInfo>(responseBody);
            return info.optionSymbols;
        }
        public async static Task<List<BybitOption>> GetBybitOptions()
        {
            HttpClient client = new();
            // Send a GET request to the specified URL
            HttpResponseMessage response = await client.GetAsync("https://api.bybit.com/v5/market/instruments-info?category=option&baseCoin=BTC&limit=1000");

            // Print the response content
            string responseBody = await response.Content.ReadAsStringAsync();

            BybitExchangeInfo info = JsonConvert.DeserializeObject<BybitExchangeInfo>(responseBody);
            List<BybitOption> ops = info.result.list;
            if (info.result.nextPageCursor != "")
            {
                while (info.result.nextPageCursor != "")
                {
                    response = await client.GetAsync($"https://api.bybit.com/v5/market/instruments-info?category=option&baseCoin=BTC&limit=1000&cursor={info.result.nextPageCursor}");

                    // Print the response content
                    responseBody = await response.Content.ReadAsStringAsync();
                    info = JsonConvert.DeserializeObject<BybitExchangeInfo>(responseBody);
                    if (info.result.list.Count == 0)
                        break;
                    ops.AddRange(info.result.list);
                }
            }
            return ops;
        }
        public async static Task<List<DeribitOption>> GetDeribitOptions()
        {
            HttpClient client = new();
            // Send a GET request to the specified URL
            HttpResponseMessage response = await client.GetAsync("https://www.deribit.com/api/v2/public/get_instruments?currency=BTC&kind=option");

            // Print the response content
            string responseBody = await response.Content.ReadAsStringAsync();

            DeribitExchangeInfo info = JsonConvert.DeserializeObject<DeribitExchangeInfo>(responseBody);
            return info.result;
        }
        public async static Task<List<BybitOptionPrice>> GetBybitOptionPrices(string coin = "BTC")
        {
            HttpClient client = new();
            // Send a GET request to the specified URL
            HttpResponseMessage response = await client.GetAsync($"https://api.bybit.com/v5/market/tickers?category=option&baseCoin={coin}");

            // Print the response content
            string responseBody = await response.Content.ReadAsStringAsync();

            BybitPriceInfo info = JsonConvert.DeserializeObject<BybitV5Api>(responseBody).result;
            return info.list;
        }
        public async static Task<List<DeribitOptionPrice>> GetDeribitOptionPrices(string coin = "BTC")
        {
            HttpClient client = new();
            // Send a GET request to the specified URL
            HttpResponseMessage response = await client.GetAsync($"https://www.deribit.com/api/v2/public/get_book_summary_by_currency?currency={coin}");

            // Print the response content
            string responseBody = await response.Content.ReadAsStringAsync();

            DeribitPriceInfo info = JsonConvert.DeserializeObject<DeribitPriceInfo>(responseBody);
            return info.result;
        }
        public async static Task<string> AuthenticateDeribit()
        {
            string clientId = APIDetails.DeribitKey;
            string clientSecret = APIDetails.DeribitSecret;
            HttpClient client = new();
            // Send a GET request to the specified URL
            HttpResponseMessage response = await client.GetAsync($"https://www.deribit.com/api/v2/public/auth?client_id={clientId}&client_secret={clientSecret}&grant_type=client_credentials");
            JToken res = JsonConvert.DeserializeObject<JToken>(await response.Content.ReadAsStringAsync());
            return res["result"]["access_token"].ToString();

        }
        public async static Task<double> GetDeribitMarginRequirements(string instrumentName, double amount, double price)
        {
            string token = await AuthenticateDeribit();
            HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            // Send a GET request to the specified URL
            HttpResponseMessage response = await client.GetAsync($"https://www.deribit.com/api/v2/private/get_margins?amount={amount}&instrument_name={instrumentName}&price={price}");
            JToken res = JsonConvert.DeserializeObject<JToken>(await response.Content.ReadAsStringAsync());
            return (double)res["result"]["sell"];
        }
    }
    public class Calculations
    {
        public static double Phi(double x)
        {
            // constants
            double a1 = 0.254829592;
            double a2 = -0.284496736;
            double a3 = 1.421413741;
            double a4 = -1.453152027;
            double a5 = 1.061405429;
            double p = 0.3275911;

            // Save the sign of x
            int sign = 1;
            if (x < 0)
                sign = -1;
            x = Math.Abs(x) / Math.Sqrt(2.0);

            // A&S formula 7.1.26
            double t = 1.0 / (1.0 + p * x);
            double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

            return 0.5 * (1.0 + sign * y);
        }
        public static double MoneynessProbability(double IV, double T, double K, double Underlying, double r = 0.05)
        {
            double F = Underlying * Math.Exp(r * T);
            double dMinus = (Math.Log(F / K) - (Math.Pow(IV, 2) / 2) * T) / (IV * Math.Sqrt(T));
            return 1 - Phi(dMinus);
        }
    }
}
