﻿using Cosmos.Toggles.Domain.DataTransferObject;
using System.Threading.Tasks;

namespace Cosmos.Toggles.Application.Service.Interfaces
{
    public interface IUserAppService
    {
        Task AddProjectAsync(string userId, string projectId);
        Task AddProjectAsync(User user, string projectId);
        Task CreateAsync(User user);
        Task<User> GetById(string userId);
    }
}
