// See https://aka.ms/new-console-template for more information
using YYHEggEgg.Logger;

Console.WriteLine("Application starting. A log will be generated once a second.");

Log.Initialize(new()
{
    Use_Console_Wrapper = true,
});

_ = Task.Factory.StartNew(async () =>
{
    while (true)
    {
        Log.Info("This is a log information.", "Sender");
        await Task.Delay(1000);
    }
});
var echo = await ConsoleWrapper.ReadLineAsync();
Log.Warn($"You input {echo}");
while (true)
{
    await Task.Delay(1000);
}
