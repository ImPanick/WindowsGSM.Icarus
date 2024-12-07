# WindowsGSM.Icarus
 Dedicated Icarus Server plugin for WindowsGSM


This plugin will attempt to make the entire processes as minimal as possible for the user.

There will be lines in the plugin to access the registry solely to pull the SteamID of the user.
The reason behind this is the storage path of the prospects the server will utilize is saved in the local
machine's User Files...

'C:\Users\ {Your User}\AppData\Local\Icarus\Saved\PlayerData\ {SteamID}\Prospects'

Upon boot, the server config's are generated. They will then query the user on what world they would like to use.. By selecting 1-3, the
server manager (by way of plugin) will download the respective world's .json file freshly created and automatically move it into the above path.

The server will then resume booting, and the host should be visible online and bound to ports.
