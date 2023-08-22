using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

// References:
//      Classic:https://learn.microsoft.com/en-us/azure/templates/Microsoft.ServiceFabric/clusters/applications?pivots=deployment-language-arm-template
//      Managed: https://learn.microsoft.com/en-us/azure/templates/microsoft.servicefabric/managedclusters/applications?pivots=deployment-language-arm-template

internal class Program
{
    public const string apiVersion = "2023-03-01-preview";

    private static void Main(string[] args)
    {
        while (true)
        {
            // Accept user input
            Console.Write("Please provide input file path: ");
            string? inputFilePath = Console.ReadLine();

            if (inputFilePath == null || inputFilePath == "")
            {
                throw new Exception("Need input to convert!");
            }
            else
            {
                Console.WriteLine("Reading from: {0}", inputFilePath);
                Console.WriteLine("\n");
            }

            // Remove quotes from copying file path before reading
            inputFilePath = inputFilePath.Replace("\"", "");
            string fileContents = File.ReadAllText(inputFilePath);

            // Convert to MC App Resource, all matching resources are attempted to be transitioned to MC model. Null resources will be removed at end.
            /// App top level conversion
            var appClassic = JsonConvert.DeserializeObject<Dictionary<string, object>>(fileContents);
            var appManaged = new Dictionary<string, object>();

            if (appClassic == null)
            {
                    throw new Exception("Issue deserializing json input, please ensure it is formatted properly, then try again");
            }

            appManaged.Add("type", "Microsoft.ServiceFabric/managedclusters/applications");
            appManaged.Add("apiVersion", apiVersion);
            appManaged.Add("name", TryGetValue(appClassic, "name"));
            appManaged.Add("location", TryGetValue(appClassic, "location"));
            appManaged.Add("dependsOn", TryGetValue(appClassic, "dependsOn"));
            appManaged.Add("tags", TryGetValue(appClassic, "tags"));
            appManaged.Add("identity", TryGetValue(appClassic, "identity"));

            var removedProperties = new List<string>();
           
            /// App Properties conversion
            if (appClassic.ContainsKey("properties"))
            {
                var serializedProperties = JsonConvert.SerializeObject(appClassic["properties"]);
                var propertiesClassic = JsonConvert.DeserializeObject<Dictionary<string, object>>(serializedProperties) ?? throw new Exception("\"properties\" could not be deserialized!");
                var propertiesManaged = new Dictionary<string, object>();

                propertiesManaged.Add("managedIdentities", TryGetValue(propertiesClassic, "managedIdentities"));
                propertiesManaged.Add("parameters", TryGetValue(propertiesClassic, "parameters"));

                var typeName = TryGetValue(propertiesClassic, "typeVersion");
                var typeVersion = TryGetValue(propertiesClassic, "typeName");
                string version = null;

                if (typeName != null && typeVersion != null)
                {
                    version = $"[resourceId(resourcegroup().name, 'Microsoft.ServiceFabric/managedClusters/applicationTypes/versions', parameters('clusterName'), '{typeName}', '{typeVersion}')";
                    propertiesManaged.Add("version", version);
                }
                else
                {
                    if (typeName == null) removedProperties.Add("typeName");
                    if (typeVersion == null) removedProperties.Add("typeVersion");
                }

                if (propertiesClassic.ContainsKey("upgradePolicy"))
                {
                    /// App Upgrade Policy Conversion
                    var serializedUpgradePolicyClassic = JsonConvert.SerializeObject(propertiesClassic["upgradePolicy"]);
                    var upgradePolicyClassic = JsonConvert.DeserializeObject<Dictionary<string, object>>(serializedUpgradePolicyClassic) ?? throw new Exception("\"upgradePolicy\" could not be deserialized!");
                    var upgradePolicyManaged = new Dictionary<string, object>
                    {
                        { "forceRestart", TryGetValue(upgradePolicyClassic, "forceRestart") },
                        { "recreateApplication", TryGetValue(upgradePolicyClassic, "recreateApplication") },
                        { "rollingUpgradeMonitoringPolicy", TryGetValue(upgradePolicyClassic, "rollingUpgradeMonitoringPolicy") },
                        { "applicationHealthPolicy", TryGetValue(upgradePolicyClassic, "applicationHealthPolicy") }
                    };

                    var upgradeMode = TryGetValue(upgradePolicyClassic, "upgradeMode");
                    if (upgradeMode == null ||
                        upgradeMode == "Monitored" ||
                        upgradeMode == "UnmonitoredAuto")
                    {
                        upgradePolicyManaged.Add("upgradeMode", upgradeMode);
                    }
                    else
                    {
                        throw new Exception($"Valid upgradeMode is required for upgradePolicy! Provided upgradeMode: {upgradeMode}");
                    }

                    var upgradeReplicaSetCheckTimeout = (string)TryGetValue(upgradePolicyClassic, "upgradeReplicaSetCheckTimeout");
                    if (upgradeReplicaSetCheckTimeout != null)
                    {
                        var timespan = TimeSpan.Parse(upgradeReplicaSetCheckTimeout);
                        upgradePolicyManaged.Add("upgradeReplicaSetCheckTimeout", (uint)timespan.TotalSeconds);
                    }

                    propertiesManaged.Add("upgradePolicy", RemoveNullEntries(upgradePolicyManaged));
                };

                if (propertiesClassic.ContainsKey("maximumNodes"))
                {
                    removedProperties.Add("maximumNodes");
                }
                if (propertiesClassic.ContainsKey("minimumNodes"))
                {
                    removedProperties.Add("minimumNodes");
                }
                if (propertiesClassic.ContainsKey("metrics"))
                {
                    removedProperties.Add("metrics");
                }
                if (propertiesClassic.ContainsKey("removeApplicationCapacity"))
                {
                    removedProperties.Add("removeApplicationCapacity");
                }

                appManaged.Add("properties", RemoveNullEntries(propertiesManaged));
            }
            else
            {
                appManaged.Add("properties", new JObject());
            }

            appManaged = RemoveNullEntries(appManaged);
            var results = JsonConvert.SerializeObject(appManaged, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            // Return result / write to new file
            for (int i = 0; i < 10; i++) 
            {
                Console.Write(". ");
            }
            Console.WriteLine("Ready!");

            Console.Write("Please provide destination file path: ");
            var dest = Console.ReadLine();
            if (dest == null)
            {
                throw new Exception("No destination provided!");
            }

            dest = dest.Replace("\"", "");
            File.WriteAllText(dest, results);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(results);
            Console.WriteLine("\n");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("The following fields do not exist in managed clusters and have been removed:\n");
            Console.WriteLine(string.Join(",\n", removedProperties));

            Console.ForegroundColor = ConsoleColor.White;

            // Ask user if they want to convert another file
            Console.Write("Convert another file? [Y] Yes [N] No [Default] No: ");
            var continueFlag = Console.ReadLine() ?? throw new Exception("No input provided! Quitting program...");

            if (continueFlag.ToLower() != "y") break;
        }
    }

    private static object TryGetValue(Dictionary<string, object> dictionary, string key)
    {
        if (dictionary.ContainsKey(key))
        {
            return dictionary[key];
        }
        return null;
    }

    private static Dictionary<string, object> RemoveNullEntries (Dictionary<string, object> dictionary)
    {
        return dictionary.Where(d => d.Value != null).ToDictionary(k => k.Key, v => v.Value);
    }
}