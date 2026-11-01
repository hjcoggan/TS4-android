# Top Spin 4 Recomp

An unofficial PC port of the Xbox 360 game Top Spin 4 (2011), built by recompiling the game's own code. It contains no game data: you supply your own disc and Title Update 1, and the Launcher prepares them once. Terms are defined in [GLOSSARY.md](GLOSSARY.md); goals and scope are in [docs/goals.md](docs/goals.md).

## Help

This section is what the Launcher's Help button opens.

### What you need

Two files, both from your own copy of the game:

1. **A disc image (`.iso`)** of your own Top Spin 4 Xbox 360 disc. Only one edition works: the disc with media ID `60819732`. The Launcher tells you if the file you picked is another edition or another game.
2. **Title Update 1**, the update file Top Spin 4 received on Xbox 360. The port is built on it and does not run without it.

The Launcher never changes your disc image or update file. It copies the game files it needs (about 6 GB) into the game folder (beside `topspin4.exe`) once. Everything the game and the Launcher write, including your settings and saves, stays in that folder, so copying it copies the whole install and deleting it removes everything. The folder must be one you can write to (not Program Files).

### Dumping your own disc

Make an image of your own Top Spin 4 disc on an Xbox 360 or a PC with a compatible disc drive, using a disc-dumping tool of your choice, and save it as an `.iso` file. Pick that file in the Launcher's setup.

### Getting Title Update 1 from your own console

Title Update 1 is a content package your console stored when it downloaded the update. Copy it from your console's storage (from the `Content` or `Cache` folder on its hard drive or USB drive) to your PC. The file is named like `TU_1A5822P_000000C000000.0000000000083`, with no extension. Pick it in the Launcher's setup.

### Changing settings

Every setting is in the Launcher's **Settings**, one tab per section, so you can set the game up before you play. You can also change every setting in the game, while playing, on its own Settings page:

- In the game's OPTIONS menu, choose **PC SETTINGS**.
- On a keyboard, press **F4**.
- On a controller, hold **Back** and **Start** together.

In either Settings page, press A (or Enter) on a row to change it. To remap a key or button, press A on its row, then the key or button to use; Esc or B cancels. Changes are saved at once to the Settings file in the game folder (`userdata`), so the Launcher and the game show the same values.

### Achievements

The Launcher's **Achievements** page lists every achievement on your disc. The ones you have earned come first, newest first, with the date you earned them; locked ones are greyed out with a padlock and say how to earn them. When you earn one in the game, an "Achievement unlocked" notice slides in at the bottom of the screen. Your unlocks are saved in the game folder (`userdata\achievements`).

The notice is silent unless you add a sound: put a `.wav` file named `achievement.wav` in the `userdata` folder and it plays with each notice. The port ships no sound of its own for this.

### Renderer

- **Native (recommended)** draws with the port's own renderer.
- **Classic (compatibility)** uses the original renderer. Switch to it if the game does not start or the graphics card stops working. The change takes effect the next time the game starts.

### If the game will not start

Open the Launcher and use **Safe start** under "Trouble starting?". It runs one session on the default settings and leaves your Settings file unchanged.

After a crash or a graphics failure the Launcher comes back with Safe start up front, and after a crash with **Open crash report** to attach to your issue.

### Starting without the Launcher

**Create shortcut** on the Launcher's home screen puts "Top Spin 4" on your Desktop and in the Start menu. It starts the game directly with your saved settings, and creating it again replaces the old one. If setup is missing or the game files were moved away, the game opens the Launcher on the setup page and says why.

### Logs

The Launcher's **Open logs** button opens the `logs` folder beside the game. Attach the newest log when you report a problem.
