using FeedHorn.Services;
using Microsoft.AspNetCore.Mvc;

namespace FeedHorn.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _authService.ValidateCredentials(request.Username, request.Password);
        if (user == null)
        {
            return Unauthorized(new { message = "Invalid credentials" });
        }

        // Set session cookie
        Response.Cookies.Append("FeedHorn_UserId", user.Id.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(7)
        });

        return Ok(new
        {
            username = user.Username,
            mustChangePassword = user.MustChangePassword
        });
    }

    [HttpPost("logout")]
    public ActionResult Logout()
    {
        Response.Cookies.Delete("FeedHorn_UserId");
        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("change-password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        // Get userId from cookie
        if (!Request.Cookies.TryGetValue("FeedHorn_UserId", out var userIdStr) ||
            !int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var success = await _authService.ChangePassword(userId, request.NewPassword);
        if (!success)
        {
            return BadRequest(new { message = "Failed to change password" });
        }

        return Ok(new { message = "Password changed successfully" });
    }

    [HttpGet("status")]
    public async Task<ActionResult> GetStatus()
    {
        if (!Request.Cookies.TryGetValue("FeedHorn_UserId", out var userIdStr) ||
            !int.TryParse(userIdStr, out var userId))
        {
            return Ok(new { authenticated = false });
        }

        var user = await _authService.GetUserById(userId);
        if (user == null)
        {
            Response.Cookies.Delete("FeedHorn_UserId");
            return Ok(new { authenticated = false });
        }

        return Ok(new
        {
            authenticated = true,
            username = user.Username,
            mustChangePassword = user.MustChangePassword
        });
    }
}

public class LoginRequest
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}

public class ChangePasswordRequest
{
    public required string NewPassword { get; set; }
}
