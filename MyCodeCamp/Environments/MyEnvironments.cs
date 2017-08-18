using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyCodeCamp.Environments
{
    /// <summary>
    /// A contract class describing our possible environments
    /// Declared in launchSettings.json
    /// </summary>
    public static class MyEnvironments
    {
        public const string Development = "Development";
        public const string Test = "Test";
        public const string Staging = "Staging";
        public const string Production = "Production";
        public const string DevExt = "DevExt";
        public const string TestExt = "TestExt";
        public const string StagingExt = "StagingExt";
    }
}