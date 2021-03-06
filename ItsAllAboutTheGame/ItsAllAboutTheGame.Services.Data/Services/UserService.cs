﻿using ItsAllAboutTheGame.Data;
using ItsAllAboutTheGame.Data.Models;
using ItsAllAboutTheGame.GlobalUtilities;
using ItsAllAboutTheGame.GlobalUtilities.Constants;
using ItsAllAboutTheGame.GlobalUtilities.Contracts;
using ItsAllAboutTheGame.GlobalUtilities.Enums;
using ItsAllAboutTheGame.Services.Data.Contracts;
using ItsAllAboutTheGame.Services.Data.DTO;
using ItsAllAboutTheGame.Services.Data.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using X.PagedList;

namespace ItsAllAboutTheGame.Services.Data
{
    public class UserService : IUserService
    {
        private readonly IForeignExchangeService foreignExchangeService;
        private readonly ItsAllAboutTheGameDbContext context;
        private readonly IWalletService walletService;
        private readonly IDateTimeProvider dateTimeProvider;

        public UserService(ItsAllAboutTheGameDbContext context, 
             IForeignExchangeService foreignExchangeService,
            IWalletService walletService, IDateTimeProvider dateTimeProvider)
        {
            this.context = context;
            this.dateTimeProvider = dateTimeProvider;
            this.foreignExchangeService = foreignExchangeService;
            this.walletService = walletService;
        }

        public async Task<UserInfoDTO> GetUserInfo(string userId)
        {
            try
            {
                var currencies = await this.foreignExchangeService.GetConvertionRates();

                var user = await context
                    .Users
                    .Where(x => x.Id == userId)
                    .Include(u => u.Wallet)
                    .FirstOrDefaultAsync();

                var userInfo = new UserInfoDTO(user, currencies);

                var getCurrencySymbol = CultureReferences.CurrencySymbols.TryGetValue(userInfo.Currency, out string currencySymbol);

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

        public async Task<IPagedList<UserDTO>> GetAllUsers(string searchByUsername = null, int page = 1, int size = GlobalConstants.DefultPageSize, string sortOrder = GlobalConstants.DefaultUserSorting)
        {
            var users = this.context
                .Users
                .Where(u => u.Email != GlobalConstants.MasterAdminEmail)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchByUsername))
            {
                users = users
                    .Where(user => user.UserName.Contains(searchByUsername, StringComparison.InvariantCultureIgnoreCase));
            }

            var property = sortOrder.Remove(sortOrder.IndexOf("_"));
            PropertyInfo prop = typeof(User).GetProperty(property, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);

            if (!sortOrder.Contains("_desc"))
            {
                users = users.OrderBy(user => prop.GetValue(user));
            }
            else
            {
                users = users.OrderByDescending(user => prop.GetValue(user));
            }

            var result = await users
                .Select(user => new UserDTO()
                {
                    UserId = user.Id,
                    LockoutFor = GetLockoutDays(user),
                    Username = user.UserName,
                    Deleted = user.IsDeleted,
                    Admin = user.Role.Equals(UserRole.Administrator),
                })
                .ToPagedListAsync(page, size);

            return result;
        }

        public async Task<UserDTO> LockoutUser(string userId, int days)
        {
            try
            {
                var user = await this.context.Users.FindAsync(userId);

                var date = dateTimeProvider.UtcNow.AddDays(days);

                user.LockoutEnd = new DateTimeOffset(date, TimeSpan.Zero);

                this.context.Users.Update(user);

                await this.context.SaveChangesAsync();

                var updatedUser = new UserDTO(user);

                updatedUser.LockoutFor = GetLockoutDays(user);

                return updatedUser;

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

                this.context.Users.Update(user);

                await this.context.SaveChangesAsync();

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

                var role = await this.context.Roles.Where(r => r.Name == GlobalConstants.AdminRole).FirstOrDefaultAsync();

                var roleId = role.Id;

                var userRole = await this.context.UserRoles.Where(ur => ur.UserId == userId && ur.RoleId == roleId).FirstOrDefaultAsync();

                if (userRole != null)
                {
                    user.Role = UserRole.None;

                    this.context.UserRoles.Remove(userRole);

                    await this.context.SaveChangesAsync();
                }
                else
                {
                    user.Role = UserRole.Administrator;

                    userRole = new IdentityUserRole<string>();

                    userRole.UserId = userId;

                    userRole.RoleId = roleId;

                    this.context.UserRoles.Add(userRole);

                    await this.context.SaveChangesAsync();
                }

                return new UserDTO(user);

            }
            catch (Exception ex)
            {
                throw new ToggleAdminException("Unable to toggle Admin Role to the selected user", ex);
            }
        }

        private int GetLockoutDays(User user)
        {
            if (user.LockoutEnd == null)
            {
                return 0;
            }

            var lockOutEndDate = user.LockoutEnd.Value.DateTime;

            var currentDate = dateTimeProvider.Now;

            var difference = (lockOutEndDate - currentDate).TotalDays;

            return (int)(difference > 0 ? Math.Round(difference, 1) : 0);
        }
    }
}
