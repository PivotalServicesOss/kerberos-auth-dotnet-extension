using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Kerberos.Dotnet.Application;

[ApiController]
[Route("[controller]")]
[Authorize]
public class SecuredController : ControllerBase
{
    [HttpGet("currentuser")]
    public IActionResult GetCurrentUserName()
    {
        return Ok(User.Identity.Name);
    }
}
