using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Wick.CloudCode.Tests
{
    /// <summary>
    /// The Unity Cloud Code client throws on any reply field its class doesn't declare, so every
    /// property a function returns must exist on the matching client class (WickBackend.cs,
    /// EconomyClient.cs). Computed properties are kept out of JSON with [JsonIgnore].
    /// </summary>
    public class ReplyContractTests
    {
        [TestCase(typeof(PublishResult), "PublishReply")]
        [TestCase(typeof(RaidStartResult), "RaidStartReply")]
        [TestCase(typeof(SubmitResult), "SubmitReply")]
        [TestCase(typeof(DefenceLogResult), "DefenceLogReply")]
        [TestCase(typeof(DefenceEntry), "DefenceEntryReply")]
        [TestCase(typeof(ReplayResult), "ReplayReply")]
        [TestCase(typeof(EconomyState), "EconomyStateReply")]
        [TestCase(typeof(RunningUpgrade), "RunningUpgradeReply")]
        [TestCase(typeof(AchievementProgress), "AchievementProgressReply")]
        [TestCase(typeof(EconomyResult), "EconomyReply")]
        [TestCase(typeof(AdRewardStatus), "AdRewardReply")]
        [TestCase(typeof(AdGrant), "AdGrantReply")]
        [TestCase(typeof(PurchaseResult), "PurchaseReply")]
        public void ClientReplyDeclaresEveryServerField(Type server, string clientClass)
        {
            HashSet<string> client = ClientFields(clientClass);
            foreach (PropertyInfo p in server.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetCustomAttribute<Newtonsoft.Json.JsonIgnoreAttribute>() != null)
                {
                    continue; // never serialised
                }

                Assert.That(client, Has.Member(p.Name.ToLowerInvariant()),
                    clientClass + " (unity/Assets/_Wick/Services/WickBackend.cs) is missing " + p.Name);
            }
        }

        private static HashSet<string> ClientFields(string className)
        {
            string data = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
            string source = File.ReadAllText(Path.Combine(data, "WickBackend.cs.txt")) + File.ReadAllText(Path.Combine(data, "EconomyClient.cs.txt"));
            Match body = Regex.Match(source, @"class " + className + @"\s*\{(?<body>[^}]*)\}");
            Assert.IsTrue(body.Success, className + " not found in WickBackend.cs");
            var fields = new HashSet<string>();
            foreach (Match f in Regex.Matches(body.Groups["body"].Value, @"public\s+[\w<>\[\],\. ]+?\s+(?<name>\w+)\s*;"))
            {
                fields.Add(f.Groups["name"].Value.ToLowerInvariant());
            }

            return fields;
        }
    }
}
