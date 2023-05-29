﻿using System;
using System.Data;
using easyNetAPI.Data;
using easyNetAPI.Data.Repository.IRepository;
using easyNetAPI.Models;
using easyNetAPI.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

namespace easyNetAPI.Controllers
{
	[ApiController]
    [Route("[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<UserController> _logger;
        private readonly AppDbContext _db;
        private readonly IUnitOfWork _unitOfWork;

        public UserController(UserManager<IdentityUser> userManager, ILogger<UserController> logger, AppDbContext db, IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _db = db;
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        [HttpPost("Follow"), Authorize(Roles = $"{SD.ROLE_EMPLOYEE},{SD.ROLE_COMPANY_ADMIN},{SD.ROLE_USER}")]
        public async Task<ActionResult<string>> FollowUserAsync(string userName)
        {
            var token = Request.Headers["Authorization"].ToString();
            var userId = await AuthControllerUtility.GetUserIdFromTokenAsync(token);
            if (userId is null)
            {
                return BadRequest("User not found");
            }

            var managedUser = _db.Users.Where(u => u.Id == userId).FirstOrDefault();
            var userToFollow = _db.Users.Where(u => u.UserName == userName).FirstOrDefault();
            if (userToFollow is null)
            {
                return BadRequest("User not found");
            }

            var managedUserBehavior = await _unitOfWork.UserBehavior.GetFirstOrDefault(managedUser.Id);
            if (managedUserBehavior is null)
            {
                return BadRequest("User not found");
            }

            var followedUserBehavior = await _unitOfWork.UserBehavior.GetFirstOrDefault(userToFollow.Id);
            if (followedUserBehavior is null)
            {
                return BadRequest("User not found");
            }

            managedUserBehavior.FollowedUsers.Add(userToFollow.Id);
            await _unitOfWork.UserBehavior.UpdateOneAsync(managedUserBehavior.UserId, managedUserBehavior);

            followedUserBehavior.FollowersList.Add(managedUser.Id);
            await _unitOfWork.UserBehavior.UpdateOneAsync(followedUserBehavior.UserId, followedUserBehavior);
            return Ok("User followed successfully");    
        }

        [HttpPost("Unfollow")]
        [Authorize(Roles = $"{SD.ROLE_EMPLOYEE},{SD.ROLE_COMPANY_ADMIN},{SD.ROLE_USER}")]
        public async Task<ActionResult<string>> UnfollowUserAsync(string userName)
        {
            var token = Request.Headers["Authorization"].ToString();
            var userId = await AuthControllerUtility.GetUserIdFromTokenAsync(token);
            if (userId is null)
            {
                return BadRequest("User not found");
            }

            var managedUser = _db.Users.Where(u => u.Id == userId).FirstOrDefault();
            var userToFollow = _db.Users.Where(u => u.UserName == userName).FirstOrDefault();
            if (userToFollow is null)
            {
                return BadRequest("User not found");
            }

            var managedUserBehavior = await _unitOfWork.UserBehavior.GetFirstOrDefault(managedUser.Id);
            if (managedUserBehavior is null)
            {
                return BadRequest("User not found");
            }

            var followedUserBehavior = await _unitOfWork.UserBehavior.GetFirstOrDefault(userToFollow.Id);
            if (followedUserBehavior is null)
            {
                return BadRequest("User not found");
            }

            if (managedUserBehavior.FollowedUsers.Count() == 0)
            {
                if (!managedUserBehavior.FollowedUsers.Contains(userToFollow.Id))
                {
                    return BadRequest("Cannot unfollow user");
                }
                return BadRequest("Cannot unfollow user");
            }

            if (followedUserBehavior.FollowersList.Count() == 0)
            {
                if (!followedUserBehavior.FollowersList.Contains(managedUser.Id))
                {
                    return BadRequest("Cannot unfollow user");
                }
                return BadRequest("Cannot unfollow user");
            }

            managedUserBehavior.FollowedUsers.Remove(userToFollow.Id);
            await _unitOfWork.UserBehavior.UpdateOneAsync(managedUserBehavior.UserId, managedUserBehavior);

            followedUserBehavior.FollowersList.Remove(managedUser.Id);
            await _unitOfWork.UserBehavior.UpdateOneAsync(followedUserBehavior.UserId, followedUserBehavior);

            return Ok("User unfollowed successfully");
        }

        //prende i follower di un utente specificato
        [HttpGet("GetUserFollowers")]
        [Authorize(Roles = $"{SD.ROLE_EMPLOYEE},{SD.ROLE_COMPANY_ADMIN},{SD.ROLE_USER},{SD.ROLE_MODERATOR}")]
        public async Task<List<string>?> GetUserFollowers(string userName)
        {
            var token = Request.Headers["Authorization"].ToString();
            var userId = await AuthControllerUtility.GetUserIdFromTokenAsync(token);
            if (userId is null)
            {
                return null;
            }

            var userToSearch = _db.Users.Where(u => u.UserName == userName).FirstOrDefault();
            if (userToSearch is null)
            {
                return null;
            }

            var userBehavior = await _unitOfWork.UserBehavior.GetFirstOrDefault(userToSearch.Id);

            var returnList = new List<string>(); 

            if (userBehavior.FollowersList.Count() != 0)
            {
                foreach (var user in userBehavior.FollowersList)
                {
                    returnList.Add(
                        _db.Users.Where(u=> u.Id == user).Select(u => u.UserName).FirstOrDefault()
                        );
                }
            }

            return returnList;
        }

        //prende i follower dell'utente che ha fatto la richiesta
        [HttpGet("GetFollowers")]
        [Authorize(Roles = $"{SD.ROLE_EMPLOYEE},{SD.ROLE_COMPANY_ADMIN},{SD.ROLE_USER}")]
        public async Task<List<string>?> GetUserFollowers()
        {
            var token = Request.Headers["Authorization"].ToString();
            var userId = await AuthControllerUtility.GetUserIdFromTokenAsync(token);
            if (userId is null)
            {
                return null;
            }

            var userBehavior = await _unitOfWork.UserBehavior.GetFirstOrDefault(userId);

            var returnList = new List<string>();

            if (userBehavior.FollowersList.Count() != 0)
            {
                foreach (var user in userBehavior.FollowersList)
                {
                    returnList.Add(
                        _db.Users.Where(u => u.Id == user).Select(u => u.UserName).FirstOrDefault()
                        );
                }
            }

            return returnList;
        }


        //prende i follower di un utente specificato
        [HttpGet("GetUserFollowedList")]
        [Authorize(Roles = $"{SD.ROLE_EMPLOYEE},{SD.ROLE_COMPANY_ADMIN},{SD.ROLE_USER},{SD.ROLE_MODERATOR}")]
        public async Task<List<string>?> GetUserFollowedList(string userName)
        {
            var token = Request.Headers["Authorization"].ToString();
            var userId = await AuthControllerUtility.GetUserIdFromTokenAsync(token);
            if (userId is null)
            {
                return null;
            }

            var userToSearch = _db.Users.Where(u => u.UserName == userName).FirstOrDefault();
            if (userToSearch is null)
            {
                return null;
            }

            var userBehavior = await _unitOfWork.UserBehavior.GetFirstOrDefault(userToSearch.Id);

            var returnList = new List<string>();

            if (userBehavior.FollowedUsers.Count() != 0)
            {
                foreach (var user in userBehavior.FollowedUsers)
                {
                    returnList.Add(
                        _db.Users.Where(u => u.Id == user).Select(u => u.UserName).FirstOrDefault()
                        );
                }
            }

            return returnList;
        }

        //prende i followed dell'utente che ha fatto la richiesta
        [HttpGet("GetFollowed"), Authorize(Roles = SD.ROLE_USER)]
        [Authorize(Roles = $"{SD.ROLE_EMPLOYEE},{SD.ROLE_COMPANY_ADMIN},{SD.ROLE_USER}")]
        public async Task<List<string>?> GetUserFollowed()
        {
            var token = Request.Headers["Authorization"].ToString();
            var userId = await AuthControllerUtility.GetUserIdFromTokenAsync(token);
            if (userId is null)
            {
                return null;
            }

            var userBehavior = await _unitOfWork.UserBehavior.GetFirstOrDefault(userId);

            var returnList = new List<string>();

            if (userBehavior.FollowedUsers.Count() != 0)
            {
                foreach (var user in userBehavior.FollowedUsers)
                {
                    returnList.Add(
                        _db.Users.Where(u => u.Id == user).Select(u => u.UserName).FirstOrDefault()
                        );
                }
            }

            return returnList;
        }

        [HttpPost("ConvertToCompanyAdminModerator"), Authorize(Roles = SD.ROLE_MODERATOR)]
        public async Task<ActionResult<string>> ConvertToAdmin(string username)
        {
            if (username.IsNullOrEmpty())
                return BadRequest("Insert username");
            var userId = await AuthControllerUtility.GetUserIdFromUsername(username, _db);
            if (userId is null || userId.Equals(string.Empty))
                return BadRequest("UserId not found");

            var user = _db.Users.Find(userId);
            if (user is null)
                return BadRequest("User not found");

            var result = await _userManager.AddToRoleAsync(user, SD.ROLE_MODERATOR);
            if (result.Succeeded)
                return Ok("User is now admin");

            return BadRequest("Could not make user admin see exception: " + result.Errors);
        }

        [HttpPost("ConvertToModerator"), Authorize(Roles = SD.ROLE_MODERATOR)]
        public async Task<ActionResult<string>> ConvertToModerator(string username)
        {
            if (username.IsNullOrEmpty())
                return BadRequest("Insert username");
            var userId = await AuthControllerUtility.GetUserIdFromUsername(username, _db);
            if (userId is null || userId.Equals(string.Empty))
                return BadRequest("UserId not found");

            var user = _db.Users.Find(userId);
            if (user is null)
                return BadRequest("User not found");

            var result = await _userManager.AddToRoleAsync(user, SD.ROLE_MODERATOR);
            if (result.Succeeded)
                return Ok("User is now admin");

            return BadRequest("Could not make user moderator see exception: " + result.Errors);
        }

        [HttpPost("ConvertToEmployeeCompanyAdmin"), Authorize(Roles = SD.ROLE_COMPANY_ADMIN)]
        public async Task<ActionResult<string>> ConvertToEmployee(string username)
        {
            if (username.IsNullOrEmpty())
                return BadRequest("Insert username");
            var userId = await AuthControllerUtility.GetUserIdFromUsername(username, _db);
            if (userId is null || userId.Equals(string.Empty))
                return BadRequest("UserId not found");

            var user = _db.Users.Find(userId);
            if (user is null)
                return BadRequest("User not found");

            var result = await _userManager.AddToRoleAsync(user, SD.ROLE_EMPLOYEE);
            if (result.Succeeded)
                return Ok("User is now employee");

            return BadRequest("Could not make user employee see exception: " + result.Errors);
        }

        [HttpPost("RemoveCompanyAdminModerator"), Authorize(Roles = SD.ROLE_MODERATOR)]
        public async Task<ActionResult<string>> RemoveFromAdmin(string username)
        {
            if (username.IsNullOrEmpty())
                return BadRequest("Insert username");
            var userId = await AuthControllerUtility.GetUserIdFromUsername(username, _db);
            if (userId is null || userId.Equals(string.Empty))
                return BadRequest("UserId not found");

            var user = _db.Users.Find(userId);
            if (user is null)
                return BadRequest("User not found");
            if (!_userManager.GetUsersInRoleAsync(SD.ROLE_COMPANY_ADMIN).Result.Contains(user))
                return BadRequest("User is not admin");
            var result = await _userManager.RemoveFromRoleAsync(user, SD.ROLE_COMPANY_ADMIN);

            if (result.Succeeded)
                return Ok("User has been removed from role admin");

            return BadRequest("Could not remove user from role admin see exception: " + result.Errors);
        }

        [HttpPost("RemoveFromModerator"), Authorize(Roles = SD.ROLE_MODERATOR)]
        public async Task<ActionResult<string>> RemoveFromModerator(string username)
        {
            if (username.IsNullOrEmpty())
                return BadRequest("Insert username");
            var userId = await AuthControllerUtility.GetUserIdFromUsername(username, _db);
            if (userId is null || userId.Equals(string.Empty))
                return BadRequest("UserId not found");

            var user = _db.Users.Find(userId);
            if (user is null)
                return BadRequest("User not found");
            if (!_userManager.GetUsersInRoleAsync(SD.ROLE_MODERATOR).Result.Contains(user))
                return BadRequest("User is not moderator");
            var result = await _userManager.RemoveFromRoleAsync(user, SD.ROLE_MODERATOR);

            if (result.Succeeded)
                return Ok("User has been removed from role moderator");

            return BadRequest("Could not remove user from role moderator see exception: " + result.Errors);
        }

        [HttpPost("RemoveFromEmployeeCompanyAdmin"), Authorize(Roles = SD.ROLE_COMPANY_ADMIN)]
        public async Task<ActionResult<string>> RemoveFromEmployee(string username)
        {
            if (username.IsNullOrEmpty())
                return BadRequest("Insert username");
            var userId = await AuthControllerUtility.GetUserIdFromUsername(username, _db);
            if (userId is null || userId.Equals(string.Empty))
                return BadRequest("UserId not found");

            var user = _db.Users.Find(userId);
            if (user is null)
                return BadRequest("User not found");
            if (!_userManager.GetUsersInRoleAsync(SD.ROLE_EMPLOYEE).Result.Contains(user))
                return BadRequest("User is not employee");
            var result = await _userManager.RemoveFromRoleAsync(user, SD.ROLE_EMPLOYEE);

            if (result.Succeeded)
                return Ok("User has been removed from role employee");

            return BadRequest("Could not remove user from role employee see exception: " + result.Errors);
        }
    }
}