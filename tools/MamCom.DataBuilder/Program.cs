using System.Text;
using MamCom.DataBuilder.Cli;

// The messages are Vietnamese; make sure the console shows them.
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

return CommandLine.Run(args, Console.Out, Console.Error);
