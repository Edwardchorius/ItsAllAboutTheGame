﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ItsAllAboutTheGame.Models.AccountViewModels;
using ItsAllAboutTheGame.Services;
using ItsAllAboutTheGame.Data.Models;
using ItsAllAboutTheGame.Services.Data.Contracts;
using System.Globalization;
using ItsAllAboutTheGame.GlobalUtilities.Contracts;
using System.Linq;
using System.Collections.Generic;
using ItsAllAboutTheGame.GlobalUtilities.Enums;
using System.Security.Claims;

namespace ItsAllAboutTheGame.Controllers
{
    [Authorize]
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        private readonly UserManager<User> userManager;
        private readonly SignInManager<User> signInManager;
        private readonly IEmailSender emailSender;
        private readonly IUserService userService;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly IWalletService walletService;

        public AccountController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IEmailSender emailSender,
            IUserService userService,
            IDateTimeProvider dateTimeProvider,
            IWalletService walletService)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.emailSender = emailSender;
            this.userService = userService;
            this.walletService = walletService;
            this.dateTimeProvider = dateTimeProvider;
        }

        [TempData]
        public string ErrorMessage { get; set; }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string returnUrl = null)
        {
            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ViewData["ReturnUrl"] = returnUrl;

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            this.ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                var result = await this.signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    var user = await this.userManager.FindByEmailAsync(model.Email);

                    if (user.IsDeleted)
                    {
                        await signInManager.SignOutAsync();

                        return RedirectToAction(nameof(Deleted), new { userEmail = model.Email });
                    }
                    else
                    {
                        return RedirectToLocal(returnUrl);
                    }
                }
                if (result.IsLockedOut)
                {
                    return RedirectToAction(nameof(Lockout), new { userEmail = model.Email });
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");

                    ViewData["InvalidLoginAtempt"] = "Invalid Login Atempt";

                    return View(model);
                }
            }
            // If we got this far, something failed, redisplay form
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Lockout(string userEmail)
        {
            var user = await this.userManager.FindByEmailAsync(userEmail);

            var lockedOutTime = user.LockoutEnd.Value.DateTime;

            var hours = (int)(lockedOutTime - dateTimeProvider.Now).TotalHours;

            var model = new LockedOutViewModel(hours);

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Deleted(string userEmail)
        {
            var user = await this.userManager.FindByEmailAsync(userEmail);

            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            Wallet wallet = await walletService.CreateUserWallet(model.UserCurrency);

            DateTime.TryParseExact(model.DateOfBirth,
                       "dd.MM.yyyy",
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.None,
                       out DateTime birthDate);

            User user = new User
            {
                Cards = new List<CreditCard>(),
                Transactions = new List<Transaction>(),
                Email = model.Email,
                UserName = model.Email,
                CreatedOn = dateTimeProvider.Now,
                FirstName = model.FirstName,
                LastName = model.LastName,
                DateOfBirth = birthDate,
                Wallet = wallet,
                WalletId = wallet.Id,
                Role = UserRole.None,
            };

            var result = await userManager.CreateAsync(user, model.Password);
           
            if (result.Succeeded)
            {
                await signInManager.SignInAsync(user, isPersistent: false);

                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
            else
            {
                AddErrors(result);

                ViewData["UserExists"] = "User with that email is already registered!";

                return View(model);
                //throw new InvalidOperationException("Could not register user!");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();

            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            // Request a redirect to the external login provider.

            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });

            var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);

            return Challenge(properties, provider);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToAction(nameof(Login));
            }
            var info = await signInManager.GetExternalLoginInfoAsync();

            if (info == null)
            {
                return RedirectToAction(nameof(Login));
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            var email = info.Principal.Claims.ToList()[1].Value;
            if (result.Succeeded)
            {
                return RedirectToLocal(returnUrl);
            }

            else if (result.IsLockedOut)
            {
                return RedirectToAction(nameof(Lockout), new { userEmail = email});
            }

            else
            {
                // If the user does not have an account, then ask the user to create an account.
                ViewData["ReturnUrl"] = returnUrl;
                ViewData["LoginProvider"] = info.LoginProvider;

                return View("ExternalLogin");
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExternalLoginConfirmation(ExternalLoginViewModel model, string returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(nameof(ExternalLogin));
            }
            // Get the information about the user from the external login provider
            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                throw new ApplicationException("Error loading external login information during confirmation.");
            }

            Wallet wallet = await walletService.CreateUserWallet(model.UserCurrency);

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var names = info.Principal.FindFirstValue(ClaimTypes.Name).Split().ToArray();

            DateTime.TryParseExact(model.DateOfBirth,
                       "dd.MM.yyyy",
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.None,
                       out DateTime birthDate);

            User user = new User
            {
                UserName = email,
                Email = email,
                CreatedOn = dateTimeProvider.Now,
                FirstName = names[0],
                LastName = names[1],
                DateOfBirth = birthDate,
                Wallet = wallet,
                WalletId = wallet.Id,
                Role = UserRole.None
            };

            var result = await userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                result = await userManager.AddLoginAsync(user, info);
                if (result.Succeeded)
                {
                    await signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToLocal(returnUrl);
                }
            }
            AddErrors(result);

            ViewData["ReturnUrl"] = returnUrl;
            return View(nameof(ExternalLogin), model);
        }
        
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
        }

        [AcceptVerbs("Get", "Post")]
        [AllowAnonymous]
        public IActionResult IsBirthDateValid(string DateOfBirth)
        {
            var isValidDate = DateTime.TryParseExact(DateOfBirth,
                       "dd.MM.yyyy",
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.None,
                       out DateTime birthDate);

            var now = dateTimeProvider.Now.Year;

            if (!isValidDate)
            {
                return Json(false);
            }
            else
            {
                int difference = now - birthDate.Year;

                if (difference < 18 || difference > 100)
                {//You cannot be under 18 years old or over 100 years
                    return Json(false);
                }

                return Json(true);
            }
        }
    }
}
