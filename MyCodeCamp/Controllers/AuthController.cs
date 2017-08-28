using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyCodeCamp.Data;
using MyCodeCamp.Data.Entities;
using MyCodeCamp.Filters;
using MyCodeCamp.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Converters;

namespace MyCodeCamp.Controllers
{
    [Route("api/[controller]")]
    public class AuthController : Controller
    {
        private SignInManager<CampUser> _signInManager;
        private CampContext _context;
        private ILogger<AuthController> _logger;
        private UserManager<CampUser> _userManager;
        private IPasswordHasher<CampUser> _hasher;
        private IConfigurationRoot _config;

        public AuthController(CampContext context, SignInManager<CampUser> signInManager,
            ILogger<AuthController> logger, UserManager<CampUser> userManager, IPasswordHasher<CampUser> hasher,
            IConfigurationRoot config)
        {
            _context = context;
            _signInManager = signInManager;
            _logger = logger;
            _userManager = userManager;
            _hasher = hasher;
            _config = config;
        }

        [HttpPost("login")]
        [ValidateModel]
        public async Task<IActionResult> Login([FromBody] CredentialModel model)
        {
            try
            {
                // signs the user in and leaves a cookie
                var result = await _signInManager.PasswordSignInAsync(
                    model.UserName, model.Password,
                    false, // do not persist that cooke in a browser after it is closed
                    false); // do not logout on failure
                if (result.Succeeded)
                {
                    return Ok();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception thrown while logging in: {ex}");
            }

            return BadRequest();
        }

        [ValidateModel]
        [HttpPost("token")]
        public async Task<IActionResult> CreateToken([FromBody] CredentialModel model)
        {
            try
            {
                // Validate credentials
                var user = await _userManager.FindByNameAsync(model.UserName);
                if (user != null)
                {
                    // verify the hash password
                    if (_hasher.VerifyHashedPassword(user, user.PasswordHash, model.Password) ==
                        PasswordVerificationResult.Success)
                    {
                        // Correct

                        // Get claims provided by Identity (see CampIdentityInitializer.cs)
                        var userClaims = await _userManager.GetClaimsAsync(user);

                        // Our JWT payload
                        var claims = new[]
                        {
                            new Claim(JwtRegisteredClaimNames.Sub, user.UserName), // subject of the token
                            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // unique identifier
                            new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName),
                            new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName),
                            new Claim(JwtRegisteredClaimNames.Email, user.Email)
                        }.Union(userClaims);

                        // Used to generate JWT Signature
                        var key =
                            new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(_config["Tokens:Key"])); // keep this key super-secret
                        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                        // Create the token
                        var token = new JwtSecurityToken(
                            issuer: _config["Tokens:Issuer"], // iss claim (issuer of the token)
                            audience: _config[
                                "Tokens:Audience"], // aud claim, audience that are recipients that JWT is intended for
                            claims: claims,
                            expires: DateTime.UtcNow.AddMinutes(15), // JWT token expiration date
                            signingCredentials: creds // signing credentials that are used to sign this token
                        );

                        return Ok(new
                        {
                            token = new JwtSecurityTokenHandler().WriteToken(token),
                            expiration = token.ValidTo
                        });
                    }
                    else
                    {
                        // Wrong password
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception thrown while generating token: {ex}");
            }

            return BadRequest("Failed to generate token");
        }
    }
}