/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Regression algorithm reproducing data type bugs in the Consolidate API. Related to GH 4205.
    /// </summary>
    public class ConsolidateRegressionAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private List<int> _consolidationCount;
        private int _customDataConsolidator;
        private Symbol _symbol;

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2013, 10, 08);
            SetEndDate(2013, 10, 11);

            var SP500 = QuantConnect.Symbol.Create(Futures.Indices.SP500EMini, SecurityType.Future, Market.CME);
            _symbol = FutureChainProvider.GetFutureContractList(SP500, StartDate).First();
            AddFutureContract(_symbol);

            _consolidationCount = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0 };

            var sma = new SimpleMovingAverage(10);
            Consolidate<QuoteBar>(_symbol, time => new CalendarInfo(time.RoundDown(TimeSpan.FromDays(1)), TimeSpan.FromDays(1)),
                bar => UpdateQuoteBar(sma, bar, 0));

            var sma2 = new SimpleMovingAverage(10);
            Consolidate<QuoteBar>(_symbol, TimeSpan.FromDays(1), bar => UpdateQuoteBar(sma2, bar, 1));

            var sma3 = new SimpleMovingAverage(10);
            Consolidate(_symbol, Resolution.Daily, TickType.Quote, (Action<QuoteBar>)(bar => UpdateQuoteBar(sma3, bar, 2)));

            var sma4 = new SimpleMovingAverage(10);
            Consolidate(_symbol, TimeSpan.FromDays(1), bar => UpdateTradeBar(sma4, bar, 3));

            var sma5 = new SimpleMovingAverage(10);
            Consolidate<TradeBar>(_symbol, TimeSpan.FromDays(1), bar => UpdateTradeBar(sma5, bar, 4));

            // custom data
            var sma6 = new SimpleMovingAverage(10);
            var symbol = AddData<CustomDataRegressionAlgorithm.Bitcoin>("BTC", Resolution.Minute).Symbol;
            Consolidate<TradeBar>(symbol, TimeSpan.FromDays(1), bar => _customDataConsolidator++);

            try
            {
                Consolidate<QuoteBar>(symbol, TimeSpan.FromDays(1), bar => { UpdateQuoteBar(sma6, bar, -1); });
                throw new Exception($"Expected {nameof(ArgumentException)} to be thrown");
            }
            catch (ArgumentException)
            {
                // will try to use BaseDataConsolidator for which input is TradeBars not QuoteBars
            }

            // Test using abstract T types, through defining a 'BaseData' handler
            var sma7 = new SimpleMovingAverage(10);
            Consolidate(_symbol, Resolution.Daily, null, (Action<BaseData>)(bar => UpdateBar(sma7, bar, 5)));

            var sma8 = new SimpleMovingAverage(10);
            Consolidate(_symbol, TimeSpan.FromDays(1), null, (Action<BaseData>)(bar => UpdateBar(sma8, bar, 6)));

            var sma9 = new SimpleMovingAverage(10);
            Consolidate(_symbol, TimeSpan.FromDays(1), (Action<BaseData>)(bar => UpdateBar(sma9, bar, 7)));
        }
        private void UpdateBar(SimpleMovingAverage sma, BaseData tradeBar, int position)
        {
            if (!(tradeBar is TradeBar))
            {
                throw new Exception("Expected a TradeBar");
            }
            _consolidationCount[position]++;
            sma.Update(tradeBar.EndTime, tradeBar.Value);
        }
        private void UpdateTradeBar(SimpleMovingAverage sma, TradeBar tradeBar, int position)
        {
            _consolidationCount[position]++;
            sma.Update(tradeBar.EndTime, tradeBar.High);
        }
        private void UpdateQuoteBar(SimpleMovingAverage sma, QuoteBar quoteBar, int position)
        {
            _consolidationCount[position]++;
            sma.Update(quoteBar.EndTime, quoteBar.High);
        }

        public override void OnEndOfAlgorithm()
        {
            if (_consolidationCount.Any(i => i != 3) || _customDataConsolidator == 0)
            {
                throw new Exception("Unexpected consolidation count");
            }
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice data)
        {
            if (!Portfolio.Invested)
            {
                SetHoldings(_symbol, 0.5);
            }
        }

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = { Language.CSharp, Language.Python };

        /// <summary>
        /// Data Points count of all timeslices of algorithm
        /// </summary>
        public long DataPoints => 5449;

        /// <summary>
        /// Data Points count of the algorithm history
        /// </summary>
        public int AlgorithmHistoryDataPoints => 0;

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Trades", "1"},
            {"Average Win", "0%"},
            {"Average Loss", "0%"},
            {"Compounding Annual Return", "256943094.482%"},
            {"Drawdown", "15.900%"},
            {"Expectancy", "0"},
            {"Net Profit", "16.178%"},
            {"Sharpe Ratio", "43229388091.465"},
            {"Probabilistic Sharpe Ratio", "0%"},
            {"Loss Rate", "0%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "69177566862.121"},
            {"Beta", "8.93"},
            {"Annual Standard Deviation", "1.6"},
            {"Annual Variance", "2.561"},
            {"Information Ratio", "48583550955.512"},
            {"Tracking Error", "1.424"},
            {"Treynor Ratio", "7746445590.006"},
            {"Total Fees", "$23.65"},
            {"Estimated Strategy Capacity", "$210000000.00"},
            {"Lowest Capacity Asset", "ES VMKLFZIH2MTD"},
            {"Fitness Score", "0.999"},
            {"Kelly Criterion Estimate", "0"},
            {"Kelly Criterion Probability Value", "0"},
            {"Sortino Ratio", "79228162514264337593543950335"},
            {"Return Over Maximum Drawdown", "23807.525"},
            {"Portfolio Turnover", "3.518"},
            {"Total Insights Generated", "0"},
            {"Total Insights Closed", "0"},
            {"Total Insights Analysis Completed", "0"},
            {"Long Insight Count", "0"},
            {"Short Insight Count", "0"},
            {"Long/Short Ratio", "100%"},
            {"Estimated Monthly Alpha Value", "$0"},
            {"Total Accumulated Estimated Alpha Value", "$0"},
            {"Mean Population Estimated Insight Value", "$0"},
            {"Mean Population Direction", "0%"},
            {"Mean Population Magnitude", "0%"},
            {"Rolling Averaged Population Direction", "0%"},
            {"Rolling Averaged Population Magnitude", "0%"},
            {"OrderListHash", "dd38e7b94027d20942a5aa9ac31a9a7f"}
        };
    }
}
