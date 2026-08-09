// See https://aka.ms/new-console-template for more information
using System.Security.Cryptography;
using System.Text;
using YYHEggEgg.Logger;

var pasteRegression = args.Contains("--paste-regression", StringComparer.Ordinal);
var pasteRegressionStress = pasteRegression &&
    args.Contains("--stress", StringComparer.Ordinal);
if (pasteRegression)
{
    Console.WriteLine(pasteRegressionStress
        ? "Paste regression stress mode. Paste a 100 KiB command, then press Enter."
        : "Paste regression mode. Paste a 100 KiB command, then press Enter.");
}
else
{
    Console.WriteLine("Press Enter to start debugging");
    Console.ReadLine();
    Console.WriteLine("Application starting. A log will be generated once a second.");
}

Log.Initialize(new()
{
    Use_Console_Wrapper = true,
});

_ = Task.Factory.StartNew(async () =>
{
    while (true)
    {
        Log.Info("This is a log information.", "Sender");
        await Task.Delay(pasteRegressionStress ? 20 : 1000);
    }
});
var input = await ConsoleWrapper.ReadLineAsync();
if (pasteRegression)
{
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    Log.Warn($"Paste regression result: Length={input.Length}; SHA-256={hash}", "PasteRegression");
    return;
}

Log.Warn($"😀 You input {input}");
while (true)
{
    await Task.Delay(1000);
}
