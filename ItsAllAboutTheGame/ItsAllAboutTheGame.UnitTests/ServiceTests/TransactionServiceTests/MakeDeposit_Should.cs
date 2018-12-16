﻿using ItsAllAboutTheGame.Data;
using ItsAllAboutTheGame.Data.Models;
using ItsAllAboutTheGame.GlobalUtilities.Contracts;
using ItsAllAboutTheGame.GlobalUtilities.Enums;
using ItsAllAboutTheGame.Services.Data.Contracts;
using ItsAllAboutTheGame.Services.Data.DTO;
using ItsAllAboutTheGame.Services.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ItsAllAboutTheGame.UnitTests.ServiceTests.TransactionServiceTests
{
    [TestClass]
    public class MakeDeposit_Should
    {
        private ServiceProvider serviceProvider = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
        private Mock<IForeignExchangeService> foreignExchangeServiceMock;
        private Mock<IWalletService> walletServiceMock;
        private Mock<IUserService> userServiceMock;
        private Mock<ICardService> cardServiceMock;
        private ForeignExchangeDTO foreignExchangeDTO;
        private Mock<IDateTimeProvider> dateTimeProvider;
        private User user;
        private string userName = "Koicho";
        private string email = "testmail@gmail";
        private string firstName = "Koichkov";
        private string lastName = "Velichkov";
        private CreditCard creditCard;
        private string cardNumber = "23232141412";
        private string cvv = "3232";
        private string lastDigits = "1412";
        private Wallet userWallet;
        private WalletDTO userWalletDTO;
        private string fakeCreatedDate = "01.01.2000";
        private string fakeBirthDate = "02.01.1996";
        private string fakeExpiryDate = "03.03.2022";
        private decimal amount = 1000;
        private Currency testCurrency = Currency.EUR;
        private Dictionary<string, decimal> currencyRates = Enum.GetNames(typeof(Currency)).ToDictionary(name => name, value => 1m);

        [TestMethod]
        public async Task ReturnTransactionDTO_When_PassedValidParams()
        {
            //Arrange
            var contextOptions = new DbContextOptionsBuilder<ItsAllAboutTheGameDbContext>()
                .UseInMemoryDatabase(databaseName: "ReturnTransactionDTO_When_PassedValidParamsMakeDeposit")
                .UseInternalServiceProvider(serviceProvider)
                .Options;

            dateTimeProvider = new Mock<IDateTimeProvider>();
            userServiceMock = new Mock<IUserService>();
            cardServiceMock = new Mock<ICardService>();
            userWallet = new Wallet();

            foreignExchangeDTO = new ForeignExchangeDTO();
            foreignExchangeDTO.Rates = currencyRates;

            foreignExchangeServiceMock = new Mock<IForeignExchangeService>();
            foreignExchangeServiceMock
                 .Setup(foreign => foreign.GetConvertionRates())
                 .ReturnsAsync(foreignExchangeDTO);

            userWalletDTO = new WalletDTO();
            userWalletDTO.Currency = testCurrency;

            walletServiceMock = new Mock<IWalletService>();
            walletServiceMock
                 .Setup(wsm => wsm.GetUserWallet(It.IsAny<User>()))
                 .ReturnsAsync(userWalletDTO);

            //Act
            using (var actContext = new ItsAllAboutTheGameDbContext(contextOptions))
            {
                //await actContext.Wallets.AddAsync(userWallet);
                //await actContext.SaveChangesAsync();
                user = new User
                {
                    Cards = new List<CreditCard>(),
                    Transactions = new List<Transaction>(),
                    UserName = userName,
                    CreatedOn = DateTime.Parse(fakeCreatedDate),
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    DateOfBirth = DateTime.Parse(fakeBirthDate),
                    Role = UserRole.None,
                    Wallet = userWallet,
                    WalletId = userWallet.Id
                };
                await actContext.Users.AddAsync(user);
                await actContext.SaveChangesAsync();
                creditCard = new CreditCard
                {
                    CVV = cvv,
                    CardNumber = cardNumber,
                    LastDigits = lastDigits,
                    ExpiryDate = DateTime.Parse(fakeExpiryDate),
                    CreatedOn = DateTime.Parse(fakeCreatedDate)
                };

                await actContext.CreditCards.AddAsync(creditCard);
                await actContext.SaveChangesAsync();
            }

            //Assert
            using (var assertContext = new ItsAllAboutTheGameDbContext(contextOptions))
            {
                var sut = new TransactionService(assertContext, walletServiceMock.Object,
                    userServiceMock.Object, foreignExchangeServiceMock.Object, cardServiceMock.Object, dateTimeProvider.Object);
                var transactionDTO = await sut.MakeDeposit(user, creditCard.Id, amount);

                Assert.IsInstanceOfType(transactionDTO, typeof(TransactionDTO));
            }
        }
    }
}
