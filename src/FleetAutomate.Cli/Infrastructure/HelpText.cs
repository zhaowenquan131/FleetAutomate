namespace FleetAutomate.Cli.Infrastructure;

internal static class HelpText
{
    public static string Value =>
        """
        FleetAutomate CLI

        Usage:
          fleetctl <resource> <verb> --project <path> [options]

        Resources:
          testproj  show | list-flows
          testflow  show | tree
          action    list | tree | show

        Examples:
          fleetctl testproj show --project D:\demo\sample.testproj
          fleetctl testflow tree --project D:\demo\sample.testproj --flow basic_flow
          fleetctl action show --project D:\demo\sample.testproj --flow basic_flow --path 7.if.2

        Options:
          --format table|json
        """;
}

