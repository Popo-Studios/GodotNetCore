using GodotNetCore;

NetworkManager.Activate();
Console.WriteLine("NetworkManager activated.");
bool res = await NetworkManager.Connect("127.0.0.1", 12345);
Console.WriteLine(res);