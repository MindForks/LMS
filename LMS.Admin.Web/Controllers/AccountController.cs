﻿using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Linq;
using LMS.Admin.Web.ViewModels;
using LMS.Identity;
using System;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using LMS.Dto;
using System.Collections.Generic;

namespace LMS.Admin.Web.Controllers
{
    public class AccountController : Controller
    {
        private  IdentityService _identityService;

        public AccountController(IdentityService identityService)
        {
            _identityService = identityService;
        }

        [HttpGet]
        public IActionResult AccessDenied(Uri ReturnUrl)
        {
            return View();
        }

        [HttpGet]
        [Authorize(Roles = "admin")]
        public IActionResult Register()
        {
            ViewData["AllRoles"] = GetRolesListItem();
            return View(_identityService.GetDefaultRegisterModel());
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Register(UserDTO model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _identityService.Register(model);
                    ViewData["AllRoles"] = GetRolesListItem();
                    return View(_identityService.GetDefaultRegisterModel());
                }
                catch (AggregateException e)
                {
                    foreach (Exception ex in e.InnerExceptions)
                        ModelState.AddModelError(string.Empty, ex.Message);
                }
            }
            ViewData["AllRoles"] = GetRolesListItem();
            return View(_identityService.GetDefaultRegisterModel());
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _identityService.LogIn(model.UserName, model.Password, model.RememberMe);

                    if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                        return Redirect(model.ReturnUrl);
                    else
                        return RedirectToAction(nameof(TestSessionController.List), "TestSession");
                }
                catch (Exception e)
                {
                        ModelState.AddModelError(string.Empty, e.Message);
                }
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _identityService.Logout();
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        [Authorize(Roles = "admin, moderator")]
        public async Task<IActionResult> List()
        {
            var users = await _identityService.GetAllUsers();
           
            return View(users);
        }

        [Authorize(Roles = "admin, moderator")]
        public async Task<IActionResult> Edit(string id)
        {
            var userDTO = await _identityService.GetById(id);
            ViewData["AllRoles"] = GetRolesListItem();
            return View(userDTO);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin, moderator")]
        public async Task<IActionResult> Edit(UserDTO model)
        {
            await _identityService.UpdateAsync(model);

            return RedirectToAction(nameof(List));
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _identityService.DeleteUser(id);
            }
            catch (Exception e)
            {
                return RedirectToAction("AccessDenied", "Account");
            }
            return RedirectToAction("List", "Account");
        }

        public IEnumerable<SelectListItem> GetRolesListItem()
        {
            return _identityService.GetAllRoles().Select(t => new SelectListItem() { Value = t.Name, Text = t.Name });
        }
    }
}
