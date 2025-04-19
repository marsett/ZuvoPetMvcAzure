using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

namespace ZuvoPetMvcAzure.Hubs
{
    public class ChatHub : Hub
    {
        public async Task EnviarMensaje(int emisorId, int receptorId, string mensaje)
        {
            // Notificar a los clientes conectados (emisor y receptor)
            await Clients.User(emisorId.ToString()).SendAsync("RecibirMensaje", emisorId, mensaje);
            await Clients.User(receptorId.ToString()).SendAsync("RecibirMensaje", emisorId, mensaje);
        }

        public async Task ConectarUsuario(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        }
    }
}
