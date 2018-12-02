﻿using ItsAllAboutTheGame.Data;
using ItsAllAboutTheGame.Data.Models;
using ItsAllAboutTheGame.Data.Models.Enums;
using ItsAllAboutTheGame.Services.Data.Constants;
using ItsAllAboutTheGame.Services.Data.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ItsAllAboutTheGame.Services.Data.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly ItsAllAboutTheGameDbContext context;
        private readonly UserManager<User> userManager;
        private readonly IWalletService walletService;
        private readonly IUserService userService;
        private readonly ICardService cardService;
        private readonly IForeignExchangeService foreignExchangeService;
        private readonly ServicesDataConstants constants;

        public TransactionService(ItsAllAboutTheGameDbContext context, IWalletService walletService,
            UserManager<User> userManager, IUserService userService, IForeignExchangeService foreignExchangeService,
            ServicesDataConstants constants, ICardService cardService)
        {
            this.context = context;
            this.walletService = walletService;
            this.userManager = userManager;
            this.userService = userService;
            this.foreignExchangeService = foreignExchangeService;
            this.constants = constants;
            this.cardService = cardService;
        }

        public async Task<Transaction> MakeDeposit(User user, int cardId, decimal amount)
        {
            var usercard = await this.cardService.GetCardNumber(user, cardId);
            var userWallet =  await this.context.Wallets.FirstOrDefaultAsync(w => w.User == user);
           
            var rates = await this.foreignExchangeService.GetConvertionRates();
            var convertedamount = amount / rates.Rates[userWallet.Currency.ToString()];

             user.Wallet.Balance += convertedamount;

            var transaction =  new Transaction()
            {
                Type = TransactionType.Deposit,
                Description = constants.DepositDescription + usercard,
                User = user,
                UserId = user.Id,
                Amount = convertedamount,
                CreatedOn = DateTime.Now
            };

            this.context.Transactions.Add(transaction);
            await this.context.SaveChangesAsync();
            

            return transaction;
        }
    }
}
