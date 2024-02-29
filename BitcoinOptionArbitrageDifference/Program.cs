using BitcoinOptionArbitrageDifference.Classes;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using BitcoinOptionArbitrageDifference.Methods;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Analysis;

namespace BitcoinOptionArbitrageDifference
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static async Task Main(string[] args)
        {
            // What needs to be done:
            // Find 2 spreads of same Maturity that produce best return (Sell Call, Buy Put, and Buy Call, Sell Put)
            // Calculate rolling Average Price of options at Maturity and look to hedge at each timed interval (Potentially improve on average as time tends towards maturity and aim to close
            // at bid/ask price to avoid maker fee.
            // Figure out how to transfer between exchanges in a way that means each contract satisfies margin. and we don't have to worry about falling below maintenance margin
            // Potentially calculate VaR on option portfolio so that we aren't at risk of liquidation
            // Find cost of exercise (Potentially from Monte Carlo Sim) to ensure trade will be profitable.

            double BybitOpTakerFee = 0.0003;
            double DeribitOpTakerFee = 0.0003;
            double BybitPerpTakerFee = 0.0001;
            double PerpetualMaxmimumMargin = 100.0;
            double PerpetualUtilisedMargin = 20.0;
            /*List<DeribitOption> deribitOptions = await Exchanges.GetDeribitOptions();
            List<BybitOption> bybitOptions = await Exchanges.GetBybitOptions();
            
            List<string> bybitOpNames = bybitOptions.Select(x => x.symbol).ToList();
            List<string> deribitOpNames = deribitOptions.Select(x => x.instrument_name).ToList();
            List<string> Instruments = bybitOpNames.Intersect(deribitOpNames).ToList();*/
            MonteCarloSimulation Sim = new(27000, 0.3, 0.05, (double)3 / 365, 200, 1000);
            Sim.RunSimulation();
            MonteCarloSimulation.ValueAtRisk VaR = new(0.05, Sim);
            Console.WriteLine($"Value at Risk is ({VaR.ClosingVaR.Item1},{VaR.ClosingVaR.Item2}) at close or ({VaR.VaR.Item1},{VaR.VaR.Item2}) throughout");

            List<DeribitOptionPrice> dOpPrices = await Exchanges.GetDeribitOptionPrices();
            List<BybitOptionPrice> bOpPrices = await Exchanges.GetBybitOptionPrices();
            Dictionary<string, Dictionary<char, BybitOptionPrice>> BybitPrices = new();
            Dictionary<string, Dictionary<char, DeribitOptionPrice>> DeribitPrices = new();
            List<string> bybitOpNames = bOpPrices.Select(x => x.symbol).ToList();
            List<string> deribitOpNames = dOpPrices.Select(x => x.instrument_name).ToList();
            List<string> Instruments = deribitOpNames.Intersect(bybitOpNames).OrderBy(x => x).ToList();
            dOpPrices = dOpPrices.Where(x => Instruments.Contains(x.instrument_name)).OrderBy(x => x.instrument_name).ToList();
            bOpPrices = bOpPrices.Where(x => Instruments.Contains(x.symbol)).OrderBy(x => x.symbol).ToList();
            for (int i = 0; i < Instruments.Count; i++)
            {
                if (!BybitPrices.ContainsKey(Instruments[i][..^2]))
                {
                    BybitPrices[Instruments[i][..^2]] = new Dictionary<char, BybitOptionPrice>();
                    DeribitPrices[Instruments[i][..^2]] = new Dictionary<char, DeribitOptionPrice>();
                }
                BybitPrices[Instruments[i][..^2]][Instruments[i][^1]] = bOpPrices[i];
                DeribitPrices[Instruments[i][..^2]][Instruments[i][^1]] = dOpPrices[i];
            }
            for (int i = 0; i < Instruments.Count; i += 2)
            {
                var Underlying = BybitPrices[Instruments[i][..^2]]['P'].underlyingPrice;
                var BCallPrice = BybitPrices[Instruments[i][..^2]]['C'].ask1Price;
                var BPutPrice = BybitPrices[Instruments[i][..^2]]['P'].ask1Price;
                var DCallPrice = DeribitPrices[Instruments[i][..^2]]['C'].bid_price * Underlying;
                var DPutPrice = DeribitPrices[Instruments[i][..^2]]['P'].bid_price * Underlying;
                var Strike = double.Parse(Instruments[i].Split('-')[^2]);
                var BybitFee = Underlying * BybitOpTakerFee;

                var NetProfitL = - BPutPrice - Underlying + Strike + DCallPrice - 2 * (BybitFee > 0.125 * BPutPrice ? 0.125 * BPutPrice : BybitFee) - 0.0003 * Underlying/* - 0.0008 * Underlying*/;
                var NetProfitS = DPutPrice + Underlying - Strike - BCallPrice - 2 * (BybitFee > 0.125 * BCallPrice ? 0.125 * BCallPrice : BybitFee) - 0.0003 * Underlying/* - 0.0008 * Underlying*/;
                var dtStr = dOpPrices[i].instrument_name.Split('-')[1];
                if (dtStr.Length < 7)
                    dtStr = dtStr.Insert(0, "0");
                var expDate = DateTimeOffset.ParseExact(dtStr, "ddMMMyy", null, System.Globalization.DateTimeStyles.AssumeUniversal);
                expDate.Add(new TimeSpan(8,0,0));

                if (NetProfitL.HasValue && NetProfitS.HasValue)
                {
                    if (NetProfitL > 0 && BPutPrice > 0)
                    {
                        Console.WriteLine($"Selling Call and Buying Put and Underlying nets ${NetProfitL:0.##} profit on {Instruments[i][..^2]}");
                        double Margin = await Exchanges.GetDeribitMarginRequirements(Instruments[i][..^2] + "-C", 1, DCallPrice.GetValueOrDefault()/Underlying); // DeribitPrices[Instruments[i][..^2]]['C'].GetMarginRequirement(Underlying, BPutPrice);
                        Margin *= Underlying;
                        Margin += BPutPrice;
                        Console.WriteLine($"Margin Requirements would be approximately ${Margin}");
                        var YTE = (expDate.ToUnixTimeSeconds() - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) / (double)(60 * 60 * 24 * 365);
                        var pnlP = (NetProfitL / Margin + 1).GetValueOrDefault();
                        Console.WriteLine($"Annualised Return on Margin would be {(Math.Pow(pnlP, 1 / YTE) - 1) * 100:0.##}% or {(pnlP - 1)*100:0.##}% across the term");
                    }
                    if (NetProfitS > 0 && BCallPrice > 0)
                    {
                        Console.WriteLine($"Buying Call and Selling Put and Underlying nets ${NetProfitS:0.##} profit on {Instruments[i][..^2]}");
                        double Margin = await Exchanges.GetDeribitMarginRequirements(Instruments[i][..^2] + "-P", 1, DPutPrice.GetValueOrDefault() / Underlying); // DeribitPrices[Instruments[i][..^2]]['P'].GetMarginRequirement(Underlying, BCallPrice);
                        Margin *= Underlying;
                        Margin += BCallPrice;
                        Console.WriteLine($"Margin Requirements would be ${Margin}");
                        var YTE = (expDate.ToUnixTimeSeconds() - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) / (double)(60 * 60 * 24 * 365);
                        var pnlP = (NetProfitS / Margin + 1).GetValueOrDefault();
                        Console.WriteLine($"Annualised Return on Margin would be {(Math.Pow(pnlP, 1 / YTE) - 1) * 100:0.##}% or {(pnlP - 1) * 100:0.##}% across the term");
                    }
                }
                else if (NetProfitL.HasValue && NetProfitL > 0 && BPutPrice > 0)
                {
                    Console.WriteLine($"Selling Call and Buying Put and Underlying nets ${NetProfitL:0.##} profit on {Instruments[i][..^2]}");
                    double Margin = await Exchanges.GetDeribitMarginRequirements(Instruments[i][..^2] + "-C", 1, DCallPrice.GetValueOrDefault() / Underlying); // DeribitPrices[Instruments[i][..^2]]['C'].GetMarginRequirement(Underlying, BPutPrice);
                    Margin *= Underlying;
                    Margin += BPutPrice;
                    Console.WriteLine($"Margin Requirements would be approximately ${Margin}");
                    var YTE = (expDate.ToUnixTimeSeconds() - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) / (double)(60 * 60 * 24 * 365);
                    var pnlP = (NetProfitL / Margin + 1).GetValueOrDefault();
                    Console.WriteLine($"Annualised Return on Margin would be {(Math.Pow((NetProfitL / Margin + 1).GetValueOrDefault(), 1 / YTE) - 1) * 100:0.##}% or {(pnlP - 1) * 100:0.##}% across the term");
                }
                else if (NetProfitS.HasValue && NetProfitS > 0 && BCallPrice > 0)
                {
                    Console.WriteLine($"Buying Call and Selling Put and Underlying nets ${NetProfitS:0.##} profit on {Instruments[i][..^2]}");
                    double Margin = await Exchanges.GetDeribitMarginRequirements(Instruments[i][..^2] + "-P", 1, DPutPrice.GetValueOrDefault() / Underlying); // DeribitPrices[Instruments[i][..^2]]['P'].GetMarginRequirement(Underlying, BCallPrice);
                    Margin *= Underlying;
                    Margin += BCallPrice;
                    Console.WriteLine($"Margin Requirements would be approximately ${Margin}");
                    var YTE = (expDate.ToUnixTimeSeconds() - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) / (double)(60 * 60 * 24 * 365);
                    var pnlP = (NetProfitS / Margin + 1).GetValueOrDefault();
                    Console.WriteLine($"Annualised Return on Margin would be {(Math.Pow((NetProfitS / Margin + 1).GetValueOrDefault(), 1 / YTE) - 1) * 100:0.##}% or {(pnlP - 1) * 100:0.##}% across the term");
                }
            }

            Console.WriteLine("\n\n\nChecking arbitrage on same option:");
            for(int i = 0; i < bOpPrices.Count; i++)
            {
                if (dOpPrices[i].bid_price == null || dOpPrices[i].ask_price == null)
                    continue;
                double Underlying = bOpPrices[i].underlyingPrice;
                double DeribitFee = (0.0003 / dOpPrices[i].bid_price.GetValueOrDefault() > 0.125 ? 0.125 * dOpPrices[i].bid_price.GetValueOrDefault() : 0.0003);
                double BybitFee = (BybitOpTakerFee * Underlying/ bOpPrices[i].ask1Price > 0.125 ? 0.125 * bOpPrices[i].ask1Price : BybitOpTakerFee * Underlying);
                double Diff = (dOpPrices[i].bid_price.GetValueOrDefault() - DeribitFee) * bOpPrices[i].underlyingPrice - bOpPrices[i].ask1Price - BybitFee;
                // Testing has found this to have never happened
                //double? oSDiff =  bOpPrices[i].bid * (1 + BybitOpTakerFee) - dOpPrices[i].ask_price * bOpPrices[i].underlyingPrice * (1 - DeribitOpTakerFee) - BybitPerpTakerFee * dOpPrices[i].ask_price * bOpPrices[i].underlyingPrice - (bOpPrices[i].underlyingPrice * (DeribitOpTakerFee + BybitOpTakerFee));
                var dtStr = dOpPrices[i].instrument_name.Split('-')[1];
                if (dtStr.Length < 7)
                    dtStr = dtStr.Insert(0, "0");
                var expDate = DateTimeOffset.ParseExact(dtStr, "ddMMMyy", null, System.Globalization.DateTimeStyles.None);
                double Strike = double.Parse(dOpPrices[i].instrument_name.Split('-')[2]);
                var YTE = (expDate.ToUnixTimeSeconds() - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) / (double)(60 * 60 * 24 * 365);
                double MoneynessProb = Calculations.MoneynessProbability(bOpPrices[i].ask1Iv, YTE, Strike, Underlying);
                if(double.IsNaN(MoneynessProb))
                    MoneynessProb = 0;
                Diff -= MoneynessProb * (0.0003 * Underlying + BybitPerpTakerFee * bOpPrices[i].ask1Price);
                if (Diff > 0 & bOpPrices[i].ask1Size > 0)
                {
                    Console.WriteLine($"Arbitrage of ${Diff:0.##} present per Option sold on Contract {bOpPrices[i].symbol}");
                    
                    double OtmAmount = dOpPrices[i].instrument_name.Split('-')[^1] == "C" ? bOpPrices[i].underlyingPrice - Strike : Strike - bOpPrices[i].underlyingPrice;
                    double Margin = await Exchanges.GetDeribitMarginRequirements(dOpPrices[i].instrument_name, 1, dOpPrices[i].bid_price.GetValueOrDefault());
                    Margin *= bOpPrices[i].underlyingPrice;
                    Margin += bOpPrices[i].ask1Price;
                    Console.WriteLine($"Margin Requirements would be approximately {Margin}");
                    Console.WriteLine($"Annualised Return on Margin would be {(Math.Pow(Diff/Margin + 1,1/YTE) - 1) * 100:0.##}%");
                }
                /*
                else if(oSDiff > 0 & bOpPrices[i].bidSize > 0)
                {
                    Console.WriteLine($"Arbitrage of ${Diff:0.##} present per Option sold on Contract {bOpPrices[i].symbol} through selling on Bybit");
                }
                */
            }

            Console.WriteLine("Done");

        }
    }
}