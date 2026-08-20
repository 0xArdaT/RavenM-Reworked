# RavenM Reworked

A Ravenfield multiplayer mod, refactored and updated for **Early Access 38 (EA38)**.

[![Release](https://img.shields.io/github/v/release/0xArdaT/RavenM-Reworked?color=7289da&label=Release&logo=GitHub&style=for-the-badge)](https://github.com/0xArdaT/RavenM-Reworked/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/0xArdaT/RavenM-Reworked/total.svg?label=Downloads&logo=GitHub&style=for-the-badge)](https://github.com/0xArdaT/RavenM-Reworked/releases)
[![Stars](https://img.shields.io/github/stars/0xArdaT/RavenM-Reworked?color=yellow&label=Stars&logo=GitHub&style=for-the-badge)](https://github.com/0xArdaT/RavenM-Reworked/stargazers)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg?style=for-the-badge)](LICENSE)

## ✨ What's Changed in Reworked?

- **EA 38 Engine Support:** Fixed 60+ breaking C# API calls, type mismatches, and internal Unity changes.
- **Log Spam Removal:** Stripped high-frequency debug logs (such as projectile spawn spam) to prevent huge `LogOutput.txt` files and eliminate micro-stutters.
- **Version Check Bypass:** Updated internal version checking to allow seamless connectivity on EA 38.

---

# Installing

<b>Important Note:</b> RavenM does not support BepInEx version 6. Please ensure to install the latest version of BepInEx 5.x.x to complete the installation.

This mod depends on [BepInEx](https://github.com/BepInEx/BepInEx), a cross-platform Unity modding framework. 

First, install BepInEx into Ravenfield following the installation instructions [here](https://docs.bepinex.dev/articles/user_guide/installation/index.html). As per the instructions, make sure to run the game at least once with BepInEx installed before adding the mod to generate config files.

Next, download the latest RavenM Reworked release [here](https://github.com/0xArdaT/RavenM-Reworked/releases/latest) and unzip the file, place `RavenM.dll` into `Ravenfield/BepInEx/plugins/`. Optionally, you may also place `RavenM.pdb` to generate better debug information in the logs.

Run the game and RavenM should now be installed.

You can add a startup argument `-noravenm` on Steam to temporarily unload RavenM plugin.

**Please be aware pirated/non-official copies of Ravenfield may encounter issues when using RavenM. The mod relies entirely on Steam to transfer game data and mods securely between players.**

---

# Playing

To play together, one player must be the host. This player will control the behaviour of all the bots, the game parameters, and the current game state. All other players will connect to the host during the match. Despite this, no port-forwarding is required! All data is routed through the Steam relay servers, which means fast, easy and encrypted connections with DDoS protection and Steam authentication.

Now, press `M` button to open the connection menu.

## Hosting
Press `Host` and choose whether the lobby is friends only or not. After pressing `Start`, you will be put into a lobby. At this point, you cannot exit the `Instant Action` page without leaving the lobby. Other players can connect with the `Lobby ID` or through the server browser.

## Joining
Press `Join` and paste the `Lobby ID` of an existing lobby. At this point, you cannot edit any of the options in the `Instant Action` page except for your team. You also cannot start the match. The settings chosen by the host will reflect on your own options.

## On Gaming

Press `Y` to type a global message (press `Enter` to send, `Esc` to close the textbox), press `U` to type a message to your team.

Press `Enter` to open the Loadout UI.

Press `CapsLock` to use voice chat (positional).

Press `~` to place a marker.

Have fun!

![Credit: Sofa#8366](https://steamuserimages-a.akamaihd.net/ugc/1917988387306327667/C90622D8C9B8B654E187AA5038A84759DFF050D9/)

---

# Building from source

Visual Studio 2019+ / Visual Studio Code / Cursor is recommended. .NET Framework 4.6+ or .NET SDK is required.

Steps to build:

1. Clone the repository to your local machine:
   git clone https://github.com/0xArdaT/RavenM-Reworked.git

2. Build project:
   dotnet build RavenM

   Dependencies should be restored when building. If not, run:
   dotnet restore

---

# Credits & License

- Original **RavenM** created by the RavenM Development Team.
- Reworked, updated for EA 38, and maintained by **Arda (0xArdaT)**.
- Discord Rich Presence Images Credit: `Wolffe#6986`
- Licensed under the **GNU General Public License v3.0 (GPLv3)**.
