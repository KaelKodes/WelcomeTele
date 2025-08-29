<h1 align="center">WelcomeTele<p align="center">


Teleports brand-new players (first time only) to a preset location as they finish their first spawn. Includes a reliable facing fix, configurable delays, and a customizable welcome message with placeholders.

Author: CTS Kael
Version: 1.3.0
Game: Rust
Framework: uMod/Oxide


## <h1 align="center">Overview<p align="center">


WelcomeTele lets you define a spawn location (including facing/yaw) and automatically teleports each player there once. It waits until the player is fully spawned (post-snapshot), then optionally pins the player’s view direction so it will not snap back.


## <h1 align="center">Features<p align="center">

One-time welcome teleport per player

Works on true first spawn (not only after the first death)

Facing lock (yaw/pitch) so players look the intended direction after teleport

Configurable message, delays, and coordinate display

Admin tools to set, show, and clear the destination

Safety guard to avoid teleporting to 0,0,0

---

##<h1 align="center"> Requirements<p align="center">

Rust server with uMod/Oxide

---

## <h1 align="center">Installation<p align="center">

Place WelcomeTele.cs into oxide/plugins/.

Load or reload the plugin if needed:

oxide.reload WelcomeTele


A default config is created at:

oxide/config/WelcomeTele.json


The saved teleport position is stored at:

oxide/data/WelcomeTele.json

---

## <h1 align="center">Permissions<p align="center">

welcometele.admin — required for admin commands (/wtset, /wtwhere, /wtclear)

welcometele.used — do not assign manually; granted automatically after a player is teleported once

Reset a single player (allow them to be teleported again):

oxide.revoke user <steamid> welcometele.used

---

## <h1 align="center">Admin Chat Commands<p align="center">

/wtset
Saves your current position and facing as the welcome teleport destination.

/wtwhere
Displays the currently saved destination and yaw.

/wtclear
Clears the saved destination (teleports are disabled until set again).

All commands require the welcometele.admin permission.

Configuration

Edit oxide/config/WelcomeTele.json, then reload:

oxide.reload WelcomeTele


Default config:

{
  "TeleportMessage": "Welcome, {name}! You’ve been teleported to: {x}, {y}, {z}",
  "ShowCoordinates": true,
  "TeleportDelay": 1.0,
  "FacingDelay": 0.15,
  "EnableFacingFix": true
}


---

## <h1 align="center">Options:<p align="center">

TeleportMessage (string)
Chat message shown to the player after teleport. Placeholders:

{name} = player display name

{x}, {y}, {z} = destination coordinates (one decimal)

ShowCoordinates (bool)
If false, the {x}, {y}, {z} placeholders are stripped from the message.

TeleportDelay (float, seconds)
Wait time after the player is fully spawned (post-snapshot) before teleporting. Typical range: 0.5–2.0.

FacingDelay (float, seconds)
Wait time after teleport before applying the facing direction. Typical range: 0.15–0.30.

EnableFacingFix (bool)
If true, the plugin pins the player’s yaw and pitch to prevent client snap-back.

---

## <h1 align="center">CFG Files<p align="center">

Config: oxide/config/WelcomeTele.json

Saved Position: oxide/data/WelcomeTele.json (written after /wtset)

---

## <h1 align="center">How It Works<p align="center">

Hooks multiple spawn events: OnPlayerInit, OnPlayerSpawned, OnPlayerRespawned, and OnPlayerSleepEnded

Waits until the player is fully ready (not receiving snapshot, alive, connected), then applies the teleport after TeleportDelay

If enabled, applies a facing fix after FacingDelay by updating entity rotation, eyes rotation, server-side view angles, and sending input.setyaw/pitch to the client

---

## <h1 align="center">Quickstart<p align="center">

Stand where you want new players to appear and face the direction they should look.

Run:

/wtset


Optionally adjust TeleportDelay and FacingDelay in oxide/config/WelcomeTele.json.

Reload:

oxide.reload WelcomeTele

## <h1 align="center">Troubleshooting<p align="center">

Message: No welcome-teleport location set
You have not run /wtset yet, or you cleared it with /wtclear.

Player did not teleport on first join
Increase TeleportDelay to 1.5–2.0. Confirm EnableFacingFix is true. Ensure the player does not already have welcometele.used:

oxide.revoke user <steamid> welcometele.used


Facing did not apply
Increase FacingDelay to 0.20–0.30 and keep EnableFacingFix true.

Teleported to an unsafe or wrong spot
Move to a better location and run /wtset again. The plugin ignores 0,0,0 to prevent accidental origin teleports.

---

## <h1 align="center">Changelog<p align="center">

1.3.0
Robust first-spawn handling with post-snapshot readiness check. Multiple hooks to catch edge cases.

1.2.0
Config file, message templating, delays, and facing toggle.

1.1.0
Safer data handling, lowercase permissions, admin quality-of-life commands.

1.0.0
Initial release.

## <h1 align="center">License<p align="center">

Use, modify, and distribute freely. Please keep the header credit to CTS Kael.
