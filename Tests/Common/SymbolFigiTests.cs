﻿using NUnit.Framework;
using QuantConnect.Configuration;
using QuantConnect.ToolBox.RandomDataGenerator;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuantConnect.Tests.Common
{
    [TestFixture]
    internal class SymbolFigiTests
    {
        private readonly List<DirectoryInfo> _temporaryFolders = new List<DirectoryInfo>();

        [Test]
        public void MapIsLoadedOnlyAfterFigiPropertyIsAccessed()
        {
            // Arrange - genrate 500 symbols
            var fakeData = GenerateFakeSidFigiData(500);
            var symbol = fakeData.First().Key;

            // Assert
            Assert.IsFalse(symbol.SidFigiMapLoaded, "No Symbol accessed the FIGI property yet, so the map should not be loaded.");

            // Act
            var figi = symbol.FIGI;

            // Assert
            Assert.IsTrue(symbol.SidFigiMapLoaded, "a Symbol accessed the FIGI property, so the map should de loaded.");
        }

        [Test]
        public void EmptySymbolReturnsEmptyFIGI()
        {
            // Arrange
            var symbol = Symbol.Empty;
            // Assert
            Assert.AreEqual(symbol.FIGI, "");
        }

        [TestCase("IBM", "IBM R735QTJ8XC9X", "BBG000BLNNH6")]
        [TestCase("GOOGL", "GOOG T1AZ164W5VTX", "BBG009S39JX6")]
        [TestCase("GOOG", "GOOCV VP83T1ZUHROL", "BBG009S3NB30")]
        [TestCase("ABC", "ABC 2T", "")]
        [TestCase("AAPL", "AAPL R735QTJ8XC9X", "", Description = "This case is not present in in the sample map")]
        [TestCase("BTCUSD", "BTCUSD XJ", "", Description = "FIGI is only available for equities... for now.")]
        public void GetSecurityFigiFromSymbol(string ticker, string securityIdentifier, string expectedFigi)
        {
            // Arrange
            var sid = SecurityIdentifier.Parse(securityIdentifier);
            var symbol = new Symbol(sid, ticker);

            // Act
            var actualFigi = symbol.FIGI;

            // Assert
            Assert.AreEqual(expectedFigi, actualFigi);
        }

        [Test]
        public void MapFileNotPresent()
        {
            // Arrange
            var mockedNas = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            Config.Set("data-folder", mockedNas.FullName);
            Globals.Reset();

            var sid = SecurityIdentifier.Parse("IBM R735QTJ8XC9X");
            var symbol = new Symbol(sid, "IBM");
            // Assert
            string figi;
            Assert.DoesNotThrow(() => figi = symbol.FIGI);
            Assert.AreEqual(string.Empty, symbol.FIGI);
        }

        [Test]
        public void MultithreadStressTest()
        {
            // Arrange

            // Set a mocked nas folder
            var mockedNas = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            _temporaryFolders.Add(mockedNas);
            Config.Set("data-folder", mockedNas.FullName);

            // Generate  fake data and save it in the mocked nas folder
            var fakeData = GenerateFakeSidFigiData(1000);
            var mockedSidFigiMapFile = new FileInfo(
                Path.Combine(
                    mockedNas.FullName,
                    SecurityType.Equity.SecurityTypeToLower(),
                    Market.USA.ToLowerInvariant(),
                    "map_files",
                    "symbol_figi_map.csv"
                )
            );
            mockedSidFigiMapFile.Directory.Create();
            File.WriteAllLines(mockedSidFigiMapFile.FullName, fakeData.Select(r => $"{r.Key.ID},{r.Value}"));
            Globals.Reset();

            // Act and Assert
            Parallel.ForEach(fakeData,
                kvp =>
                {
                    Assert.AreEqual(kvp.Value, kvp.Key.FIGI);
                });
        }

        [TearDown]
        public void DeleteTemporaryFolder()
        {
            foreach (var temporaryFolder in _temporaryFolders)
            {
                if (temporaryFolder.Exists) temporaryFolder.Delete(true);
            }
        }

        public Dictionary<Symbol, string> GenerateFakeSidFigiData(int samplesCount)
        {
            var settings = new RandomDataGeneratorSettings
            {
                Market = Market.USA,
                SecurityType = SecurityType.Equity,
                SymbolCount = samplesCount,
            };
            var generator = new SymbolGenerator(settings, new RandomValueGenerator());
            var randomSymbols = generator.GenerateRandomSymbols();
            var output = randomSymbols.ToDictionary(
                s => s,
                s => Guid.NewGuid().ToString().Replace("-", string.Empty).Substring(0, 12).ToUpperInvariant()
            );
            return output;
        }
    }
}