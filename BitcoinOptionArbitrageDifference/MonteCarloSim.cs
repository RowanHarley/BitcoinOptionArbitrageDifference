using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinOptionArbitrageDifference
{
    public class MonteCarloSimulation
    {
        private double S;
        double sigma;
        double r;
        double T;
        int n;
        int nr;
        List<List<double>> Simulations = new();

        public MonteCarloSimulation(double S, double sigma, double r, double T, int n, int nr)
        {
            this.S = S;
            this.sigma = sigma;
            this.r = r;
            this.T = T;
            this.n = n;
            this.nr = nr;
        }
        public List<List<double>> RunSimulation()
        {
            List<List<double>> Sims = new();
            double nu = r - 0.5 * Math.Pow(sigma, 2);
            double dt = T / n;

            for (int i = 0; i < nr; i += 2)
            {
                Sims.AddRange(new[] { new List<double>(), new List<double>()});
                Sims[i].Add(S);
                Sims[i + 1].Add(S);
                for (int j = 0; j < n; j++)
                {
                    Random rand = new(); //reuse this if you are generating many
                    double u1 = 1.0 - rand.NextDouble(); //uniform(0,1] random doubles
                    double u2 = 1.0 - rand.NextDouble();
                    double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                    double Sn = Sims[i][^1] * Math.Exp(nu*dt + sigma * Math.Sqrt(dt) * randStdNormal);
                    Sims[i].Add(Sn);
                    Sn = Sims[i][^1] * Math.Exp(nu*dt - sigma * Math.Sqrt(dt) * randStdNormal);
                    Sims[i + 1].Add(Sn);
                }
            }
            Simulations = Sims;
            return Sims;
        }
        public class ValueAtRisk
        {
            public (double, double) ClosingVaR;
            public (double, double) VaR;

            public ValueAtRisk(double Confidence, MonteCarloSimulation Simulation)
            {
                List<List<double>> Simulations = Simulation.Simulations;
                if (Simulations.Count == 0)
                    throw new Exception("No simulation was carried out beforehand");

                List<double> ValueAtClose = new();
                List<double> ValueThrough = new();
                for (int i = 0; i < Simulations.Count; i++)
                {
                    List<double> Simi = Simulations[i];
                    ValueAtClose.Add(Simi[^1]);
                    ValueThrough.Add(Simi.Max());
                    ValueThrough.Add(Simi.Min());
                }
                ValueAtClose.Sort();
                ValueThrough.Sort();
                ClosingVaR = (ValueAtClose[(int)Math.Round(Confidence * ValueAtClose.Count)], ValueAtClose[(int)Math.Round((1 - Confidence) * ValueAtClose.Count)]);
                VaR = (ValueThrough[(int)Math.Round(Confidence * ValueThrough.Count)], ValueThrough[(int)Math.Round((1 - Confidence) * ValueThrough.Count)]);
            }
        }
        
    }
}
