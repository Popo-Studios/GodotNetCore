using GodotNetCore;

Console.WriteLine("NetworkManager activated.");
bool res = await NetworkManager.Connect("127.0.0.1", 12345);

var lr = await NetworkManager.Login("a", "1234");

if (lr.Success && lr.UserIdentifier != null) {

    SessionCreationOption co = new SessionCreationOption {
        UserIdentifier = lr.UserIdentifier.Value,
        MaxPlayers = 10,
        Name = "session1",
        SessionType = ""
    };

    var cr = await SessionManager.CreateNewSession(co);
    Console.WriteLine($"Session creation: {cr.Success} {cr.SessionInfo?.Name ?? "None"}");
    NetworkManager.Disconnect();

    if (cr.SessionInfo != null) {
        await NetworkManager.Connect("127.0.0.1", cr.SessionInfo.Value.Identifier.SessionPort);
        var jr = await SessionManager.JoinSession(cr.SessionInfo.Value, new SessionJoinOption {
            UserIdentifier = lr.UserIdentifier.Value,
            SessionNumber = cr.SessionInfo.Value.Identifier.SessionNumber,
            Password = null
        });

        Console.WriteLine($"join: {jr.Success}");

        NetworkManager.Disconnect();

        res = await NetworkManager.Connect("127.0.0.1", 12345);

        SessionListOption option = default;
        option.Page = 1; option.SessionPerPage = 10; option.SessionType = "";
        var result = await SessionManager.GetSessionList(option);
        Console.WriteLine(result.TotalSessionCount);
    }
}