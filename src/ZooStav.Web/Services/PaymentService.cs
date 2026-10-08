using ZooStav.Web.Domain;

namespace ZooStav.Web.Services;

public record PaymentRequest(int AnimalId, decimal Amount, string Purpose, string DonorName, string? DonorEmail, string Message);
public record PaymentResult(bool Success, string Reference, string Message);

public interface IPaymentService
{
    Task<PaymentResult> ProcessAsync(PaymentRequest request, CancellationToken ct = default);
}

/// <summary>
/// Демонстрационный платёжный шлюз (free donation). Реальная интеграция (ЮKassa / CloudPayments)
/// подключается заменой этой реализации — контроллеры и API менять не нужно.
/// </summary>
public class MockPaymentService(ILogger<MockPaymentService> logger) : IPaymentService
{
    public Task<PaymentResult> ProcessAsync(PaymentRequest request, CancellationToken ct = default)
    {
        var reference = "ZOO-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" +
                        Random.Shared.Next(1000, 9999);

        logger.LogInformation("Донат обработан демо-шлюзом: {Animal} / {Amount} {Purpose} / {Reference}",
            request.AnimalId, request.Amount, request.Purpose, reference);

        var purpose = DonationPurposes.Title(request.Purpose);
        return Task.FromResult(new PaymentResult(true, reference,
            $"Спасибо! Донат на «{purpose}» принят (демонстрационный платёжный шлюз, чек {reference})."));
    }
}
