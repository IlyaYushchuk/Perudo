using Microsoft.AspNetCore.Identity;

namespace Server.Services;

public interface ITokenService
{
    public string GetToken(IdentityUser user);
}
