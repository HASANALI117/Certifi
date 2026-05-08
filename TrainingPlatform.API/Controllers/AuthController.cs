using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TrainingPlatform.API.DTOs;
using TrainingPlatform.API.Models;
using TrainingPlatform.API.Services;

namespace TrainingPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        TokenService tokenService) : ControllerBase
    {
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequestDto request)
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null) return Unauthorized("Invalid credentials");

            var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
            if (!result.Succeeded) return Unauthorized("Invalid credentials");

            var roles = await userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? string.Empty;
            var token = tokenService.GenerateToken(user, role);

            return Ok(new LoginResponseDto
            {
                Token = token,
                Email = user.Email!,
                FullName = $"{user.FirstName} {user.LastName}",
                Role = role,
            });
        }
    }
}