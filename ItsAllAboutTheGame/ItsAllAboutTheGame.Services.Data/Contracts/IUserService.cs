﻿using ItsAllAboutTheGame.Data.Models;
using ItsAllAboutTheGame.Data.Models.Enums;
using ItsAllAboutTheGame.GlobalUtilities.Constants;
using ItsAllAboutTheGame.Services.Data.DTO;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using X.PagedList;

namespace ItsAllAboutTheGame.Services.Data.Contracts
{
    public interface IUserService
    {
        Task<User> RegisterUser(string email, string firstName, string lastName,  DateTime dateOfBirth, Currency userCurrency);

        Task<User> RegisterUserWithLoginProvider(ExternalLoginInfo info, Currency userCurrency, DateTime dateOfBirth);

        Task<UserInfoDTO> GetUserInfo(ClaimsPrincipal userClaims);

        Task<User> GetUser(string username);

        IPagedList<UserDTO> GetAllUsers(string searchByUsername = null, int page = 1, int size = 5, string sortOrder = GlobalConstants.DefultUserSorting);

        IPagedList<TransactionDTO> GetUserTransactionHistory(string userId, int page = 1, int size = GlobalConstants.DefultPageSize, string sortOrder = GlobalConstants.DefultTransactionSorting);

        Task<IEnumerable<CreditCard>> UserCards(User user);

        Task<UserDTO> LockoutUser(string userId, int days);

        Task<UserDTO> DeleteUser(string userId);

        Task<UserDTO> ToggleAdmin(string userId);
    }
}
