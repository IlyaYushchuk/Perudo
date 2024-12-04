using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.DTO;
using Server.Exceptions;
using Server.Services;
using System.Text.Json;

namespace Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
	private readonly UserManager<IdentityUser> _userManager;
	private readonly ITokenService _tokenService;
	private readonly SignInManager<IdentityUser> _signInManager;

	public UsersController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, ITokenService tokenService)
	{
		_userManager = userManager;
		_tokenService = tokenService;
		_signInManager = signInManager;
	}

	[HttpPost]
	[Route("login")]
	public async Task<IActionResult> Login([FromBody] RegisterDTO userDto)
	{
		var signInResult = await _signInManager.PasswordSignInAsync(userDto.Email, userDto.Password, false, false);

		if (!signInResult.Succeeded)
		{
			throw new BadRequestException("Incorrect email or password");
		}

		var user = await _userManager.FindByEmailAsync(userDto.Email);

		var token = _tokenService.GetToken(user);

		return Ok(token);
	}

	[HttpPost]
	[Route("register")]
	public async Task<IActionResult> Register([FromBody] RegisterDTO userDto)
	{
		var user = new IdentityUser
		{
			UserName = userDto.Email,
			Email = userDto.Email,
			EmailConfirmed = true
		};

		var result = await _userManager.CreateAsync(user, userDto.Password);

		if (!result.Succeeded)
		{
			var errors = JsonSerializer.Serialize(result.Errors);

			throw new BadRequestException($"Cannot register user: {errors}");
		}

		return Created();
	}

	[HttpGet]
	[Authorize]
	public IActionResult Test()
	{
		return Ok();
	}
}
