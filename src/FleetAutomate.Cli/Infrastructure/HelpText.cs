namespace FleetAutomate.Cli.Infrastructure;

internal static class HelpText
{
    public static string Value =>
        """
        FleetAutomate CLI

        Usage:
          fleetctl <resource> <verb> --project <path> [options]

        Resources:
          testproj  create | show | list-flows
          testflow  show | tree | create
          action    list | tree | show | add | set | remove

        Examples:
          fleetctl testproj show --project D:\demo\sample.testproj
          fleetctl testproj create --project D:\demo\sample.testproj --name SampleProject
          fleetctl testflow tree --project D:\demo\sample.testproj --flow basic_flow
          fleetctl action show --project D:\demo\sample.testproj --flow basic_flow --path 7.if.2
          fleetctl testflow create --project D:\demo\sample.testproj --name calculator_flow
          fleetctl action add --project D:\demo\sample.testproj --flow calculator_flow --type LaunchApplicationAction
          fleetctl action set --project D:\demo\sample.testproj --flow calculator_flow --path 0 --property ExecutablePath --value calc.exe
          fleetctl action remove --project D:\demo\sample.testproj --flow calculator_flow --path 1

        Options:
          --format table|json
        """;
}

