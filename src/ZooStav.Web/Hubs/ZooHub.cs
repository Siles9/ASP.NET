using Microsoft.AspNetCore.SignalR;

namespace ZooStav.Web.Hubs;

/// <summary>
/// Хаб реального времени: после добавления записи дневника или доната
/// все открытые страницы (в т.ч. страница животного на поддомене) мгновенно получают уведомление.
/// </summary>
public class ZooHub : Hub
{
    public async Task JoinAnimal(string slug)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "animal:" + slug);
        await Clients.Caller.SendAsync("joined", slug);
    }

    public async Task LeaveAnimal(string slug)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "animal:" + slug);
    }
}
