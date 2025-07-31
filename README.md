# Peak Stranding

Get some help from your past self: this mod keeps the tools you deployed on map between runs.

Death Stranding-style asynchronous multiplayer with the appearance random items from random players is planned for future updates.

This is a server-side mod, only the lobby host have to install it. Hosts saves items that are deployed during the run by any player, and then syncs them back to clients at the start of next runs on the same map. Does nothing on client side, shouldn't hurt either.

## Known issues

### Overloading the network
It's pretty easy to overload the map with items, especially if they're close together, especially if it's a bunch of ropes that are heavy to syncronize. If players connecting to you are getting kicked due to Proton errors, delete your saves or disable the mod before map switches to a new seed. Reconnects during gameplay and mods that increase the player limit or speed up the run restart are bound to add insult to injury.

### Inaccuracies in item replication
Some items cannot be replicated precisely. Expect the ropes to be arranged differently than you left them. The rope shooter's anchor angle is also a bit off.

## Planned (Not Yet Implemented)
- Asynchronous multiplayer - random items from other players who have played on this map
- Wide range of settings

### Where are the saves stored?
Deployed items are tied to map seed and saved to `\BepInEx\config\PeakStranding\PlacedItems` folder.





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
