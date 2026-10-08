using Microsoft.AspNetCore.SignalR;

namespace ZooStav.Web.Hubs;

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
