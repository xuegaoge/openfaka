using OpenFaka.Application.DTOs.Store;

namespace OpenFaka.Application.Interfaces;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, long? userId, string clientIp, int orderType = 0);
    Task<OrderDto> GetOrderAsync(string orderNo);
    Task<List<OrderDto>> GetOrdersByEmailAsync(string email);
    Task<bool> ProcessPaymentCallbackAsync(string provider, string payload, IDictionary<string, string> headers);
}
