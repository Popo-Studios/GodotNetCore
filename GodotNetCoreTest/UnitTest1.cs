using GodotNetCore;

namespace GodotNetCoreTest
{
    public class UnitTest1
    {
        [Fact]
        public async Task Test1()
        {
            Console.WriteLine("start");
            NetworkManager.Activate();
            bool result = await NetworkManager.Connect("127.0.0.1", 12345);
            Assert.True(result, "Failed to connect to server");

        }
    }
}