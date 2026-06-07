using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FreeSql;
using OpenFaka.Core.Entities;
using OpenFaka.Core.Interfaces;
using LinCms.Entities;

namespace OpenFaka.Web.Controllers.Admin;

[ApiController]
[Route("admin/faka/payment-channels")]
[Authorize(Roles = LinGroup.Admin)]
public class AdminPaymentChannelController : ControllerBase
{
    private readonly IFreeSql _db;
    private readonly IConfiguration _config;
    private readonly ICacheService _cache;

    public AdminPaymentChannelController(IFreeSql db, IConfiguration config, ICacheService cache)
    {
        _db = db;
        _config = config;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetChannels()
    {
        var channels = await _db.Select<FakaPaymentChannel>()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        // 脱敏：不返回完整的 config_data
        var result = channels.Select(c => new
        {
            c.Id,
            c.ChannelCode,
            c.ChannelName,
            c.ProviderType,
            c.IsEnabled,
            c.SortOrder,
            HasConfig = !string.IsNullOrEmpty(c.ConfigData),
            c.CreateTime
        });

        return Ok(result);
    }

    [HttpGet("{id}/config")]
    public async Task<IActionResult> GetChannelConfig(long id)
    {
        var channel = await _db.Select<FakaPaymentChannel>()
            .Where(c => c.Id == id && !c.IsDeleted)
            .FirstAsync();
        if (channel == null) return NotFound();

        var decrypted = string.IsNullOrEmpty(channel.ConfigData) ? null : DecryptConfig(channel.ConfigData);
        return Ok(new { configData = decrypted });
    }

    [HttpPost]
    public async Task<IActionResult> CreateChannel([FromBody] CreateChannelRequest request)
    {
        var encrypted = string.IsNullOrEmpty(request.ConfigData) ? null : EncryptConfig(request.ConfigData);

        var channel = new FakaPaymentChannel
        {
            ChannelCode = request.ChannelCode,
            ChannelName = request.ChannelName,
            ProviderType = request.ProviderType,
            ConfigData = encrypted,
            IsEnabled = request.IsEnabled,
            SortOrder = request.SortOrder
        };

        await _db.Insert(channel).ExecuteAffrowsAsync();
        await _cache.RemoveAsync("store:payment_channels");
        return Ok(new { id = channel.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateChannel(long id, [FromBody] UpdateChannelRequest request)
    {
        var channel = await _db.Select<FakaPaymentChannel>()
            .Where(c => c.Id == id && !c.IsDeleted)
            .FirstAsync();
        if (channel == null) return NotFound();

        var encrypted = string.IsNullOrEmpty(request.ConfigData) ? null : EncryptConfig(request.ConfigData);

        await _db.Update<FakaPaymentChannel>()
            .Where(c => c.Id == id)
            .Set(c => c.ChannelName, request.ChannelName)
            .Set(c => c.IsEnabled, request.IsEnabled)
            .Set(c => c.SortOrder, request.SortOrder)
            .SetIf(!string.IsNullOrEmpty(request.ConfigData), c => c.ConfigData, encrypted)
            .ExecuteAffrowsAsync();

        await _cache.RemoveAsync("store:payment_channels");
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteChannel(long id)
    {
        await _db.Update<FakaPaymentChannel>()
            .Where(c => c.Id == id)
            .Set(c => c.IsDeleted, true)
            .ExecuteAffrowsAsync();

        await _cache.RemoveAsync("store:payment_channels");
        return Ok();
    }

    private string EncryptConfig(string plainText)
    {
        var key = _config["Jwt:SecretKey"] ?? "CHANGE_ME_TO_AT_LEAST_32_CHARACTERS!";
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(key[..32]);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + encryptedBytes.Length];
        Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
        Array.Copy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

        return Convert.ToBase64String(result);
    }

    private string DecryptConfig(string cipherText)
    {
        try
        {
            var key = _config["Jwt:SecretKey"] ?? "CHANGE_ME_TO_AT_LEAST_32_CHARACTERS!";
            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key[..32]);

            var iv = new byte[16];
            Array.Copy(fullCipher, 0, iv, 0, 16);
            aes.IV = iv;

            var cipher = new byte[fullCipher.Length - 16];
            Array.Copy(fullCipher, 16, cipher, 0, cipher.Length);

            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch
        {
            return null;
        }
    }
}

public class CreateChannelRequest
{
    public string ChannelCode { get; set; }
    public string ChannelName { get; set; }
    public Core.Enums.PaymentChannelType ProviderType { get; set; }
    public string ConfigData { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
}

public class UpdateChannelRequest
{
    public string ChannelName { get; set; }
    public bool IsEnabled { get; set; }
    public int SortOrder { get; set; }
    public string? ConfigData { get; set; }
}
