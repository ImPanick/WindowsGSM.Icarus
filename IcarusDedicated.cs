using System;
using System.Diagnostics;
using System.Linq;
using system.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using Newtonsoft.Json;
using Microsoft.Win32;

namespace WindowsGSM.Plugins
{
    public class Icarus: SteamCMDAgent
    {
        public Plugin Plugin = new Plugin
        {
            Name = "Icarus",
            Author = "ImPanicking",
            Description = "Icarus Dedicated Server",
            Version = "1.0.0",
            url = "https://github.com/ImPanick/WindowsGSM.Icarus", // GitHub Repo for the plugin
            color = "#ffb121" // Icarus' Gold-ish Yellow
        };

        //SteamCMD Installer setup/Initialization (Will probe for updates)
        public override bool loginAnonymous => true;
        public override bool AppID => "2089300"; // Dedicated Game Server AppID for Icarus!

        //Game Server Setup
        public Icarus(ServerConfig serverData) : base(serverData) => base.serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;

        private string GetCurrentSteamId() // This entire process just locates the users' SteamID to use for setting the save files of the .json prospect files (The different maps will be on GitHub repository!)
        {
            try
            {
                // Check if Steam is running
                Process[] steamProcesses = Process.GetProcessesByName("steam");
                if (steamProcesses.Length == 0)
                {
                    Console.WriteLine("Steam is not running. Please start Steam and try again.");
                    return null;
                }

                // Get Steam installation path from registry -- This is going to look Very sketchy on the surface but I promise it works and is not nefarious! (It's open source for crying out loud lol)
                string steamPath = Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
                    "InstallPath",
                    null)?.ToString();

                if (string.IsNullOrEmpty(steamPath))
                {
                    Console.WriteLine("Could not locate Steam installation.");
                    return null;
                }

                // Read Steam's loginusers.vdf to get active user
                string loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
                if (!File.Exists(loginUsersPath))
                {
                    Console.WriteLine("Could not find Steam login information.");
                    return null;
                }

                // Read and parse the loginusers.vdf file
                string[] lines = File.ReadAllLines(loginUsersPath);
                foreach (string line in lines)
                {
                    // Look for the "AccountID" or "steamid" entry that has "MostRecent" "1" -- See I am ONLY pulling the ID!
                    if (line.Contains("\"steamid\""))
                    {
                        string steamId = line.Split('"')[3].Trim();
                        return steamId;
                    }
                }

                Console.WriteLine("No active Steam user found.");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting Steam ID: {ex.Message}");
                return null;
            }
        }

        private bool ScanAndConfigureServer()
        {
            // ... existing config file checking code ...

            string selectedMap = mapChoice switch
            {
                1 => "Prometheus",
                2 => "Styx",
                3 => "Olympus",
                _ => "Olympus" // Default fallback will be Olympus
            };

            // Create configuration files
            try
            {
                Directory.CreateDirectory(configPath);
                CreateConfigFiles(configPath, selectedMap);
                
                // Download and move map files
                if (!DownloadAndSetupMapFiles(selectedMap))
                {
                    Console.WriteLine("Failed to download or setup map files.");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during server configuration: {ex.Message}");
                return false;
            }
        }

        private bool DownloadAndSetupMapFiles(string mapName)
        {
            try
            {
                // Define the GitHub raw URLs for each map
                Dictionary<string, string> mapUrls = new()
                {
                    { "Prometheus", "https://raw.githubusercontent.com/ImPanick/WindowsGSM.Icarus/refs/heads/main/IcarusWorlds/Prometheus/Prometheus.json" },
                    { "Styx", "https://raw.githubusercontent.com/ImPanick/WindowsGSM.Icarus/refs/heads/main/IcarusWorlds/Styx/Styx.json" },
                    { "Olympus", "https://raw.githubusercontent.com/ImPanick/WindowsGSM.Icarus/refs/heads/main/IcarusWorlds/Olympus/Olympus.json" }

                    //OLD URLS (these are slightly shorter URL's that are for the maps.)
                    //{ "Prometheus", "https://raw.githubusercontent.com/ImPanick/prometheus-files/main/prospect.json" },
                    //{ "Styx", "https://raw.githubusercontent.com/ImPanick/styx-files/main/prospect.json" },
                    //{ "Olympus", "https://raw.githubusercontent.com/ImPanick/olympus-files/main/prospect.json" }
                };

                // Get user's Steam ID
                string steamId = GetCurrentSteamId(); // Using the method we created earlier to get the Steam ID
                if (string.IsNullOrEmpty(steamId))
                {
                    Console.WriteLine("Could not determine Steam ID.");
                    return false;
                }

                // Setup destination path
                string prospectsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Icarus", "Saved", "PlayerData", steamId, "Prospects"
                );

                // Create directory if it doesn't exist
                Directory.CreateDirectory(prospectsPath);

                // Download the map files
                using (var client = new HttpClient())
                {
                    string mapUrl = mapUrls[mapName];
                    string destinationFile = Path.Combine(prospectsPath, $"{mapName.ToLowerInvariant()}_prospect.json");
                    
                    Console.WriteLine($"Downloading map files for {mapName}...");
                    string jsonContent = client.GetStringAsync(mapUrl).Result;
                    File.WriteAllText(destinationFile, jsonContent);
                    Console.WriteLine("Map files downloaded successfully!");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading map files: {ex.Message}");
                return false;
            }
        }

        private string GetSteamID()
        {
            // You'll need to implement this method to retrieve the Steam ID
            // This could be from a config file, user input, or Steam API
            Console.Write("Enter your Steam ID: ");
            return Console.ReadLine()?.Trim();
        }

        private void CreateConfigFiles(string configPath, string mapName)
        {
            // Create GameUserSettings.ini  
            string gameUserSettingsPath = Path.Combine(configPath, "GameUserSettings.ini");
            File.WriteAllText(gameUserSettingsPath, $@"[/Script/Icarus.GameUserSettings]
            MapName={mapName}
            // Add other necessary GameUserSettings configurations
            ");

                // Create ServerSettings.ini
                string serverSettingsPath = Path.Combine(configPath, "ServerSettings.ini");
                File.WriteAllText(serverSettingsPath, $@"[/Script/Icarus.ServerSettings]
            DefaultMap={mapName}
            // Add other necessary ServerSettings configurations
            ");
        }

        //Fixed Variables which would be the Executable of the Game Server as well as naming the server.
        public override string StartPath => "IcarusServer.exe"; // Path to the Game Server Executable
        public override string FullName => "Icarus Dedicated Server"; // Name of the Server
        public int PortIncrement = 1;

        public object QueryMethod = new A2S();

        //Game Server Ports
        public int Port = 27015; // Game Server Port
        public int QueryPort = 27016; // Query Port

    }
}