using Newtonsoft.Json;

internal class Program
{
    public const string apiVersion = "2023-03-01-preview";
    private static void Main(string[] args)
    {
        while (true)
        {
            // Accept user input
            Console.Write("Please provide file path: ");
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

            // Convert to MC App Resource
            /// App top level conversion
            var appClassic = JsonConvert.DeserializeObject<Dictionary<string, object>>(fileContents);
            var appManaged = new Dictionary<string, object>();

            if (appClassic == null)
            {
                throw new Exception("Issue deserializing json input, please ensure it is formatted properly, then try again");
            }

            appManaged.Add("type", "Microsoft.ServiceFabric/managedclusters/applications");
            appManaged.Add("apiVersion", apiVersion);
            appManaged.Add("name", appClassic["name"]);
            appManaged.Add("location", appClassic["location"]);
            appManaged.Add("tags", appClassic["tags"]);
            appManaged.Add("identity", appClassic["identity"]);

            /// App properties conversion
            var serializedProperties = JsonConvert.SerializeObject(appClassic["properties"]);
            var propertiesClassic = JsonConvert.DeserializeObject<Dictionary<string, object>>(serializedProperties);
            var propertiesManaged = new Dictionary<string, object>();

            if (propertiesClassic == null)
            {
                throw new Exception("Expected non-empty properties");
            }

            propertiesManaged.Add("managedIdentities", propertiesClassic["managedIdentities"]);
            propertiesManaged.Add("parameters", propertiesClassic["parameters"]);
            propertiesManaged.Add("version", propertiesClassic["typeVersion"]);

            var removedProperties = new List<string>();
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
            if (propertiesClassic.ContainsKey("typeName"))
            {
                removedProperties.Add("typeName");
            }

            /// App Upgrade Policy Conversion
            var serializedUpgradePolicyClassic = JsonConvert.SerializeObject(propertiesClassic["upgradePolicy"]);
            var upgradePolicyClassic = JsonConvert.DeserializeObject<Dictionary<string, object>>(serializedUpgradePolicyClassic);
            var upgradePolicyManaged = new Dictionary<string, object>();

            if (upgradePolicyClassic == null)
            {
                throw new Exception("Expected valid upgradePolicy");
            };

            upgradePolicyManaged.Add("forceRestart", upgradePolicyClassic["forceRestart"]);
            upgradePolicyManaged.Add("recreateApplication", upgradePolicyClassic["recreateApplication"]);
            upgradePolicyManaged.Add("upgradeReplicaSetCheckTimeout", upgradePolicyClassic["upgradeReplicaSetCheckTimeout"]);
            upgradePolicyManaged.Add("rollingUpgradeMonitoringPolicy", upgradePolicyClassic["rollingUpgradeMonitoringPolicy"]);
            upgradePolicyManaged.Add("applicationHealthPolicy", upgradePolicyClassic["applicationHealthPolicy"]);

            if (upgradePolicyClassic["upgradeMode"] == "Monitored" ||
                upgradePolicyClassic["upgradeMode"] == "UnmonitoredAuto")
            {
                upgradePolicyManaged.Add("upgradeMode", upgradePolicyClassic["upgradeMode"]);
            }
            else
            {
                upgradePolicyManaged.Add("upgradeMode", "Monitored");
            }

            propertiesManaged.Add("upgradePolicy", upgradePolicyManaged);
            appManaged.Add("properties", propertiesManaged);

            var results = JsonConvert.SerializeObject(appManaged, Formatting.Indented);
            // Return result / write to new file
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(results);
            Console.WriteLine("\n");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("The following fields do not exist in managed clusters and have been removed:\n");
            Console.WriteLine(string.Join(",\n", removedProperties));

            Console.ForegroundColor = ConsoleColor.White;

            // Ask user if they want to convert another file
            Console.Write("Convert another file? [Y] Yes [N] No [Default] No: ");
            var continueFlag = Console.ReadLine();

            if (continueFlag != "Y") break;
        }
    }
}