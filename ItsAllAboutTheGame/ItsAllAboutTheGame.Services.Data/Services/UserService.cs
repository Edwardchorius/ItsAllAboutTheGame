﻿using ItsAllAboutTheGame.Data;
using ItsAllAboutTheGame.Data.Models;
using ItsAllAboutTheGame.Data.Models.Enums;
using ItsAllAboutTheGame.GlobalUtilities.Constants;
using ItsAllAboutTheGame.Services.Data.Constants;
using ItsAllAboutTheGame.Services.Data.Contracts;
using ItsAllAboutTheGame.Services.Data.DTO;
using ItsAllAboutTheGame.Services.Data.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using X.PagedList;

namespace ItsAllAboutTheGame.Services.Data
{
    public class UserService : IUserService
    {
        private readonly IForeignExchangeService foreignExchangeService;
        private readonly ItsAllAboutTheGameDbContext context;
        private readonly UserManager<User> userManager;
        private readonly SignInManager<User> signInManager;
        private readonly IWalletService walletService;

        public UserService(ItsAllAboutTheGameDbContext context, UserManager<User> userManager,
            SignInManager<User> signInManager, IForeignExchangeService foreignExchangeService,
            IWalletService walletService)
        {
            this.context = context;
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.foreignExchangeService = foreignExchangeService;
            this.walletService = walletService;
        }

        public async Task<User> RegisterUser(string email, string firstName, string lastName, DateTime dateOfBirth, Currency userCurrency)
        {
            var currentDate = DateTime.Now;

            try
            {
                if (currentDate.Subtract(dateOfBirth).TotalDays < 6575)
                {
                    throw new UserNo18Exception("User must have 18 years to register!");
                }
            }
            catch (UserNo18Exception ex)
            {
                throw new UserNo18Exception(ex.Message);
            }


            User user = new User
            {
                Cards = new List<CreditCard>(),
                Transactions = new List<Transaction>(),
                UserName = email,
                CreatedOn = DateTime.Now,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                DateOfBirth = dateOfBirth
            };

            Wallet wallet = await walletService.CreateUserWallet(user, userCurrency);
            user.Wallet = wallet;
            user.WalletId = wallet.Id;

            return user;
        }

        public async Task<User> RegisterUserWithLoginProvider(ExternalLoginInfo info, Currency userCurrency, DateTime dateOfBirth)
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var name = info.Principal.FindFirstValue(ClaimTypes.Name).Split().ToArray();
            string firstName = name[0];
            string lastName = name[1];

            var currentDate = DateTime.Now;

            try
            {
                if (currentDate.Subtract(dateOfBirth).TotalDays < 6575)
                {
                    throw new UserNo18Exception("User must have 18 years to register!");
                }
            }
            catch (UserNo18Exception ex)
            {
                throw new UserNo18Exception(ex.Message);
            }

            User user = new User
            {
                UserName = email,
                CreatedOn = DateTime.Now,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                DateOfBirth = dateOfBirth
            };

            Wallet wallet = await walletService.CreateUserWallet(user, userCurrency);
            user.Wallet = wallet;
            user.WalletId = wallet.Id;

            return user;
        }

        public async Task<UserInfoDTO> GetUserInfo(ClaimsPrincipal userClaims)
        {
            try
            {
                var userId = userManager.GetUserId(userClaims);

                var currencies = await this.foreignExchangeService.GetConvertionRates();

                var user = await userManager
                    .Users
                    .Where(x => x.Id == userId)
                    .Include(u => u.Wallet)
                    .FirstOrDefaultAsync();

                var userInfo = new UserInfoDTO(user, currencies);

                var getCurrencySymbol = ServicesDataConstants.CurrencySymbols.TryGetValue(userInfo.Currency, out string currencySymbol);

                if (!getCurrencySymbol)
                {
                    throw new EntityNotFoundException("Currency with such ISOCurrencySymbol cannot be found");
                }
                userInfo.CurrencySymbol = currencySymbol;

                return userInfo;
            }
            catch (Exception ex)
            {
                throw new EntityNotFoundException("We cannot find your remembered user. Please manually delete your cookies and Login again.", ex);
            }
        }

        public async Task<User> GetUser(string username)
        {
            var user = await this.context.Users.FirstOrDefaultAsync(n => n.UserName == username);

            return user;
        }

        public async Task<IEnumerable<CreditCard>> UserCards(User user)
        {
            var userCards = await this.context.CreditCards.Where(k => k.User == user).ToListAsync();

            return userCards;
        }

        public IPagedList<UserDTO> GetAllUsers(string searchByUsername = null, int page = 1, int size = GlobalConstants.DefultPageSize, string sortOrder = GlobalConstants.DefultUserSorting)
        {
            var users = this.context
                .Users
                .Where(u => u.Email != GlobalConstants.MasterAdminEmail)
                .Include(user => user.Wallet)
                .Include(user => user.Cards)
                .Select(u => new UserDTO(u));

            var property = sortOrder.Remove(sortOrder.IndexOf("_"));
            PropertyInfo prop = typeof(UserDTO).GetProperty(property, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
            if (!sortOrder.Contains("_desc"))
            {
                users = users.OrderBy(user => prop.GetValue(user));
            }
            else
            {
                users = users.OrderByDescending(user => prop.GetValue(user));
            }

            if (!string.IsNullOrEmpty(searchByUsername))
            {
                users = users.Where(user => user.Username.Contains(searchByUsername, StringComparison.InvariantCultureIgnoreCase));
            }

            return users.ToPagedList(page, size);
        }


        public IPagedList<TransactionDTO> GetUserTransactionHistory(string userId, int page = 1, int size = GlobalConstants.DefultPageSize, string sortOrder = GlobalConstants.DefultTransactionSorting)
        {
            var user = this.context.Users.Where(u => u.Id == userId).Include(t => t.Transactions).FirstOrDefault();

            var transactions = user.Transactions.Select(tr => new TransactionDTO(tr));

            var property = sortOrder.Remove(sortOrder.IndexOf("_"));
            PropertyInfo prop = typeof(TransactionDTO).GetProperty(property, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
            if (!sortOrder.Contains("_desc"))
            {
                transactions = transactions.OrderBy(transaction => prop.GetValue(transaction));
            }
            else
            {
                transactions = transactions.OrderByDescending(transaction => prop.GetValue(transaction));
            }


            return transactions.ToPagedList(page, size);
        }
        

        public async Task<UserDTO> LockoutUser(string userId, int days)
        {
            try
            {
                var user = await this.context.Users.FindAsync(userId);

                var date = DateTime.UtcNow.AddDays(days);

                user.LockoutEnd = new DateTimeOffset(date, TimeSpan.Zero);

                await this.userManager.UpdateAsync(user);

                return new UserDTO(user);

            }
            catch (Exception ex)
            {
                throw new LockoutUserException("Unable to Lockout the selected user", ex);
            }
        }

        public async Task<UserDTO> DeleteUser(string userId)
        {
            try
            {
                var user = await this.context.Users.FindAsync(userId);

                user.IsDeleted = !user.IsDeleted;

                await this.userManager.UpdateAsync(user);

                return new UserDTO(user);

            }
            catch (Exception ex)
            {
                throw new DeleteUserException("Unable to Delete the selected user", ex);
            }
        }

        public async Task<UserDTO> ToggleAdmin(string userId)
        {
            try
            {
                var user = await this.context.Users.FindAsync(userId);

                var result = await this.userManager
                .IsInRoleAsync(user, GlobalConstants.AdminRole);

                if (result)
                {
                    user.Role = UserRole.None;
                    await this.userManager.RemoveFromRoleAsync(user, GlobalConstants.AdminRole);
                }
                else
                {
                    user.Role = UserRole.Administrator;
                    await this.userManager.AddToRoleAsync(user, GlobalConstants.AdminRole);
                }

                return new UserDTO(user);

            }
            catch (Exception ex)
            {
                throw new ToggleAdminException("Unable to toggle Admin Role  the selected user", ex);
            }
        }
    }
}
