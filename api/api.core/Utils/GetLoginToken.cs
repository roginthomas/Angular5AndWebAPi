﻿using api.core.Models;
using api.core.Providers;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace api.core.Utils
{
    public class GetLoginToken
    {
        public static TokenProviderOptions GetOptions()
        {          
            var signingKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Configuration.Config.GetSection("TokenAuthentication:SecretKey").Value));

            return new TokenProviderOptions
            {
                Path = Configuration.Config.GetSection("TokenAuthentication:TokenPath").Value,
                Audience = Configuration.Config.GetSection("TokenAuthentication:Audience").Value,
                Issuer = Configuration.Config.GetSection("TokenAuthentication:Issuer").Value,
                SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
            };
        }

        public static LoginResponseData Execute(ApplicationUser user, ApiDbContext db, RefreshToken refreshToken)
        {
            var options = GetOptions();
            var now = DateTime.UtcNow;

            var claims = new List<Claim>()
            {
                new Claim(JwtRegisteredClaimNames.NameId, user.Id),
                //new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUniversalTime().ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),       
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
            };

            foreach (var userClaim in user.Claims)
            {
                claims.Add(new Claim(userClaim.ClaimType, userClaim.ClaimValue));
            }

            if (refreshToken == null)
            {
                refreshToken = new RefreshToken()
                {
                    UserId = user.Id,
                    Token = Guid.NewGuid().ToString("N"),
                };
                db.InsertNew(refreshToken);
            }

            refreshToken.IssuedUtc = now;
            refreshToken.ExpiresUtc = now.Add(options.Expiration);
            db.SaveChanges();

            var jwt = new JwtSecurityToken(
                issuer: options.Issuer,
                audience: options.Audience,
                claims: claims.ToArray(),
                notBefore: now,
                expires: now.Add(options.Expiration),
                signingCredentials: options.SigningCredentials);
            var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

            var response = new LoginResponseData
            {
                access_token = encodedJwt,
                refresh_token = refreshToken.Token,
                expires_in = (int)options.Expiration.TotalSeconds,
                userName = user.UserName,
                firstName = user.FirstName,
                lastName = user.LastName,
                isAdmin = user.Claims.Any(i => i.ClaimType == Extensions.AdminClaim)
            };
            return response;
        }
    }

}
