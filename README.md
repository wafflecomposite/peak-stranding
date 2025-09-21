# Peak Stranding

> Once, there was an expedition


### 👍 Now with likes 👍

This PEAK mod adds Death Stranding-style asynchronous multiplayer - random items from other players appear in your world and display the names of the scouts who placed them. 
You can also save and load your items locally to keep them between the runs on the same map (**disabled by default**).

Leave a feedback on the [PEAK Modding discord server](https://discord.gg/dSEtuhxHg4) in [Peak Stranding thread](https://discord.com/channels/1363179626435707082/1400596905763016794) or in the [GitHub issues](https://github.com/wafflecomposite/peak-stranding/issues)

This is a *server-side* **required** + *client-side* **highly recommended** mod.

## Known issues

### Overloading the scene
If players connecting to you are getting kicked with Proton errors - reduce the amount of online structures or turn off loading locally saved structures (disabled by default).

### Ropes are troublesome?
Might be caused by the optimizer. Try disabling `Experimental_Rope_Optimizer` in config. Improved in 0.9.

### Deleting the items
The online chain item cannot be deleted. Magic bean vines disappears only from the host side when deleted. This is due to bugs in the game itself and may or may not be fixed in the future.

## Config
The config file is located at `BepInEx/config/com.github.wafflecomposite.PeakStranding.cfg`. You can also change the settings in-game by installing [ModConfig](https://thunderstore.io/c/peak/p/PEAKModding/ModConfig/). Most settings require restarting the run to take effect.  
### Local
- `Save_Structures_Locally` - Whether to save structures placed in your lobby locally. Default: `true`.
- `Load_Local_Structures` - Whether to load previously saved structures at the start of a new run. Default: `false`. You can enable it instead of online structures to save your progress through the map.
- `Local_Structures_Limit` - How many local structures to load at the start of a new run (-1 for no limit). If you have more than this, only the most recent ones will be loaded. Default: `-1`.
### Online
- `Send_Structures_To_Online` - Whether to share structures placed in your lobby to other players. Default: `true`.
- `Load_Online_Structures` - Whether to load random structures placed by other players in your world. Default: `true`.
- `Online_Structures_Limit` - How many remote structures to load at the start of a new run. Default: `40`. Limited to `300` on server side.
- `Structure_Allow_List` - A space-separated list of structure prefab names that are allowed to be placed by other players. Leave empty to allow all structures. Default: `bounceshroom piton shelfshroom flagseagull flagturtle chainshooter magicbean ropeshooter ropespool stove`
- `Allow_Clients_Like` - Allow clients to like online structures. Default: `true`.
- `Allow_Clients_Delete` - Allow clients to delete online structures. Default: `true`.
- `Custom_Server_Api_BaseUrl` - Custom Server URL. Leave empty to use official Peak Stranding server
### UI 
- `Show_Structure_Overlay` - Whether to show the in-world overlay for structures placed by other players (including like/remove prompts). Default: `true`.
- `Show_Toasts` - Whether to show toasts with stats. Default: `true`.
### Experimental
- `Experimental_Rope_Optimizer` - Fixes severe lag caused by a large number of ropes by disabling their physics and throttling network sync when not in use. Installing the mod on clients is recommended for best expirience. Try to disable if ropes become unusable.

## Planned (Not Yet Implemented)
### Gameplay
- A way to view your 'like' stats
- Proper UI
- Making random non-deployable items (i.e. medkit, food) appearing in your world if lost or abandoned by other players
### Optimization
- MorePeak and custom map support (partially done)
### Support custom items from other mods
- Per-mod basis; may come with updates

### Where are the local saves stored?
Deployed items are saved per map seed in `BepInEx/config/PeakStranding/PlacedItems` folder.
