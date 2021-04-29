using Fan.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Fan.Membership
{
    public class UserService : IUserService
    {
        private readonly UserManager<User> _userManager;
        private readonly ILogger<UserService> _logger;
        public UserService(UserManager<User> userManager, ILogger<UserService> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Find user by either email or username.  Returns null if not found.
        /// </summary>
        /// <param name="emailOrUsername"></param>
        /// <returns></returns>
        public async Task<User> FindByEmailOrUsernameAsync(string emailOrUsername)
        {
            bool isEmail = Util.IsValidEmail(emailOrUsername);
            return isEmail ? await _userManager.FindByEmailAsync(emailOrUsername) :
                await _userManager.FindByNameAsync(emailOrUsername);
        }

        public async Task<bool> RegisterUser(string username, string email, string name, string password)
        {
            var user = new User()
            {
                UserName = username,
                Email = email,
                DisplayName = name,
            };

            if ((await _userManager.CreateAsync(user, password)).Succeeded)
            {
                var newUser = await _userManager.FindByEmailAsync(user.Email);
                if (user != null)
                {
                    var result = await _userManager.AddToRoleAsync(newUser, Role.USER_ROLE);
                    if (result.Succeeded)
                        return true;
                }
            }

            return false;
        }
    }
}