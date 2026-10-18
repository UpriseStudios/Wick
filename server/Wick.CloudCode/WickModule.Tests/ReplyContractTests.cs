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
    /// property a function returns must exist on the matching client class in WickBackend.cs.
    /// </summary>
    public class ReplyContractTests
    {
        [TestCase(typeof(PublishResult), "PublishReply")]
        [TestCase(typeof(RaidStartResult), "RaidStartReply")]
        [TestCase(typeof(SubmitResult), "SubmitReply")]
        public void ClientReplyDeclaresEveryServerField(Type server, string clientClass)
        {
            HashSet<string> client = ClientFields(clientClass);
            foreach (PropertyInfo p in server.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.That(client, Has.Member(p.Name.ToLowerInvariant()),
                    clientClass + " (unity/Assets/_Wick/Services/WickBackend.cs) is missing " + p.Name);
            }
        }

        private static HashSet<string> ClientFields(string className)
        {
            string source = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "WickBackend.cs.txt"));
            Match body = Regex.Match(source, @"class " + className + @"\s*\{(?<body>[^}]*)\}");
            Assert.IsTrue(body.Success, className + " not found in WickBackend.cs");
            var fields = new HashSet<string>();
            foreach (Match f in Regex.Matches(body.Groups["body"].Value, @"public\s+[\w<>\[\]]+\s+(?<name>\w+)\s*;"))
            {
                fields.Add(f.Groups["name"].Value.ToLowerInvariant());
            }

            return fields;
        }
    }
}
