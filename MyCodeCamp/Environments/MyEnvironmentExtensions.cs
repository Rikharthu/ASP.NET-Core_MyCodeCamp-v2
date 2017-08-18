using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace MyCodeCamp.Environments
{
    /// <summary>
    /// A static class that is holding our method extensions on an IHostingEnvironment interface
    /// About custom environments: https://blogs.msdn.microsoft.com/mvpawardprogram/2017/04/25/custom-environ-asp-net-core/
    /// About extension methods: https://weblogs.asp.net/scottgu/new-orcas-language-feature-extension-methods
    /// </summary>
    public static class MyEnvironmentExtensions
    {
        public static bool IsTest(this IHostingEnvironment env)
        {
            return env.IsEnvironment(MyEnvironments.Test);
        }
        public static bool IsTestExt(this IHostingEnvironment env)
        {
            return env.IsEnvironment(MyEnvironments.TestExt);
        }
        public static bool IsDevExt(this IHostingEnvironment env)
        {
            return env.IsEnvironment(MyEnvironments.DevExt);
        }
        public static bool IsStagingExt(this IHostingEnvironment env)
        {
            return env.IsEnvironment(MyEnvironments.StagingExt);
        }
    }
}
