using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using TrainingPlatform.API.Models;

namespace TrainingPlatform.API.Services
{
    public class TokenService(IConfiguration config)
    {
        public string GenerateToken(AppUser user, string role)
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(config["JWT:Key"]!)
            );

            var claims = new List<Claim>
            {
              new(ClaimTypes.NameIdentifier, user.Id),
              new(ClaimTypes.Email, user.Email!),
              new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
              new(ClaimTypes.Role, role),
            };

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: config["JWT:Issuer"],
                audience: config["JWT:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}