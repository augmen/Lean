﻿/*
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
using NUnit.Framework;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Securities.Future;
using QuantConnect.Securities.Option;
using QuantConnect.Tests.Engine.DataFeeds;

namespace QuantConnect.Tests.Common.Securities
{
    [TestFixture]
    public class FutureMarginBuyingPowerModelTests
    {
        // Test class to enable calling protected methods
        public class TestFutureMarginModel : FutureMarginModel
        {
            public TestFutureMarginModel(Security security = null)
            : base(security: security)
            {
            }

            public new decimal GetMaintenanceMargin(Security security)
            {
                return base.GetMaintenanceMargin(security);
            }

            public new decimal GetInitialMarginRequirement(Security security)
            {
                return base.GetInitialMarginRequirement(security);
            }

            public new decimal GetInitialMarginRequiredForOrder(
                InitialMarginRequiredForOrderParameters parameters)
            {
                return base.GetInitialMarginRequiredForOrder(parameters);
            }
        }

        [Test]
        public void TestMarginForSymbolWithOneLinerHistory()
        {
            const decimal price = 1.2345m;
            var time = new DateTime(2016, 1, 1);
            var expDate = new DateTime(2017, 1, 1);
            var tz = TimeZones.NewYork;

            // For this symbol we don't have any history, but only one date and margins line
            var ticker = QuantConnect.Securities.Futures.Softs.Coffee;
            var symbol = Symbol.CreateFuture(ticker, Market.USA, expDate);

            var futureSecurity = new Future(
                SecurityExchangeHours.AlwaysOpen(tz),
                new SubscriptionDataConfig(typeof(TradeBar), symbol, Resolution.Minute, tz, tz, true, false, false),
                new Cash(Currencies.USD, 0, 1m),
                new OptionSymbolProperties(SymbolProperties.GetDefault(Currencies.USD)),
                ErrorCurrencyConverter.Instance,
                RegisteredSecurityDataTypesProvider.Null
            );
            futureSecurity.SetMarketPrice(new Tick { Value = price, Time = time });
            futureSecurity.Holdings.SetHoldings(1.5m, 1);

            var buyingPowerModel = new TestFutureMarginModel();
            Assert.AreEqual(2900m, buyingPowerModel.GetMaintenanceMargin(futureSecurity));
        }

        [Test]
        public void TestMarginForSymbolWithNoHistory()
        {
            const decimal price = 1.2345m;
            var time = new DateTime(2016, 1, 1);
            var expDate = new DateTime(2017, 1, 1);
            var tz = TimeZones.NewYork;

            // For this symbol we don't have any history at all
            var ticker = "NOT-A-SYMBOL";
            var symbol = Symbol.CreateFuture(ticker, Market.USA, expDate);

            var futureSecurity = new Future(SecurityExchangeHours.AlwaysOpen(tz),
                new SubscriptionDataConfig(typeof(TradeBar), symbol, Resolution.Minute, tz, tz, true, false, false),
                new Cash(Currencies.USD, 0, 1m),
                new OptionSymbolProperties(SymbolProperties.GetDefault(Currencies.USD)),
                ErrorCurrencyConverter.Instance,
                RegisteredSecurityDataTypesProvider.Null);
            futureSecurity.SetMarketPrice(new Tick { Value = price, Time = time });
            futureSecurity.Holdings.SetHoldings(1.5m, 1);

            var buyingPowerModel = new TestFutureMarginModel();
            Assert.AreEqual(0m, buyingPowerModel.GetMaintenanceMargin(futureSecurity));
        }

        [Test]
        public void TestMarginForSymbolWithHistory()
        {
            const decimal price = 1.2345m;
            var time = new DateTime(2013, 1, 1);
            var expDate = new DateTime(2017, 1, 1);
            var tz = TimeZones.NewYork;

            // For this symbol we don't have history
            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;
            var symbol = Symbol.CreateFuture(ticker, Market.USA, expDate);

            var futureSecurity = new Future(
                SecurityExchangeHours.AlwaysOpen(tz),
                new SubscriptionDataConfig(typeof(TradeBar), symbol, Resolution.Minute, tz, tz, true, false, false),
                new Cash(Currencies.USD, 0, 1m),
                new OptionSymbolProperties(SymbolProperties.GetDefault(Currencies.USD)),
                ErrorCurrencyConverter.Instance,
                RegisteredSecurityDataTypesProvider.Null
            );
            futureSecurity.SetMarketPrice(new Tick { Value = price, Time = time });
            futureSecurity.Holdings.SetHoldings(1.5m, 1);

            var buyingPowerModel = new TestFutureMarginModel();
            Assert.AreEqual(625m, buyingPowerModel.GetMaintenanceMargin(futureSecurity));

            // now we move forward to exact date when margin req changed
            time = new DateTime(2014, 06, 13);
            futureSecurity.SetMarketPrice(new Tick { Value = price, Time = time });
            Assert.AreEqual(725m, buyingPowerModel.GetMaintenanceMargin(futureSecurity));

            // now we fly beyond the last line of the history file (currently) to see how margin model resolves future dates
            time = new DateTime(2016, 06, 04);
            futureSecurity.SetMarketPrice(new Tick { Value = price, Time = time });
            Assert.AreEqual(585m, buyingPowerModel.GetMaintenanceMargin(futureSecurity));
        }

        [TestCase(0)]
        [TestCase(10000)]
        public void NonAccountCurrency_GetBuyingPower(decimal nonAccountCurrencyCash)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            algorithm.Portfolio.SetAccountCurrency("EUR");
            algorithm.Portfolio.SetCash(10000);
            algorithm.Portfolio.SetCash(Currencies.USD, nonAccountCurrencyCash, 0.88m);

            // For this symbol we don't have history
            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;

            var futureSecurity = algorithm.AddFuture(ticker);

            var buyingPowerModel = new FutureMarginModel();
            var quantity = buyingPowerModel.GetBuyingPower(new BuyingPowerParameters(
                algorithm.Portfolio, futureSecurity, OrderDirection.Buy));

            Assert.AreEqual(10000m + algorithm.Portfolio.CashBook[Currencies.USD].ValueInAccountCurrency,
                quantity.Value);
        }

        [Test]
        public void NonAccountCurrency_GetMaintenanceMarginRequirement()
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            algorithm.Portfolio.SetAccountCurrency("EUR");
            algorithm.Portfolio.SetCash(10000);
            algorithm.Portfolio.SetCash(Currencies.USD, 0, 0.88m);

            // For this symbol we don't have history
            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;

            const decimal price = 1.2345m;
            var time = new DateTime(2013, 1, 1);
            var futureSecurity = algorithm.AddFuture(ticker);
            futureSecurity.SetMarketPrice(new Tick { Value = price, Time = time });
            futureSecurity.Holdings.SetHoldings(1.5m, 1);

            var buyingPowerModel = new FutureMarginModel(security: futureSecurity);
            var res = buyingPowerModel.GetMaintenanceMarginRequirement(futureSecurity);

            var margin = buyingPowerModel.MaintenanceMarginRequirement * futureSecurity.Holdings.AbsoluteQuantity;
            var expectedPercentage = margin / futureSecurity.Holdings.HoldingsValue;

            Assert.AreEqual(expectedPercentage, res);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void GetMaintenanceMargin(decimal quantity)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;

            const decimal price = 1.2345m;
            var time = new DateTime(2013, 1, 1);
            var futureSecurity = algorithm.AddFuture(ticker);
            var buyingPowerModel = new TestFutureMarginModel(futureSecurity);
            futureSecurity.SetMarketPrice(new Tick { Value = price, Time = time });
            futureSecurity.Holdings.SetHoldings(1.5m, quantity);

            var res = buyingPowerModel.GetMaintenanceMargin(futureSecurity);
            Assert.AreEqual(buyingPowerModel.MaintenanceMarginRequirement * futureSecurity.Holdings.AbsoluteQuantity, res);

            // We increase the quantity * 2, maintenance margin should DOUBLE
            futureSecurity.Holdings.SetHoldings(1.5m, quantity * 2);
            res = buyingPowerModel.GetMaintenanceMargin(futureSecurity);
            Assert.AreEqual(buyingPowerModel.MaintenanceMarginRequirement * futureSecurity.Holdings.AbsoluteQuantity, res);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void GetMaintenanceMarginRequirement(decimal quantity)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;

            const decimal price = 1.2345m;
            var time = new DateTime(2013, 1, 1);
            var futureSecurity = algorithm.AddFuture(ticker);
            var buyingPowerModel = new FutureMarginModel(security: futureSecurity);
            futureSecurity.SetMarketPrice(new Tick { Value = price, Time = time });
            futureSecurity.Holdings.SetHoldings(1.5m, quantity);

            var res = buyingPowerModel.GetMaintenanceMarginRequirement(futureSecurity);
            var margin = buyingPowerModel.MaintenanceMarginRequirement * futureSecurity.Holdings.AbsoluteQuantity;
            var expectedPercentage = margin / futureSecurity.Holdings.HoldingsValue;
            Assert.AreEqual(Math.Abs(expectedPercentage), res);

            // We increase the quantity * 2 but maintenance margin PERCENTAGE requirement stays the same and its absolute
            futureSecurity.Holdings.SetHoldings(1.5m, quantity * 2);
            res = buyingPowerModel.GetMaintenanceMarginRequirement(futureSecurity);
            Assert.AreEqual(Math.Abs(expectedPercentage), res);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void GetInitialMarginRequirement(decimal quantity)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;

            const decimal price = 1.2345m;
            var time = new DateTime(2013, 1, 1);
            var futureSecurity = algorithm.AddFuture(ticker);
            var buyingPowerModel = new TestFutureMarginModel();
            futureSecurity.SetMarketPrice(new Tick { Value = price, Time = time });
            futureSecurity.Holdings.SetHoldings(1.5m, quantity);

            var initialMargin = buyingPowerModel.GetInitialMarginRequirement(futureSecurity);
            Assert.IsTrue(initialMargin > 0);
            var overnightMargin = buyingPowerModel.GetMaintenanceMarginRequirement(futureSecurity);

            // initial margin is greater than the maintenance margin
            Assert.Greater(initialMargin, overnightMargin);
        }

        [TestCase(10)]
        [TestCase(-10)]
        public void GetInitialMarginRequiredForOrder(decimal quantity)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;

            const decimal price = 1.2345m;
            var time = new DateTime(2013, 1, 1);
            var futureSecurity = algorithm.AddFuture(ticker);
            var buyingPowerModel = new TestFutureMarginModel(futureSecurity);
            futureSecurity.SetMarketPrice(new Tick { Value = price, Time = time });
            futureSecurity.Holdings.SetHoldings(1.5m, 1);

            var initialMargin = buyingPowerModel.GetInitialMarginRequiredForOrder(
                new InitialMarginRequiredForOrderParameters(algorithm.Portfolio.CashBook,
                    futureSecurity,
                    new MarketOrder(futureSecurity.Symbol, quantity, algorithm.UtcTime)));

            var initialMarginPercentage = buyingPowerModel.GetInitialMarginRequirement(futureSecurity);

            Assert.AreEqual(initialMarginPercentage
                            * (new MarketOrder(futureSecurity.Symbol, quantity, algorithm.UtcTime).GetValue(futureSecurity))
                            + 18.50m * Math.Sign(quantity), // fees -> 10 quantity * 1.85
                initialMargin);
        }

        [Test]
        public void GetBuyingPowerNoHoldings()
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;

            var futureSecurity = algorithm.AddFuture(ticker);
            var buyingPowerModel = new TestFutureMarginModel(futureSecurity);

            // No position
            var buyingPower = buyingPowerModel.GetBuyingPower(
                new BuyingPowerParameters(algorithm.Portfolio, futureSecurity, OrderDirection.Buy)).Value;
            Assert.AreEqual(algorithm.Portfolio.MarginRemaining, buyingPower);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void GetBuyingPowerWithHoldings_SameDirection(decimal quantity)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;

            var futureSecurity = algorithm.AddFuture(ticker);
            var buyingPowerModel = new TestFutureMarginModel(futureSecurity);
            var time = new DateTime(2013, 1, 1);
            futureSecurity.SetMarketPrice(new Tick { Value = 1.5m, Time = time });
            futureSecurity.Holdings.SetHoldings(1.5m, quantity);

            var buyingPower = buyingPowerModel.GetBuyingPower(
                new BuyingPowerParameters(algorithm.Portfolio, futureSecurity, quantity > 0 ? OrderDirection.Buy : OrderDirection.Sell)).Value;
            Assert.AreEqual(
                algorithm.Portfolio.TotalPortfolioValue
                - buyingPowerModel.GetMaintenanceMargin(futureSecurity), // current position
                buyingPower);
            Assert.IsTrue(buyingPower > 0);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void GetBuyingPowerWithHoldings_ReverseDirection(decimal quantity)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;

            var futureSecurity = algorithm.AddFuture(ticker);
            var buyingPowerModel = new TestFutureMarginModel(futureSecurity);
            var time = new DateTime(2013, 1, 1);
            futureSecurity.SetMarketPrice(new Tick { Value = 1.5m, Time = time });
            futureSecurity.Holdings.SetHoldings(1.5m, quantity);

            var buyingPower = buyingPowerModel.GetBuyingPower(
                new BuyingPowerParameters(algorithm.Portfolio, futureSecurity, quantity > 0 ? OrderDirection.Sell : OrderDirection.Buy)).Value;

            Assert.AreEqual(
                algorithm.Portfolio.MarginRemaining
                + buyingPowerModel.GetMaintenanceMargin(futureSecurity) // close position
                + buyingPowerModel.GetInitialMarginRequirement(futureSecurity) * futureSecurity.Holdings.AbsoluteHoldingsValue, // open position
                buyingPower);
            Assert.IsTrue(buyingPower > 0);
        }

        [TestCase(100)]
        [TestCase(-100)]
        public void MarginUsedForPositionWhenPriceDrops(decimal quantity)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));

            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;
            var futureSecurity = algorithm.AddFuture(ticker);
            futureSecurity.Holdings.SetHoldings(20, quantity);
            Update(futureSecurity, 20, algorithm);

            var marginForPosition = futureSecurity.BuyingPowerModel.GetReservedBuyingPowerForPosition(
                new ReservedBuyingPowerForPositionParameters(futureSecurity)).Value;

            // Drop 40% price from $20 to $12
            Update(futureSecurity, 12, algorithm);

            var marginForPositionAfter = futureSecurity.BuyingPowerModel.GetReservedBuyingPowerForPosition(
                new ReservedBuyingPowerForPositionParameters(futureSecurity)).Value;

            Assert.AreEqual(marginForPosition, marginForPositionAfter);
        }

        [TestCase(100)]
        [TestCase(-100)]
        public void MarginUsedForPositionWhenPriceIncreases(decimal quantity)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));

            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;
            var futureSecurity = algorithm.AddFuture(ticker);
            futureSecurity.Holdings.SetHoldings(20, quantity);
            Update(futureSecurity, 20, algorithm);

            var marginForPosition = futureSecurity.BuyingPowerModel.GetReservedBuyingPowerForPosition(
                new ReservedBuyingPowerForPositionParameters(futureSecurity)).Value;

            // Increase from $20 to $40
            Update(futureSecurity, 40, algorithm);

            var marginForPositionAfter = futureSecurity.BuyingPowerModel.GetReservedBuyingPowerForPosition(
                new ReservedBuyingPowerForPositionParameters(futureSecurity)).Value;

            Assert.AreEqual(marginForPosition, marginForPositionAfter);
        }

        [Test]
        public void PortfolioStatusForPositionWhenPriceDrops()
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));

            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;
            var futureSecurity = algorithm.AddFuture(ticker);
            futureSecurity.Holdings.SetHoldings(20, 100);
            Update(futureSecurity, 20, algorithm);

            var marginUsed = algorithm.Portfolio.TotalMarginUsed;
            Assert.IsTrue(marginUsed > 0);
            Assert.IsTrue(algorithm.Portfolio.TotalPortfolioValue > 0);
            Assert.IsTrue(algorithm.Portfolio.MarginRemaining > 0);

            // Drop 40% price from $20 to $12
            Update(futureSecurity, 12, algorithm);

            var expected = (12 - 20) * 100 * futureSecurity.SymbolProperties.ContractMultiplier - 1.85m * 100;
            Assert.AreEqual(futureSecurity.Holdings.UnrealizedProfit, expected);

            // we have a massive loss because of futures leverage
            Assert.IsTrue(algorithm.Portfolio.TotalPortfolioValue < 0);
            Assert.IsTrue(algorithm.Portfolio.MarginRemaining < 0);

            // margin used didn't change because for futures it relies on the maintenance margin
            Assert.AreEqual(marginUsed, algorithm.Portfolio.TotalMarginUsed);
        }

        [Test]
        public void PortfolioStatusPositionWhenPriceIncreases()
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));

            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;
            var futureSecurity = algorithm.AddFuture(ticker);
            futureSecurity.Holdings.SetHoldings(20, 100);
            Update(futureSecurity, 20, algorithm);

            var marginUsed = algorithm.Portfolio.TotalMarginUsed;
            Assert.IsTrue(marginUsed > 0);
            Assert.IsTrue(algorithm.Portfolio.TotalPortfolioValue > 0);
            Assert.IsTrue(algorithm.Portfolio.MarginRemaining > 0);

            // Increase from $20 to $40
            Update(futureSecurity, 40, algorithm);

            var expected = (40 - 20) * 100 * futureSecurity.SymbolProperties.ContractMultiplier - 1.85m * 100;
            Assert.AreEqual(futureSecurity.Holdings.UnrealizedProfit, expected);

            // we have a massive win because of futures leverage
            Assert.IsTrue(algorithm.Portfolio.TotalPortfolioValue > 0);
            Assert.IsTrue(algorithm.Portfolio.MarginRemaining > 0);

            // margin used didn't change because for futures it relies on the maintenance margin
            Assert.AreEqual(marginUsed, algorithm.Portfolio.TotalMarginUsed);
        }

        [Test]
        public void GetLeverage()
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));

            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;
            var futureSecurity = algorithm.AddFuture(ticker);
            var leverage = futureSecurity.BuyingPowerModel.GetLeverage(futureSecurity);

            // security has no price, default value is 1
            Assert.AreEqual(1, leverage);

            Update(futureSecurity, 100, algorithm);

            leverage = futureSecurity.BuyingPowerModel.GetLeverage(futureSecurity);

            // eur usd leverage is high!
            Assert.Greater(leverage, 350);

            Update(futureSecurity, 200, algorithm);
            var leverage2 = futureSecurity.BuyingPowerModel.GetLeverage(futureSecurity);

            // price doubled but initial margin requirement is the same, so the leverage doubled too!
            Assert.AreEqual(leverage * 2, leverage2);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void SetLeverageThrowsException(int leverage)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));

            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;
            var futureSecurity = algorithm.AddFuture(ticker);

            Assert.Throws<InvalidOperationException>(() => futureSecurity.BuyingPowerModel.SetLeverage(futureSecurity, leverage));
        }

        [Test]
        public void MarginRequirementsChangeWithDate()
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));

            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;
            var futureSecurity = algorithm.AddFuture(ticker);
            var model = futureSecurity.BuyingPowerModel as FutureMarginModel;

            Update(futureSecurity, 100, algorithm, new DateTime(2001, 01, 07));
            var initial = model.InitialMarginRequirement;
            var maintenance = model.MaintenanceMarginRequirement;
            Assert.AreEqual(810, initial);
            Assert.AreEqual(600, maintenance);

            // date previous to margin change
            Update(futureSecurity, 100, algorithm, new DateTime(2001, 12, 10));
            Assert.AreEqual(810, initial);
            Assert.AreEqual(600, maintenance);

            // new margins!
            Update(futureSecurity, 100, algorithm, new DateTime(2001, 12, 11));
            Assert.AreEqual(945, model.InitialMarginRequirement);
            Assert.AreEqual(700, model.MaintenanceMarginRequirement);
        }

        [TestCase(-1.1)]
        [TestCase(1.1)]
        public void GetMaximumOrderQuantityForTargetValue_ThrowsForInvalidTarget(decimal target)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));

            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;
            var futureSecurity = algorithm.AddFuture(ticker);

            Assert.Throws<InvalidOperationException>(() => futureSecurity.BuyingPowerModel.GetMaximumOrderQuantityForTargetValue(
                new GetMaximumOrderQuantityForTargetValueParameters(algorithm.Portfolio,
                    futureSecurity,
                    target)));
        }

        [TestCase(1)]
        [TestCase(0.5)]
        [TestCase(-1)]
        [TestCase(-0.5)]
        public void GetMaximumOrderQuantityForTargetValue_NoHoldings(decimal target)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SetFinishedWarmingUp();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            var orderProcessor = new FakeOrderProcessor();
            algorithm.Transactions.SetOrderProcessor(orderProcessor);

            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;
            var futureSecurity = algorithm.AddFuture(ticker);
            Update(futureSecurity, 100, algorithm);
            var model = futureSecurity.BuyingPowerModel as FutureMarginModel;

            var quantity = algorithm.CalculateOrderQuantity(futureSecurity.Symbol, target);

            var expected = (algorithm.Portfolio.TotalPortfolioValue * Math.Abs(target)) / model.InitialMarginRequirement - 1 * Math.Abs(target); // -1 fees
            expected -= expected % futureSecurity.SymbolProperties.LotSize;

            Assert.AreEqual(expected * Math.Sign(target), quantity);

            var request = GetOrderRequest(futureSecurity.Symbol, quantity);
            request.SetOrderId(0);
            orderProcessor.AddTicket(new OrderTicket(algorithm.Transactions, request));

            Assert.IsTrue(model.HasSufficientBuyingPowerForOrder(
                new HasSufficientBuyingPowerForOrderParameters(algorithm.Portfolio,
                    futureSecurity,
                    new MarketOrder(futureSecurity.Symbol, expected, DateTime.UtcNow))).IsSufficient);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void HasSufficientBuyingPowerForOrderInvalidTargets(decimal target)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SetFinishedWarmingUp();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            var orderProcessor = new FakeOrderProcessor();
            algorithm.Transactions.SetOrderProcessor(orderProcessor);

            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;
            var futureSecurity = algorithm.AddFuture(ticker);
            Update(futureSecurity, 100, algorithm);
            var model = futureSecurity.BuyingPowerModel as FutureMarginModel;

            var quantity = algorithm.CalculateOrderQuantity(futureSecurity.Symbol, target);
            var request = GetOrderRequest(futureSecurity.Symbol, quantity);
            request.SetOrderId(0);
            orderProcessor.AddTicket(new OrderTicket(algorithm.Transactions, request));

            var result = model.HasSufficientBuyingPowerForOrder(new HasSufficientBuyingPowerForOrderParameters(
                    algorithm.Portfolio,
                    futureSecurity,
                    // we get the maximum target value 1/-1 and add a lot size it shouldn't be a valid order
                    new MarketOrder(futureSecurity.Symbol, quantity + futureSecurity.SymbolProperties.LotSize * Math.Sign(quantity), DateTime.UtcNow)));

            Assert.IsFalse(result.IsSufficient);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void GetMaximumOrderQuantityForTargetValue_TwoStep(decimal target)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SetFinishedWarmingUp();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            var orderProcessor = new FakeOrderProcessor();
            algorithm.Transactions.SetOrderProcessor(orderProcessor);

            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;
            var futureSecurity = algorithm.AddFuture(ticker);
            Update(futureSecurity, 100, algorithm);
            var expectedFinalQuantity = algorithm.CalculateOrderQuantity(futureSecurity.Symbol, target);

            var quantity = algorithm.CalculateOrderQuantity(futureSecurity.Symbol, target / 2);
            futureSecurity.Holdings.SetHoldings(100, quantity);
            algorithm.Portfolio.InvalidateTotalPortfolioValue();

            var quantity2 = algorithm.CalculateOrderQuantity(futureSecurity.Symbol, target);

            var request = GetOrderRequest(futureSecurity.Symbol, quantity2);
            request.SetOrderId(0);
            orderProcessor.AddTicket(new OrderTicket(algorithm.Transactions, request));

            Assert.IsTrue(futureSecurity.BuyingPowerModel.HasSufficientBuyingPowerForOrder(
                new HasSufficientBuyingPowerForOrderParameters(algorithm.Portfolio,
                    futureSecurity,
                    new MarketOrder(futureSecurity.Symbol, quantity2, DateTime.UtcNow))).IsSufficient);

            // two step operation is the same as 1 step
            Assert.AreEqual(expectedFinalQuantity, quantity + quantity2);
        }

        [TestCase(1)]
        [TestCase(0.5)]
        [TestCase(-1)]
        [TestCase(-0.5)]
        public void GetMaximumOrderQuantityForTargetValue_WithHoldingsSameDirection(decimal target)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SetFinishedWarmingUp();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            var orderProcessor = new FakeOrderProcessor();
            algorithm.Transactions.SetOrderProcessor(orderProcessor);

            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;
            var futureSecurity = algorithm.AddFuture(ticker);
            futureSecurity.Holdings.SetHoldings(100, 10 * Math.Sign(target));
            Update(futureSecurity, 100, algorithm);

            var model = new TestFutureMarginModel(futureSecurity);
            futureSecurity.BuyingPowerModel = model;

            var quantity = algorithm.CalculateOrderQuantity(futureSecurity.Symbol, target);

            var expected = (algorithm.Portfolio.TotalPortfolioValue * Math.Abs(target) - model.GetInitialMarginRequirement(futureSecurity) * futureSecurity.Holdings.AbsoluteHoldingsValue)
                           / model.InitialMarginRequirement - 1 * Math.Abs(target); // -1 fees
            expected -= expected % futureSecurity.SymbolProperties.LotSize;
            Console.WriteLine($"Expected {expected}");

            Assert.AreEqual(expected * Math.Sign(target), quantity);

            var request = GetOrderRequest(futureSecurity.Symbol, quantity);
            request.SetOrderId(0);
            orderProcessor.AddTicket(new OrderTicket(algorithm.Transactions, request));

            Assert.IsTrue(model.HasSufficientBuyingPowerForOrder(
                new HasSufficientBuyingPowerForOrderParameters(algorithm.Portfolio,
                    futureSecurity,
                    new MarketOrder(futureSecurity.Symbol, expected * Math.Sign(target), DateTime.UtcNow))).IsSufficient);
        }

        [TestCase(1)]
        [TestCase(0.5)]
        [TestCase(-1)]
        [TestCase(-0.5)]
        public void GetMaximumOrderQuantityForTargetValue_WithHoldingsInverseDirection(decimal target)
        {
            var algorithm = new QCAlgorithm();
            algorithm.SetFinishedWarmingUp();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            var orderProcessor = new FakeOrderProcessor();
            algorithm.Transactions.SetOrderProcessor(orderProcessor);

            var ticker = QuantConnect.Securities.Futures.Financials.EuroDollar;
            var futureSecurity = algorithm.AddFuture(ticker);
            futureSecurity.Holdings.SetHoldings(100, 10 * -1 * Math.Sign(target));
            Update(futureSecurity, 100, algorithm);

            var model = new TestFutureMarginModel(futureSecurity);
            futureSecurity.BuyingPowerModel = model;

            var quantity = algorithm.CalculateOrderQuantity(futureSecurity.Symbol, target);

            var expected = (algorithm.Portfolio.TotalPortfolioValue * Math.Abs(target) + model.GetInitialMarginRequirement(futureSecurity) * futureSecurity.Holdings.AbsoluteHoldingsValue)
                           / model.InitialMarginRequirement - 1 * Math.Abs(target); // -1 fees
            expected -= expected % futureSecurity.SymbolProperties.LotSize;
            Console.WriteLine($"Expected {expected}");

            Assert.AreEqual(expected * Math.Sign(target), quantity);

            var request = GetOrderRequest(futureSecurity.Symbol, quantity);
            request.SetOrderId(0);
            orderProcessor.AddTicket(new OrderTicket(algorithm.Transactions, request));

            Assert.IsTrue(model.HasSufficientBuyingPowerForOrder(
                new HasSufficientBuyingPowerForOrderParameters(algorithm.Portfolio,
                    futureSecurity,
                    new MarketOrder(futureSecurity.Symbol, expected * Math.Sign(target), DateTime.UtcNow))).IsSufficient);
        }

        private static void Update(Security security, decimal close, QCAlgorithm algorithm, DateTime? time = null)
        {
            security.SetMarketPrice(new TradeBar
            {
                Time = time ?? DateTime.UtcNow,
                Symbol = security.Symbol,
                Open = close,
                High = close,
                Low = close,
                Close = close
            });
            algorithm.Portfolio.InvalidateTotalPortfolioValue();
        }

        private static SubmitOrderRequest GetOrderRequest(Symbol symbol, decimal quantity)
        {
            return new SubmitOrderRequest(OrderType.Market,
                SecurityType.Future,
                symbol,
                quantity,
                1,
                1,
                DateTime.UtcNow,
                "");
        }
    }
}
