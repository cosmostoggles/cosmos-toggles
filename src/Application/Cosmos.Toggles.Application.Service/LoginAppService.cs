﻿using AutoMapper;
using Azure.Cosmos;
using Cosmos.Toggles.Application.Service.Interfaces;
using Cosmos.Toggles.Domain.DataTransferObject;
using Cosmos.Toggles.Domain.Entities.Interfaces;
using Cosmos.Toggles.Domain.Service.Interfaces;
using FluentValidation;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Cosmos.Toggles.Application.Service
{
    public class LoginAppService : ILoginAppService
    {
        const int EXPIRES = 1800;

        private readonly IMapper _mapper;
        private readonly ICosmosToggleDataContext _cosmosToggleDataContext;
        private readonly INotificationContext _notificationContext;
        private readonly IValidator<Login> _loginValidator;
        private readonly ITokenService _tokenService;

        public LoginAppService(IMapper mapper, ICosmosToggleDataContext cosmosToggleDataContext, INotificationContext notificationContext,
            IValidator<Login> loginValidator, ITokenService tokenService)
        {
            _mapper = mapper;
            _cosmosToggleDataContext = cosmosToggleDataContext;
            _notificationContext = notificationContext;
            _loginValidator = loginValidator;
            _tokenService = tokenService;
        }

        public async Task<Token> LoginAsync(Login login, string ipAddress)
        {
            var userEntity = await _cosmosToggleDataContext.UserRepository.GetByEmailPasswordAsync(login.Email, login.Password);

            if (userEntity == null)
            {
                await _notificationContext.AddAsync(HttpStatusCode.Unauthorized, "Unauthorized", "Incorrect e-mail or password.");
                return null;
            }

            var dateTimeNow = DateTime.UtcNow;

            var result = new RefreshToken
            {
                UserId = userEntity.Id,
                Key = await _tokenService.CreateKeyAsync(),
                Jwt = await _tokenService.CreateJwtAsync(userEntity.Id, userEntity.Name, userEntity.Email, EXPIRES),
                Created = dateTimeNow,
                CreatedIp = ipAddress,
                Expires = dateTimeNow.AddSeconds(EXPIRES)
            };

            var refreshToken = _mapper.Map<Domain.Entities.RefreshToken>(result, opts =>
            {
                opts.Items["UserId"] = userEntity.Id;
            });

            await _cosmosToggleDataContext.RefreshTokenRepository.AddAsync(refreshToken, new PartitionKey(refreshToken.UserId));

            return _mapper.Map<Token>(result);
        }

        public async Task<Token> RefreshAsync(string key, string userId, string ipAddress)
        {
            var refreshTokenEntity = await _cosmosToggleDataContext.RefreshTokenRepository.GetByKeyUserIdAsync(key, userId);

            if (refreshTokenEntity == null)
            {
                await _notificationContext.AddAsync(HttpStatusCode.Unauthorized, "The access token expired", null);
                return null;
            }
            var dateTimeNow = DateTime.UtcNow;

            refreshTokenEntity.Revoked = dateTimeNow;
            refreshTokenEntity.RevokedIp = ipAddress;
            refreshTokenEntity.Ttl = 1;

            _cosmosToggleDataContext.RefreshTokenRepository.TryUpdateAsync(refreshTokenEntity, userId);

            var user = await _tokenService.ExtractUserAsync(refreshTokenEntity.Jwt);

            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                Key = await _tokenService.CreateKeyAsync(),
                Jwt = await _tokenService.CreateJwtAsync(user.Id, user.Name, user.Email, EXPIRES),
                Created = dateTimeNow,
                CreatedIp = ipAddress,
                Expires = DateTime.UtcNow.AddSeconds(EXPIRES)
            };

            await _cosmosToggleDataContext.RefreshTokenRepository.AddAsync(_mapper.Map<Domain.Entities.RefreshToken>(refreshToken),
              new PartitionKey(user.Id));

            return _mapper.Map<Token>(refreshToken);
        }

    }
}
