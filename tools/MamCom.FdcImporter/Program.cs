using System.Text;
using MamCom.FdcImporter.Cli;

// The messages are Vietnamese; make sure the console shows them.
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // Let the run stop between two calls, so a file is never left half written.
    e.Cancel = true;
    cancellation.Cancel();
};

return await CommandLine.RunAsync(args, Console.Out, Console.Error, ImporterEnvironment.Create(), cancellation.Token);
