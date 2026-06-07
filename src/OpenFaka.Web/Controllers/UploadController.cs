using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OpenFaka.Web.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public UploadController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpPost("image")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { code = -1, message = "No file uploaded" });

        // 限制文件大小 10MB
        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { code = -1, message = "File too large (max 10MB)" });

        // 限制文件类型
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest(new { code = -1, message = "Invalid file type" });

        // 生成唯一文件名
        var ext = Path.GetExtension(file.FileName);
        var fileName = $"{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid():N}{ext}";
        var uploadsDir = Path.Combine(_env.ContentRootPath, "uploads");

        // 确保目录存在
        var filePath = Path.Combine(uploadsDir, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        // 保存文件
        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        // 返回 URL
        var url = $"/uploads/{fileName}";
        return Ok(new { url });
    }
}
