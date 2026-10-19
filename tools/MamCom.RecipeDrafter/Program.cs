using System.Text;
using MamCom.RecipeDrafter.Cli;

// The messages are Vietnamese; make sure the console shows them.
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // Let the run stop between two dishes, so a draft is never left half written.
    e.Cancel = true;
    cancellation.Cancel();
};

return await CommandLine.RunAsync(args, Console.Out, Console.Error, DrafterEnvironment.Create(), cancellation.Token);
