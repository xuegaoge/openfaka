using OpenFaka.Application.DTOs.Admin;

namespace OpenFaka.Application.Interfaces;

public interface ICardKeyService
{
    Task<CardImportBatch> ImportCardKeysAsync(ImportCardKeysRequest request, long importedBy);
    Task<List<CardKeyAdminDto>> GetCardKeysAsync(long? productId, int page, int count, bool includeContent = false);
    Task<int> GetCardKeyCountAsync(long? productId);
    Task<bool> VoidCardKeyAsync(long id);
    Task<List<CardKeyStockDto>> GetStockSummaryAsync();
    Task<List<string>> GetCardKeysForDeliveryAsync(long orderItemId, long productId, long? specId, int quantity);
}
