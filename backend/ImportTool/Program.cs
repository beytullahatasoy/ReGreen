using ImportTool;
using ImportTool.Cli;

CliOptions options;
try
{
    options = CliOptions.Parse(args);
}
catch (CliArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

var (exitCode, _) = await Orchestrator.RunAsync(options, Console.Out, Console.Error);
return exitCode;
