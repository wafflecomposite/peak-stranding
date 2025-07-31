# Peak Stranding

**Get help from your past self**: this mod keeps the tools you deploy on a map between runs.  

Death Stranding-style asynchronous multiplayer - random items from other players appearing in your world - is planned for future updates.

This is a **server-side** mod; only the lobby host has to install it. The host saves every item deployed during a run by any player and syncs them back to clients at the start of subsequent runs on the same map. The mod does nothing on the client side, so having it installed there won’t hurt anything.

## Known issues

### Overloading the network
It’s easy to flood a map with items, especially when they're clustered together - or worse, a pile of ropes, which are heavy to synchronize. If players connecting to you get kicked with Proton errors, delete your saves or disable the mod before the map rolls to a new seed. Reconnects during gameplay and mods that raise the player limit or speed up restarts only make the problem worse.

### Inaccuracies in item replication
Some items can’t be reproduced exactly. Ropes may be arranged differently than when you left them, and the rope-shooter's anchor angle is slightly off.

## Planned (Not Yet Implemented)
- Asynchronous multiplayer: random items from other players who have played on the same map
- Wide range of configuration options

### Where are the saves stored?
Deployed items are saved per map seed in `\BepInEx\config\PeakStranding\PlacedItems` folder.





### Thunderstore Packaging (remove later)

This template comes with Thunderstore packaging built-in, using [TCLI](<https://github.com/thunderstore-io/thunderstore-cli>).

You can build Thunderstore packages by running:

```sh
dotnet build -c Release -target:PackTS -v d
```

> [!NOTE]  
> You can learn about different build options with `dotnet build --help`.  
> `-c` is short for `--configuration` and `-v d` is `--verbosity detailed`.

The built package will be found at `artifacts/thunderstore/`.
