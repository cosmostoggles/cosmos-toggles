﻿using AutoMapper;
using Azure.Cosmos;
using Cosmos.Toggles.Application.Service.Interfaces;
using Cosmos.Toggles.Domain.DataTransferObject;
using Cosmos.Toggles.Domain.Entities.Interfaces;
using Cosmos.Toggles.Domain.Service.Interfaces;
using FluentValidation;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Cosmos.Toggles.Application.Service
{
    public class UserAppService : IUserAppService
    {
        private readonly IMapper _mapper;
        private readonly ICosmosToggleDataContext _cosmosToggleDataContext;
        private readonly INotificationContext _notificationContext;
        private readonly IValidator<User> _userValidator;

        public UserAppService(IMapper mapper, ICosmosToggleDataContext cosmosToggleDataContext, INotificationContext notificationContext,
            IValidator<User> userValidator, IAuthAppService authAppService)
        {
            _mapper = mapper;
            _cosmosToggleDataContext = cosmosToggleDataContext;
            _notificationContext = notificationContext;
            _userValidator = userValidator;
        }

        private async Task CreatePasswordAsync(string email, string password, string activationCode, string activationKey)
        {
            var user = await _cosmosToggleDataContext.UserRepository.GetByEmailAsync(email);

            if (user != null)
            {
                user.Password = password;
                await _cosmosToggleDataContext.UserRepository.UpdateAsync(user, new PartitionKey(user.Id));
            }
            else
                await _notificationContext.AddAsync(HttpStatusCode.Conflict, "User already exists");
        }

        public async Task AddProjectAsync(string userId, string projectId)
        {
            var user = await this.GetById(userId);
            await AddProjectAsync(user, projectId);
        }

        public async Task AddProjectAsync(User user, string projectId)
        {
            if (user != null)
            {
                if (user.Projects == null || user.Projects.Count() == 0)
                {
                    user.Projects = new List<string> { };
                }

                if (!user.Projects.Contains(projectId))
                {
                    user.Projects.ToList().Add(projectId);
                    await _cosmosToggleDataContext.UserRepository.UpdateAsync(_mapper.Map<Domain.Entities.User>(user), new PartitionKey(user.Id));
                }
            }
        }

        public async Task CreateAsync(User user)
        {
            _userValidator.ValidateAndThrow(user, ruleSet: "create");
            var currentUser = await _cosmosToggleDataContext.UserRepository.GetByEmailAsync(user.Email);

            if (currentUser == null)
            {
                var entity = _mapper.Map<Domain.Entities.User>(user);
                await _cosmosToggleDataContext.UserRepository.AddAsync(entity, new PartitionKey(entity.Id));
            }
            else
                await _notificationContext.AddAsync(HttpStatusCode.Conflict, "User already exists");
        }

        public async Task<User> GetById(string userId)
        {
            var entity = await _cosmosToggleDataContext.UserRepository.GetByIdAsync(userId, new PartitionKey(userId));

            if (entity == null)
            {
                await _notificationContext.AddAsync(HttpStatusCode.NotFound, "User not found");
                return null;
            }

            return _mapper.Map<User>(entity);
        }

    }
}
