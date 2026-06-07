using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DotNetCore.Security;
using FreeSql;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenFaka.Core.Entities.Cms;

namespace OpenFaka.Web.Controllers.Cms;

[ApiController]
[Route("cms/user")]
public class AuthController : ControllerBase
{
    private readonly IFreeSql _db;
    private readonly ICryptographyService _crypto;
    private readonly IConfiguration _config;

    // 验证码存储：captchaId -> code（5分钟过期，简单实现）
    private static readonly ConcurrentDictionary<string, (string Code, DateTime Expire)> _captchas = new();

    public AuthController(IFreeSql db, ICryptographyService crypto, IConfiguration config)
    {
        _db = db;
        _crypto = crypto;
        _config = config;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Select<LinUser>()
            .Where(u => u.Username == request.Username || u.Email == request.Username)
            .FirstAsync();

        if (user == null)
            return Unauthorized(new { code = 1001, message = "用户不存在" });

        if (user.Active != 1)
            return Unauthorized(new { code = 1002, message = "用户未激活" });

        // 验证密码
        var identity = await _db.Select<LinUserIdentity>()
            .Where(i => i.CreateUserId == user.Id && i.IdentityType == "Password")
            .FirstAsync();

        if (identity != null)
        {
            var encrypted = _crypto.Encrypt(request.Password, user.Salt ?? "");
            if (identity.Credential != encrypted)
                return Unauthorized(new { code = 1003, message = "密码错误" });
        }

        // 查询用户分组
        var groups = await _db.Select<LinGroup>()
            .InnerJoin<LinUserGroup>((g, ug) => g.Id == ug.GroupId && ug.UserId == user.Id)
            .ToListAsync();

        // 生成 Token
        var (accessToken, refreshToken) = GenerateTokens(user, groups);

        // 保存 refresh token
        await _db.Update<LinUser>(user.Id)
            .Set(u => u.RefreshToken, refreshToken)
            .Set(u => u.LastLoginTime, DateTime.Now)
            .ExecuteAffrowsAsync();

        return Ok(new
        {
            access_token = accessToken,
            refresh_token = refreshToken,
            token_type = "Bearer",
            expires_in = 86400
        });
    }

    [AllowAnonymous]
    [HttpGet("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var authHeader = Request.Headers["Authorization"].ToString();
        var refreshToken = authHeader.StartsWith("Bearer ")
            ? authHeader["Bearer ".Length..]
            : authHeader;

        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { code = 1004, message = "请先登录" });

        var user = await _db.Select<LinUser>()
            .Where(u => u.RefreshToken == refreshToken)
            .FirstAsync();

        if (user == null)
            return Unauthorized(new { code = 1004, message = "无效的 refresh token" });

        var groups = await _db.Select<LinGroup>()
            .InnerJoin<LinUserGroup>((g, ug) => g.Id == ug.GroupId && ug.UserId == user.Id)
            .ToListAsync();

        var (accessToken, newRefreshToken) = GenerateTokens(user, groups);

        await _db.Update<LinUser>(user.Id)
            .Set(u => u.RefreshToken, newRefreshToken)
            .Set(u => u.LastLoginTime, DateTime.Now)
            .ExecuteAffrowsAsync();

        return Ok(new
        {
            access_token = accessToken,
            refresh_token = newRefreshToken,
            token_type = "Bearer",
            expires_in = 86400
        });
    }

    [Authorize]
    [HttpGet("logout")]
    public IActionResult Logout()
    {
        return Ok(new { code = 0, message = "退出成功" });
    }

    [Authorize]
    [HttpGet("information")]
    public async Task<IActionResult> GetInformation()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { code = 1005, message = "未登录" });

        var user = await _db.Select<LinUser>()
            .IncludeMany(u => u.Groups!)
            .Where(u => u.Id == userId)
            .FirstAsync();

        if (user == null)
            return Unauthorized(new { code = 1001, message = "用户不存在" });

        var groupNames = user.Groups?.Select(g => g.Name).ToList() ?? new List<string>();
        var role = groupNames.Contains("Admin") ? "ADMIN" : "USER";

        return Ok(new
        {
            id = user.Id,
            username = user.Username,
            nickname = user.Nickname,
            email = user.Email,
            avatar = user.Avatar,
            active = user.Active,
            groups = groupNames,
            role,
            points = 0,
            created_at = user.CreateTime?.ToString("yyyy-MM-ddTHH:mm:ss") ?? ""
        });
    }

    [Authorize]
    [HttpGet("permissions")]
    public IActionResult GetPermissions()
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();
        return Ok(new { permissions = new List<string>(), roles });
    }

    [Authorize]
    [HttpPut("change_password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { code = 1005, message = "未登录" });

        var user = await _db.Select<LinUser>().Where(u => u.Id == userId).FirstAsync();
        if (user == null)
            return BadRequest(new { code = 1001, message = "用户不存在" });

        // 验证旧密码
        var identity = await _db.Select<LinUserIdentity>()
            .Where(i => i.CreateUserId == user.Id && i.IdentityType == "Password")
            .FirstAsync();

        if (identity != null)
        {
            var oldEncrypted = _crypto.Encrypt(request.OldPassword, user.Salt ?? "");
            if (identity.Credential != oldEncrypted)
                return BadRequest(new { code = 1003, message = "旧密码错误" });
        }

        // 更新密码
        var newSalt = Guid.NewGuid().ToString();
        var newEncrypted = _crypto.Encrypt(request.NewPassword, newSalt);

        await _db.Update<LinUserIdentity>(identity!.Id)
            .Set(i => i.Credential, newEncrypted)
            .Set(i => i.UpdateTime, DateTime.Now)
            .ExecuteAffrowsAsync();

        await _db.Update<LinUser>(userId)
            .Set(u => u.Salt, newSalt)
            .Set(u => u.UpdateTime, DateTime.Now)
            .ExecuteAffrowsAsync();

        return Ok(new { code = 0, message = "密码修改成功" });
    }

    [AllowAnonymous]
    [HttpGet("captcha")]
    public IActionResult GetCaptcha()
    {
        // 清理过期验证码
        var now = DateTime.Now;
        foreach (var kv in _captchas)
        {
            if (kv.Value.Expire < now) _captchas.TryRemove(kv.Key, out _);
        }

        var code = GenerateCaptchaCode(4);
        var captchaId = Guid.NewGuid().ToString("N");
        _captchas[captchaId] = (code, now.AddMinutes(5));

        var svg = GenerateCaptchaSvg(code);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));

        return Ok(new
        {
            captcha_id = captchaId,
            captcha_image = $"data:image/svg+xml;base64,{base64}"
        });
    }

    [AllowAnonymous]
    [HttpPost("account/register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // 校验验证码
        if (string.IsNullOrEmpty(request.CaptchaId) || string.IsNullOrEmpty(request.Captcha))
            return BadRequest(new { code = 1007, message = "请输入验证码" });

        if (!_captchas.TryGetValue(request.CaptchaId, out var captchaEntry)
            || captchaEntry.Expire < DateTime.Now
            || !string.Equals(captchaEntry.Code, request.Captcha, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { code = 1008, message = "验证码错误或已过期" });
        }
        _captchas.TryRemove(request.CaptchaId, out _);

        var exists = await _db.Select<LinUser>()
            .Where(u => u.Username == request.Username || u.Email == request.Email)
            .AnyAsync();

        if (exists)
            return BadRequest(new { code = 1006, message = "用户名或邮箱已存在" });

        var salt = Guid.NewGuid().ToString();
        var encryptedPwd = _crypto.Encrypt(request.Password, salt);
        var now = DateTime.Now;

        // 插入用户
        var newUserId = await _db.Insert(new LinUser
        {
            Username = request.Username,
            Nickname = request.Nickname ?? request.Username,
            Email = request.Email,
            Salt = salt,
            Active = 1,
            CreateTime = now
        }).ExecuteIdentityAsync();

        // 创建密码身份
        await _db.Insert(new LinUserIdentity
        {
            Id = Guid.NewGuid().ToString(),
            IdentityType = "Password",
            Identifier = request.Username,
            Credential = encryptedPwd,
            CreateUserId = (long)newUserId,
            CreateTime = now
        }).ExecuteAffrowsAsync();

        // 加入默认用户组
        var defaultGroupId = await _db.Select<LinGroup>()
            .Where(g => g.Name == "User")
            .ToOneAsync(g => g.Id);

        if (defaultGroupId > 0)
        {
            await _db.Insert(new LinUserGroup
            {
                UserId = (long)newUserId,
                GroupId = defaultGroupId,
                CreateTime = now
            }).ExecuteAffrowsAsync();
        }

        return Ok(new { code = 0, message = "注册成功" });
    }

    // ============ JWT 生成 ============

    private (string accessToken, string refreshToken) GenerateTokens(LinUser user, IEnumerable<LinGroup> groups)
    {
        var jwtSection = _config.GetSection("Jwt");
        var secretKey = jwtSection["SecretKey"]!;
        var issuer = jwtSection["Issuer"] ?? "OpenFaka";
        var audience = jwtSection["Audience"] ?? "OpenFaka";
        var expireSeconds = int.Parse(jwtSection["ExpireSeconds"] ?? "86400");

        // 与 Program.cs 保持一致
        if (string.IsNullOrEmpty(secretKey) || secretKey.Contains("CHANGE_ME"))
        {
            var keyFile = Path.Combine(Directory.GetCurrentDirectory(), ".jwt-secret");
            if (System.IO.File.Exists(keyFile))
                secretKey = System.IO.File.ReadAllText(keyFile).Trim();
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(ClaimTypes.Name, user.Nickname ?? ""),
            new("username", user.Username ?? "")
        };

        foreach (var g in groups)
            claims.Add(new Claim(ClaimTypes.Role, g.Name));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddSeconds(expireSeconds),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        var accessToken = handler.WriteToken(handler.CreateToken(tokenDescriptor));
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        return (accessToken, refreshToken);
    }

    // ============ 验证码生成 ============

    private static string GenerateCaptchaCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 去掉容易混淆的 I/O/0/1
        var bytes = RandomNumberGenerator.GetBytes(length);
        var sb = new StringBuilder(length);
        foreach (var b in bytes)
            sb.Append(chars[b % chars.Length]);
        return sb.ToString();
    }

    private static string GenerateCaptchaSvg(string code)
    {
        var rnd = Random.Shared;
        var sb = new StringBuilder();
        sb.Append("<svg xmlns='http://www.w3.org/2000/svg' width='120' height='40'>");
        sb.Append("<rect width='120' height='40' fill='#f0f0f0'/>");

        // 干扰线
        for (var i = 0; i < 4; i++)
        {
            var color = $"rgb({rnd.Next(100,200)},{rnd.Next(100,200)},{rnd.Next(100,200)})";
            sb.Append($"<line x1='{rnd.Next(0,120)}' y1='{rnd.Next(0,40)}' x2='{rnd.Next(0,120)}' y2='{rnd.Next(0,40)}' stroke='{color}' stroke-width='1'/>");
        }

        // 文字
        for (var i = 0; i < code.Length; i++)
        {
            var x = 15 + i * 25;
            var y = 25 + rnd.Next(-5, 6);
            var rotate = rnd.Next(-15, 16);
            var r = rnd.Next(50, 180);
            var g = rnd.Next(50, 180);
            var b = rnd.Next(50, 180);
            var size = rnd.Next(22, 30);
            sb.Append($"<text x='{x}' y='{y}' font-size='{size}' font-family='monospace' font-weight='bold' fill='rgb({r},{g},{b})' transform='rotate({rotate},{x},{y})'>{code[i]}</text>");
        }

        // 干扰点
        for (var i = 0; i < 20; i++)
        {
            var color = $"rgb({rnd.Next(0,256)},{rnd.Next(0,256)},{rnd.Next(0,256)})";
            sb.Append($"<circle cx='{rnd.Next(0,120)}' cy='{rnd.Next(0,40)}' r='1' fill='{color}'/>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }
}

// ============ Request DTOs ============

public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Captcha { get; set; }
}

public class RegisterRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Nickname { get; set; }
    public string? CaptchaId { get; set; }
    public string? Captcha { get; set; }
}

public class ChangePasswordRequest
{
    public string OldPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}
