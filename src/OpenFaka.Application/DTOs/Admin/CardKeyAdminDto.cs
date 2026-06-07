namespace OpenFaka.Application.DTOs.Admin;

public class ImportCardKeysRequest
{
    public long ProductId { get; set; }
    public long? SpecId { get; set; }
    public List<string> CardKeys { get; set; }
}

public class CardImportBatch
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public long? SpecId { get; set; }
    public string ImportedBy { get; set; }
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public string FailDetail { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CardKeyAdminDto
{
    public long Id { get; set; }
    /// <summary>
    /// 完整内容（仅管理员列表/详情模式使用）
    /// </summary>
    public string Content { get; set; }
    public string ProductTitle { get; set; }
    public string SpecName { get; set; }
    /// <summary>
    /// 脱敏后的内容
    /// </summary>
    public string ContentMasked { get; set; }
    public string Status { get; set; }
    public string OrderNo { get; set; }
    public DateTime? SoldAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CardKeyStockDto
{
    public long ProductId { get; set; }
    public string ProductTitle { get; set; }
    public long? SpecId { get; set; }
    public string SpecName { get; set; }
    public bool? SpecEnabled { get; set; }
    /// <summary>
    /// 总数 = available + sold + locked + invalid
    /// </summary>
    public int Total { get; set; }
    public int Available { get; set; }
    public int Sold { get; set; }
    public int Locked { get; set; }
    public int Invalid { get; set; }
}
