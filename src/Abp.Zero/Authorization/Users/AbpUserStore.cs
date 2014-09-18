﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization.Roles;
using Abp.Authorization.Users.Logins;
using Abp.Authorization.Users.Roles;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using Microsoft.AspNet.Identity;

namespace Abp.Authorization.Users
{
    public class AbpUserStore :
        IUserPasswordStore<AbpUser, long>,
        IUserEmailStore<AbpUser, long>,
        IUserLoginStore<AbpUser, long>,
        IUserRoleStore<AbpUser, long>,
        IQueryableUserStore<AbpUser, long>,
        ITransientDependency
    {
        #region Private fields

        private readonly IAbpUserRepository _userRepository;
        private readonly IUserLoginRepository _userLoginRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IAbpRoleRepository _abpRoleRepository;
        private readonly IAbpSession _session;

        #endregion

        #region Constructor

        public AbpUserStore(
            IAbpUserRepository userRepository,
            IUserLoginRepository userLoginRepository,
            IUserRoleRepository userRoleRepository,
            IAbpRoleRepository abpRoleRepository,
            IAbpSession session)
        {
            _userRepository = userRepository;
            _userLoginRepository = userLoginRepository;
            _userRoleRepository = userRoleRepository;
            _abpRoleRepository = abpRoleRepository;
            _session = session;
        }

        #endregion

        #region IUserStore

        public void Dispose()
        {
            //No need to dispose since using dependency injection manager
        }

        public Task CreateAsync(AbpUser user)
        {
            return Task.Factory.StartNew(() => _userRepository.Insert(user));
        }

        public Task UpdateAsync(AbpUser user)
        {
            return Task.Factory.StartNew(() => _userRepository.Update(user));
        }

        public Task DeleteAsync(AbpUser user)
        {
            return Task.Factory.StartNew(() => _userRepository.Delete(user.Id));
        }

        public Task<AbpUser> FindByIdAsync(long userId)
        {
            return Task.Factory.StartNew(() => _userRepository.FirstOrDefault(userId));
        }

        public Task<AbpUser> FindByNameAsync(string userName)
        {
            return Task.Factory.StartNew(() => _userRepository.FirstOrDefault(user => user.TenantId == _session.TenantId && (user.UserName == userName || user.EmailAddress == userName) && user.IsEmailConfirmed));
        }

        #endregion

        #region IUserPasswordStore

        public Task SetPasswordHashAsync(AbpUser user, string passwordHash)
        {
            return Task.Factory.StartNew(() => _userRepository.UpdatePassword(user.Id, passwordHash));
        }

        public Task<string> GetPasswordHashAsync(AbpUser user)
        {
            return Task.Factory.StartNew(() => _userRepository.Get(user.Id).Password); //TODO: Optimize
        }

        public Task<bool> HasPasswordAsync(AbpUser user)
        {
            return Task.Factory.StartNew(() => !string.IsNullOrEmpty(_userRepository.Get(user.Id).Password)); //TODO: Optimize
        }

        #endregion

        #region IUserEmailStore

        public Task SetEmailAsync(AbpUser user, string email)
        {
            return Task.Factory.StartNew(() => _userRepository.UpdateEmail(user.Id, email));
        }

        public Task<string> GetEmailAsync(AbpUser user)
        {
            return Task.Factory.StartNew(() => _userRepository.Get(user.Id).EmailAddress);
        }

        public Task<bool> GetEmailConfirmedAsync(AbpUser user)
        {
            return Task.Factory.StartNew(() => _userRepository.Get(user.Id).IsEmailConfirmed); //TODO: Optimize?
        }

        public Task SetEmailConfirmedAsync(AbpUser user, bool confirmed)
        {
            return Task.Factory.StartNew(() => _userRepository.UpdateIsEmailConfirmed(user.Id, confirmed));
        }

        public Task<AbpUser> FindByEmailAsync(string email)
        {
            return Task.Factory.StartNew(() => _userRepository.FirstOrDefault(user => user.EmailAddress == email));
        }

        #endregion

        #region IUserLoginStore

        public Task AddLoginAsync(AbpUser user, UserLoginInfo login)
        {
            //TODO: Check if already exists?
            return Task.Factory.StartNew(
                () =>
                    _userLoginRepository.Insert(
                        new UserLogin
                        {
                            LoginProvider = login.LoginProvider,
                            ProviderKey = login.ProviderKey,
                            UserId = user.Id
                        })
                );
        }

        public Task RemoveLoginAsync(AbpUser user, UserLoginInfo login)
        {
            throw new NotImplementedException(); //TODO: Implement!
        }

        public Task<IList<UserLoginInfo>> GetLoginsAsync(AbpUser user)
        {
            return Task.Factory.StartNew<IList<UserLoginInfo>>(
                () =>
                    _userLoginRepository
                        .GetAllList(ul => ul.UserId == user.Id)
                        .Select(ul => new UserLoginInfo(ul.LoginProvider, ul.ProviderKey))
                        .ToList()
                );
        }

        public Task<AbpUser> FindAsync(UserLoginInfo login)
        {
            return Task.Factory.StartNew(
                () => FindUser(login.LoginProvider, login.ProviderKey)
                );
        }

        [UnitOfWork]
        protected virtual AbpUser FindUser(string loginProvider, string providerKey)
        {
            var query =
                from user in _userRepository.GetAll()
                join userLogin in _userLoginRepository.GetAll() on user.Id equals userLogin.UserId
                where userLogin.LoginProvider == loginProvider && userLogin.ProviderKey == providerKey
                select user;
            return query.FirstOrDefault();
        }

        #endregion

        #region IUserRoleStore

        public Task AddToRoleAsync(AbpUser user, string roleName)
        {
            //TODO: Check if already exists?
            return Task.Factory.StartNew(
                () =>
                    _userRoleRepository.Insert(
                        new UserRole
                        {
                            User = user,
                            Role = _abpRoleRepository.Single(role => role.Name == roleName) //TODO: Can find another way?
                        })
                );
        }

        public Task RemoveFromRoleAsync(AbpUser user, string roleName)
        {
            return Task.Factory.StartNew(
                () =>
                {
                    var userRole = _userRoleRepository.FirstOrDefault(
                        ur => ur.User.Id == user.Id && ur.Role.Name == roleName
                        );

                    if (userRole == null)
                    {
                        return;
                    }

                    _userRoleRepository.Delete(userRole.Id);
                });
        }

        public Task<IList<string>> GetRolesAsync(AbpUser user)
        {
            return Task.Factory.StartNew<IList<string>>(
                () =>
                    _userRoleRepository
                        .Query(q => q
                            .Where(ur => ur.User.Id == user.Id)
                            .Select(ur => ur.Role.Name)
                            .ToList()
                        )
                );
        }

        public Task<bool> IsInRoleAsync(AbpUser user, string roleName)
        {
            return Task.Factory.StartNew(
                () => _userRoleRepository.FirstOrDefault(
                    ur => ur.User.Id == user.Id && ur.Role.Name == roleName
                    ) != null
                );
        }

        #endregion

        #region IQueryableUserStore

        public IQueryable<AbpUser> Users
        {
            get { return _userRepository.GetAll(); }
        }

        #endregion
    }
}
