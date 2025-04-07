using Refresh.MatchServer;

RefreshMatchServer server = new();
server.Start();
await Task.Delay(-1);