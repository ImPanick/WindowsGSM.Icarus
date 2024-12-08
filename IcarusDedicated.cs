using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using System.Net.Http;
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
            Version = "0.0.1",
            url = "https://github.com/ImPanick/WindowsGSM.Icarus", // GitHub Repo for the plugin
            color = "#ffb121" // Icarus' Gold-ish Yellow
        };

        //SteamCMD Installer setup/Initialization (Will probe for updates if you check bottom of script)
        public override bool loginAnonymous => true;
        public override string AppId => "2089300";

        //Game Server Setup
        public Icarus(ServerConfig serverData) : base(serverData) => base.serverData = serverData;
        private readonly ServerConfig _serverData;
        public new string Error { get; set; }
        public new string Notice { get; set; }

        public string AdminPassword, FriendPassword, GuestPassword;

        private static Random random = new Random();

            private string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

            public void GeneratePasswords()
        {
            AdminPassword = GenerateRandomString(8);
            FriendPassword = GenerateRandomString(8);
            GuestPassword = GenerateRandomString(8);
        }

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

                // Get Steam installation path from registry and reads the loginusers.vdf file for the SteamID - This is passed as SteamID to be used to locate the prospect save files (Under User AppData directory)..
                string steamPath = Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
                    "InstallPath",
                    null)?.ToString();

                if (string.IsNullOrEmpty(steamPath))
                {
                    Console.WriteLine("Could not locate Steam installation.");
                    return null;
                }

                // Read Steam's loginusers.vdf to get active user out of Registry
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
                    // Look for the "AccountID" or "steamid" entry that has "MostRecent" and pass to the SteamId string.
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
            Console.WriteLine("Please select a Map to configure! Edit the saved JSON to set your ownership as your own personal account's ID.");
            
            // Fix: Add variable declaration and user input
            Console.WriteLine("1. Prometheus");
            Console.WriteLine("2. Styx");
            Console.WriteLine("3. Olympus");
            Console.Write("Enter your choice (1-3): ");
            int mapChoice;
            if (!int.TryParse(Console.ReadLine(), out mapChoice))
            {
                mapChoice = 3; // Default to Olympus if invalid input
            }

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
                // Fix: Use explicit Dictionary initialization instead of new()
                Dictionary<string, string> mapUrls = new Dictionary<string, string>
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
        public new string FullName => "Icarus Dedicated Server"; // Name of the Server
        public int PortIncrement = 1;

        public object QueryMethod = new A2S();

        //Game Server Ports
        public int Port = 27015; // Game Server Port
        public int QueryPort = 27016; // Query Port


        public async void CreateServerCFG()
        {
            GeneratePasswords();

            var serverConfig = new
            {
                name = $"{_serverData.ServerName}",
                password = "",
                saveDirectory = "./savegame",
                logDirectory = "./logs",
                ip = $"{_serverData.ServerIP}",
                gamePort = Int32.Parse(_serverData.Port),
                queryPort = Int32.Parse(_serverData.QueryPort),
                slotCount = Int32.Parse(_serverData.ServerMaxPlayer),
                gameSettingsPreset = "Default",
                gameSettings = new
                {
                    SessionName=$"{_serverData.ServerName}",
                    JoinPassword="",
                    MaxPlayers=Int32.Parse(_serverData.ServerMaxPlayer),
                    ShutdownIfNotJoinedFor=300.000000,
                    ShutdownIfEmptyFor=300.000000,
                    AllowNonAdminsToLaunchProspects=True,
                    AllowNonAdminsToDeleteProspects=False,
                    LoadProspect="",
                    CreateProspect="",
                    ResumeProspect=True,
                    LastProspectName=$"{mapName.ToLowerInvariant()}_prospect.json" // This prospect name is the name of the .json file that is used to load the map.
                },
                userGroups = new[]
                {
                    new
                    {
	                name = "Admin",
	                password = AdminPassword,
	                canKickBan = true,
	            },
	            new
	            {
	                name = "Friend",
	                password = FriendPassword,
	                canKickBan = false,
	            },
	            new
	            {
	                name = "Guest",
	                password = GuestPassword,
	                canKickBan = false,
	            }
                }
            };

            // Convert the object to JSON format
            string jsonContent = JsonConvert.SerializeObject(serverConfig, Formatting.Indented);

            // Specify the file path
            string filePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "Icarus_server.json");

            // Write the JSON content to the file
            File.WriteAllText(filePath, jsonContent);
        }
                // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }

            // Prepare start parameter
            string param = $" {_serverData.ServerParam} ";
			param += $"-ip=\"{_serverData.ServerIP}\" ";
            param += $"-gamePort={_serverData.ServerPort} ";
            param += $"-queryPort={_serverData.ServerQueryPort} ";
            param += $"-slotCount={_serverData.ServerMaxPlayer} ";
            param += $"-name=\"\"\"{_serverData.ServerName}\"\"\"";

            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = param.ToString(),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (AllowsEmbedConsole)
            {
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start Process
            try
            {
                p.Start();
                if (AllowsEmbedConsole)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }
                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }

        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                Functions.ServerConsole.SendWaitToMainWindow("^c");
                p.WaitForExit(2000);
            });
        }
        public new async Task<Process> Install()
        {
            var steamCMD = new Installer.SteamCMD();
            Process p = await steamCMD.Install(_serverData.ServerID, string.Empty, AppId, true, loginAnonymous);
            Error = steamCMD.Error;
            return p;
        }
        public new async Task<Process> Update(bool validate = false, string custom = null)
        {
            Process p = await Installer.SteamCMD.UpdateEx(serverData.ServerID, AppId, validate, custom: custom, loginAnonymous: loginAnonymous);
            await Task.Run(() => { p.WaitForExit(); });

            return p;
        }

        public new bool IsInstallValid()
        {
            return File.Exists(ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        }
        public new bool IsImportValid(string path)
        {
            string importPath = Path.Combine(path, StartPath);
            Error = $"Invalid Path! Fail to find {Path.GetFileName(StartPath)}";
            return File.Exists(importPath);
        }
        public new string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }
        public new async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }
    }
}