﻿using ItsAllAboutTheGame.Data.Models.Enums;
using ItsAllAboutTheGame.Services.Data.Contracts;
using ItsAllAboutTheGame.Services.Data.DTO;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ItsAllAboutTheGame.Services.Data.Constants
{
    public class ServicesDataConstants
    {
        private static string currencies;

        private static Dictionary<string, string> currencySymbols;

        private static Action[] Sorting;

        private static string baseCurrency;

        private readonly IMemoryCache cache;


        public ServicesDataConstants(IMemoryCache cache)
        {
            SetCurrencySymbols();
            currencies = string.Join(",", Enum.GetNames(typeof(Currency)));
            baseCurrency = "USD";
            this.cache = cache;
        }

        

        public static Dictionary<string, string> CurrencySymbols
        {
            get => currencySymbols;
        }

        public string Currencies {
            get => currencies;
        }

        public string BaseCurrency
        {
            get => baseCurrency;
        }

        private static void SetCurrencySymbols()
        {
            currencySymbols = CultureInfo
                .GetCultures(CultureTypes.AllCultures)
                .Where(c => !c.IsNeutralCulture)
                .Select(culture =>
                {
                    try
                    {
                        return new RegionInfo(culture.LCID);
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(ri => ri != null)
                .GroupBy(ri => ri.ISOCurrencySymbol)
                .ToDictionary(x => x.Key, x => x.First().CurrencySymbol);
        }

        public string DepositDescription = "Deposit with card ";
    }
}
